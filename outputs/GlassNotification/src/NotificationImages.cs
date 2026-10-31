using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Xml;
namespace Glass {
internal sealed class ToastImageRef {public string Placement;public string Source;public bool CropCircle;}
internal interface INotificationPayloadSource {
 bool IsAvailable {get;}
 // Returns the toast XML for a notification, or null when Windows has not stored it (yet).
 string FindPayload(uint id);
}
// Chrome writes notification images as temporary .tmp files in "Notification Resources" and deletes them shortly
// after the toast is shown or suppressed. Watching that folder caches newly written image bytes in memory so they
// remain available even if Chrome deletes the temporary file before the notification listener poll completes.
internal static class NotificationResourceCache {
 const int MaximumEntries=64;
 static readonly object gate=new object();
 static readonly Dictionary<string,byte[]> entries=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
 static readonly Queue<string> order=new Queue<string>();
 static readonly Dictionary<string,FileSystemWatcher> watchers=new Dictionary<string,FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
 static bool defaultChecked;
 internal static void EnsureDefaultWatcher(){
  if(defaultChecked)return;
  defaultChecked=true;
  try{
   string localAppData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   if(string.IsNullOrEmpty(localAppData))return;
   string chromeResources=Path.Combine(localAppData,"Google","Chrome","User Data","Notification Resources");
   WatchDirectory(chromeResources);
  }catch(Exception){}
 }
 internal static void WatchDirectory(string directory){
  if(string.IsNullOrWhiteSpace(directory))return;
  try{
   string full=Path.GetFullPath(directory);
   if(!Directory.Exists(full))return;
   lock(gate){
    if(watchers.ContainsKey(full))return;
    var watcher=new FileSystemWatcher(full){
     NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.CreationTime|NotifyFilters.Size,
     Filter="*.*",
     IncludeSubdirectories=false,
     EnableRaisingEvents=true
    };
    FileSystemEventHandler onChanged=delegate(object s,FileSystemEventArgs e){CaptureFileWithRetry(e.FullPath);};
    RenamedEventHandler onRenamed=delegate(object s,RenamedEventArgs e){CaptureFileWithRetry(e.FullPath);};
    watcher.Created+=onChanged;
    watcher.Changed+=onChanged;
    watcher.Renamed+=onRenamed;
    watchers[full]=watcher;
   }
   foreach(string existing in Directory.GetFiles(full))CaptureFile(existing);
  }catch(Exception){}
 }
 static void CaptureFileWithRetry(string path){
  if(CaptureFile(path))return;
  ThreadPool.QueueUserWorkItem(delegate{
   for(int i=0;i<4;i++){
    Thread.Sleep(15);
    if(CaptureFile(path))return;
   }
  });
 }
 internal static bool CaptureFile(string path){
  string normalized=NotificationImageResolver.ResolveLocalPath(path);
  if(normalized==null)return false;
  byte[] bytes=NotificationImageResolver.ReadFileBytes(normalized);
  if(bytes==null||bytes.Length==0)return false;
  Store(normalized,bytes);
  return true;
 }
 internal static void Store(string normalizedPath,byte[] bytes){
  if(string.IsNullOrEmpty(normalizedPath)||bytes==null||bytes.Length==0||bytes.Length>NotificationImageResolver.MaximumFileBytes)return;
  lock(gate){
   if(!entries.ContainsKey(normalizedPath)){
    if(entries.Count>=MaximumEntries)entries.Remove(order.Dequeue());
    order.Enqueue(normalizedPath);
   }
   entries[normalizedPath]=bytes;
  }
 }
 internal static byte[] Get(string normalizedPath){
  if(string.IsNullOrEmpty(normalizedPath))return null;
  lock(gate){
   byte[] bytes;
   return entries.TryGetValue(normalizedPath,out bytes)?bytes:null;
  }
 }
 internal static bool IsTransientResourcePath(string normalizedPath){
  if(string.IsNullOrEmpty(normalizedPath))return false;
  return normalizedPath.IndexOf("\\Notification Resources\\",StringComparison.OrdinalIgnoreCase)>=0
   ||normalizedPath.IndexOf("/Notification Resources/",StringComparison.OrdinalIgnoreCase)>=0;
 }
}
// Windows exposes only the text of a notification through its listener API. The images live in the toast XML that
// Windows keeps for each notification, so they are read from there. Only files on local fixed drives are opened:
// URLs, network paths and special device paths are never touched.
internal sealed class NotificationImageResolver {
 internal const long MaximumFileBytes=4*1024*1024;
 internal const int LogoPixels=128,ContentPixels=256;
 readonly INotificationPayloadSource payloads;
 public NotificationImageResolver(INotificationPayloadSource source){
  if(source==null)throw new ArgumentNullException("source");
  payloads=source;
  NotificationResourceCache.EnsureDefaultWatcher();
 }
 // True when the lookup is finished (images attached or definitively absent); false when the payload is not stored yet.
 public bool TryAttach(SystemNotification notification){
  if(notification==null||!payloads.IsAvailable)return true;
  NotificationResourceCache.EnsureDefaultWatcher();
  string xml=payloads.FindPayload(notification.Id);if(xml==null)return false;
  string launch=ParseLaunch(xml);if(!string.IsNullOrEmpty(launch))notification.LaunchArguments=launch;
  ToastImageRef logo=null,hero=null,inline=null;
  foreach(var image in ParseImages(xml)){
   string placement=image.Placement??"";
   if(string.Equals(placement,"appLogoOverride",StringComparison.OrdinalIgnoreCase)){if(logo==null)logo=image;}
   else if(string.Equals(placement,"hero",StringComparison.OrdinalIgnoreCase)){if(hero==null)hero=image;}
   else if(placement.Length==0||string.Equals(placement,"inline",StringComparison.OrdinalIgnoreCase)){if(inline==null)inline=image;}
  }
  if(logo!=null){notification.LogoImage=LoadFrom(logo.Source,LogoPixels);notification.LogoCircle=logo.CropCircle&&notification.LogoImage!=null;}
  var content=hero!=null?hero:inline;
  if(hero!=null)notification.ContentImage=LoadFrom(hero.Source,ContentPixels);
  if(notification.ContentImage==null&&inline!=null)notification.ContentImage=LoadFrom(inline.Source,ContentPixels);
  return (logo==null||notification.LogoImage!=null||ResolveLocalPath(logo.Source)==null)
   &&(content==null||notification.ContentImage!=null||ResolveLocalPath(content.Source)==null);
 }
 static BitmapSource LoadFrom(string source,int maximumSide){string path=ResolveLocalPath(source);return path==null?null:Load(path,maximumSide);}
 internal static string ParseLaunch(string xml){
  if(string.IsNullOrEmpty(xml))return null;
  var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=262144,IgnoreComments=true};
  try{
   using(var reader=XmlReader.Create(new StringReader(xml),settings))
    while(reader.Read())
     if(reader.NodeType==XmlNodeType.Element&&string.Equals(reader.LocalName,"toast",StringComparison.OrdinalIgnoreCase)){
      string launch=reader.GetAttribute("launch");
      return string.IsNullOrWhiteSpace(launch)?null:launch.Trim();
     }
  }catch(XmlException){}
  return null;
 }
 internal static List<ToastImageRef> ParseImages(string xml){
  var result=new List<ToastImageRef>();if(string.IsNullOrEmpty(xml))return result;
  var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=262144,IgnoreComments=true};
  try{
   using(var reader=XmlReader.Create(new StringReader(xml),settings))
    while(reader.Read()&&result.Count<8)
     if(reader.NodeType==XmlNodeType.Element&&string.Equals(reader.LocalName,"image",StringComparison.OrdinalIgnoreCase)){
      string source=reader.GetAttribute("src");if(string.IsNullOrWhiteSpace(source))continue;
      result.Add(new ToastImageRef {Placement=reader.GetAttribute("placement"),Source=source,CropCircle=string.Equals(reader.GetAttribute("hint-crop"),"circle",StringComparison.OrdinalIgnoreCase)});
     }
  }catch(XmlException){}
  return result;
 }
 // Accepts a plain drive path or a file: URI that points at a local fixed drive. Everything else yields null.
 internal static string ResolveLocalPath(string source){
  if(string.IsNullOrWhiteSpace(source)||source.Length>2048||source.IndexOf('\0')>=0)return null;
  string path=source.Trim();
  if(path.StartsWith("file:",StringComparison.OrdinalIgnoreCase)){
   Uri uri;if(!Uri.TryCreate(path,UriKind.Absolute,out uri)||!uri.IsFile||uri.IsUnc)return null;path=uri.LocalPath;
  }else if(path.Contains("://"))return null;
  if(path.Length<4||!char.IsLetter(path[0])||path[1]!=':'||(path[2]!='\\'&&path[2]!='/'))return null;
  try{
   string full=Path.GetFullPath(path);
   if(full.Length<4||full.StartsWith("\\\\",StringComparison.Ordinal)||full.IndexOf(':',2)>=0)return null;
   if(new DriveInfo(full.Substring(0,1)).DriveType!=DriveType.Fixed)return null;
   return full;
  }catch(ArgumentException){return null;}catch(NotSupportedException){return null;}catch(PathTooLongException){return null;}catch(IOException){return null;}
 }
 internal static byte[] ReadFileBytes(string path){
  try{
   var info=new FileInfo(path);
   if(!info.Exists||(info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length<1||info.Length>MaximumFileBytes)return null;
   using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,4096,FileOptions.SequentialScan)){
    if(file.Length<1||file.Length>MaximumFileBytes)return null;
    var bytes=new byte[file.Length];int read=0;while(read<bytes.Length){int n=file.Read(bytes,read,bytes.Length-read);if(n<=0)return null;read+=n;}
    return bytes;
   }
  }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(NotSupportedException){return null;}catch(ArgumentException){return null;}
 }
 internal static BitmapSource Load(string path,int maximumSide){
  try{
   byte[] bytes=ReadFileBytes(path);
   if(bytes!=null){
    if(NotificationResourceCache.IsTransientResourcePath(path))NotificationResourceCache.Store(path,bytes);
   }else if(NotificationResourceCache.IsTransientResourcePath(path)){
    try{string dir=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(dir))NotificationResourceCache.WatchDirectory(dir);}catch(Exception){}
    bytes=NotificationResourceCache.Get(path);
   }
   if(bytes==null||bytes.Length==0)return null;
   return DecodeBytes(bytes,maximumSide);
  }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(NotSupportedException){return null;}catch(FormatException){return null;}
  catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}catch(OverflowException){return null;}catch(COMException){return null;}
 }
 internal static BitmapSource DecodeBytes(byte[] bytes,int maximumSide){
  using(var stream=new MemoryStream(bytes,false)){
   var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);if(decoder.Frames.Count==0)return null;
   int width=decoder.Frames[0].PixelWidth,height=decoder.Frames[0].PixelHeight;if(width<1||height<1||width>4096||height>4096)return null;
   double scale=Math.Min(1,(double)maximumSide/Math.Max(width,height));stream.Position=0;
   var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
   if(width>=height)image.DecodePixelWidth=Math.Max(1,(int)Math.Round(width*scale));
   else image.DecodePixelHeight=Math.Max(1,(int)Math.Round(height*scale));
   image.StreamSource=stream;image.EndInit();image.Freeze();return image;
  }
 }
}
internal sealed class WpnDatabasePayloadSource : INotificationPayloadSource {
 const long MaximumDatabaseBytes=128L*1024*1024;const int MaximumPayloadBytes=262144;
 const int SqliteRow=100,OpenReadOnly=1,OpenReadWrite=2;
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_open_v2",CallingConvention=CallingConvention.Cdecl)]static extern int Open(byte[] file,out IntPtr database,int flags,IntPtr vfs);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_busy_timeout",CallingConvention=CallingConvention.Cdecl)]static extern int BusyTimeout(IntPtr database,int ms);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_prepare_v2",CallingConvention=CallingConvention.Cdecl)]static extern int Prepare(IntPtr database,byte[] sql,int length,out IntPtr statement,IntPtr tail);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_bind_int64",CallingConvention=CallingConvention.Cdecl)]static extern int BindInt64(IntPtr statement,int index,long value);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_step",CallingConvention=CallingConvention.Cdecl)]static extern int Step(IntPtr statement);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_column_blob",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr ColumnBlob(IntPtr statement,int column);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_column_bytes",CallingConvention=CallingConvention.Cdecl)]static extern int ColumnBytes(IntPtr statement,int column);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_finalize",CallingConvention=CallingConvention.Cdecl)]static extern int Finalize(IntPtr statement);
 [DllImport("winsqlite3.dll",EntryPoint="sqlite3_close",CallingConvention=CallingConvention.Cdecl)]static extern int Close(IntPtr database);
 [DllImport("kernel32.dll",EntryPoint="CopyFileW",CharSet=CharSet.Unicode,SetLastError=true)]
 [return:MarshalAs(UnmanagedType.Bool)]static extern bool CopyFile(string source,string destination,bool failIfExists);
 readonly string folder;bool unsupported;
 public WpnDatabasePayloadSource():this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Notifications")){}
 internal WpnDatabasePayloadSource(string notificationsFolder){folder=notificationsFolder;}
 public bool IsAvailable {get{return !unsupported&&File.Exists(Path.Combine(folder,"wpndatabase.db"));}}
 // Query the live database in read-only mode first so temporary Chrome images are read before deletion;
 // fall back to a private snapshot (database plus write-ahead log) when direct access is unavailable.
 public string FindPayload(uint id){
  string live=Path.Combine(folder,"wpndatabase.db");
  try{
   if(File.Exists(live)&&new FileInfo(live).Length<=MaximumDatabaseBytes){
    string direct=Query(live,id,OpenReadOnly);
    if(!string.IsNullOrEmpty(direct))return direct;
   }
  }catch(IOException){}catch(UnauthorizedAccessException){}
  catch(DllNotFoundException){unsupported=true;return null;}catch(EntryPointNotFoundException){unsupported=true;return null;}
  string snapshot=Path.Combine(Path.GetTempPath(),"GlassNotification.Images",Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(snapshot);
   if(!Copy(live,Path.Combine(snapshot,"wpndatabase.db"),true))return null;
   if(!Copy(Path.Combine(folder,"wpndatabase.db-wal"),Path.Combine(snapshot,"wpndatabase.db-wal"),false))return null;
   return Query(Path.Combine(snapshot,"wpndatabase.db"),id,OpenReadWrite);
  }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
  catch(DllNotFoundException){unsupported=true;return null;}catch(EntryPointNotFoundException){unsupported=true;return null;}
  finally{try{if(Directory.Exists(snapshot))Directory.Delete(snapshot,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
 }
 static bool Copy(string from,string to,bool required){
  if(!File.Exists(from))return !required;
  // CopyFileW supports copying the live SQLite files without managed reads of locked ranges.
  if(new FileInfo(from).Length>MaximumDatabaseBytes)return false;
  return CopyFile(from,to,true)&&new FileInfo(to).Length<=MaximumDatabaseBytes;
 }
 static string Query(string database,uint id,int flags){
  IntPtr db=IntPtr.Zero,statement=IntPtr.Zero;
  try{
   if(Open(Encoding.UTF8.GetBytes(database+"\0"),out db,flags,IntPtr.Zero)!=0)return null;
   BusyTimeout(db,120);
   if(Prepare(db,Encoding.UTF8.GetBytes("SELECT Payload FROM Notification WHERE Id=?1\0"),-1,out statement,IntPtr.Zero)!=0)return null;
   BindInt64(statement,1,id);
   if(Step(statement)!=SqliteRow)return null;
   int length=ColumnBytes(statement,0);IntPtr blob=ColumnBlob(statement,0);
   if(length<=0||blob==IntPtr.Zero)return null;
   if(length>MaximumPayloadBytes)return "";
   var bytes=new byte[length];Marshal.Copy(blob,bytes,0,length);return Decode(bytes);
  }finally{if(statement!=IntPtr.Zero)Finalize(statement);if(db!=IntPtr.Zero)Close(db);}
 }
 internal static string Decode(byte[] bytes){
  if(bytes.Length>=3&&bytes[0]==0xEF&&bytes[1]==0xBB&&bytes[2]==0xBF)return Encoding.UTF8.GetString(bytes,3,bytes.Length-3);
  if(bytes.Length>=2&&bytes[0]==0xFF&&bytes[1]==0xFE)return Encoding.Unicode.GetString(bytes,2,bytes.Length-2);
  if(bytes.Length>=2&&bytes[1]==0)return Encoding.Unicode.GetString(bytes);
  return Encoding.UTF8.GetString(bytes);
 }
}
}
