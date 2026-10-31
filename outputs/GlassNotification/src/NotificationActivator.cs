using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace Glass {
public interface INotificationActivator {
 bool Activate(SystemNotification notification);
}
public sealed class ChromeNotificationActivator : INotificationActivator {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 struct NOTIFICATION_USER_INPUT_DATA {
  [MarshalAs(UnmanagedType.LPWStr)] public string Key;
  [MarshalAs(UnmanagedType.LPWStr)] public string Value;
 }
 [ComImport,Guid("53E31837-6600-4A81-9395-75CFFE746F94"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface INotificationActivationCallback {
  void Activate(
   [In,MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
   [In,MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
   [In,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=3)] NOTIFICATION_USER_INPUT_DATA[] data,
   [In,MarshalAs(UnmanagedType.U4)] uint dataCount);
 }
 [DllImport("ole32.dll",ExactSpelling=true,PreserveSig=true)]
 static extern int CoCreateInstance([In] ref Guid rclsid,IntPtr pUnkOuter,uint dwClsContext,[In] ref Guid riid,[MarshalAs(UnmanagedType.Interface)] out INotificationActivationCallback ppv);
 [DllImport("ole32.dll",ExactSpelling=true)]
 static extern int CoInitializeEx(IntPtr reserved,uint flags);
 [DllImport("ole32.dll",ExactSpelling=true)]
 static extern void CoUninitialize();
 [DllImport("user32.dll")]
 static extern bool AllowSetForegroundWindow(int dwProcessId);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
 const int ASFW_ANY=-1;
 const uint CLSCTX_LOCAL_SERVER=4;
 static readonly Guid CallbackIid=new Guid("53E31837-6600-4A81-9395-75CFFE746F94");
 static readonly Guid DefaultChromeClsid=new Guid("A2C6CB58-C076-425C-ACB7-6D19D64428CD");
 static readonly Guid[] KnownChromeClsids=new[]{
  DefaultChromeClsid,
  new Guid("B9206A83-1628-40F8-9F79-99E4F572C9C2"), // Chrome Beta
  new Guid("F4534B46-86F0-459E-B198-8FF85E6E8CA4"), // Chrome Dev
  new Guid("0A0740E2-C2B7-4475-A059-F02A0B7F869A"), // Chrome Canary
  new Guid("7D4A0453-6119-48CB-B1FD-58134F4F6825")  // Chromium
 };
 static readonly ChromeActivationRoutes routes=new ChromeActivationRoutes(ReadRegisteredClsids,ScanChromeClsids,delegate{return DateTime.UtcNow;});
 readonly Func<string,string,bool> comActivator;
 readonly Func<string,string,bool> processLauncher;
 readonly Func<string> chromePathResolver;
 public ChromeNotificationActivator():this(TryActivateCom,StartProcess,FindChromeExecutable){}
 internal ChromeNotificationActivator(Func<string,string,bool> com,Func<string,string,bool> launcher,Func<string> pathResolver){
  comActivator=com??TryActivateCom;
  processLauncher=launcher??StartProcess;
  chromePathResolver=pathResolver??FindChromeExecutable;
 }
  public bool Activate(SystemNotification notification){
   if(notification==null)return false;
   string senderId=string.IsNullOrWhiteSpace(notification.SenderId)?"Chrome":notification.SenderId.Trim();
   string launch=notification.LaunchArguments;
   string targetUrl=ExtractTargetUrl(launch,notification.Body);
   bool isChrome=senderId.IndexOf("Chrome",StringComparison.OrdinalIgnoreCase)>=0||senderId.IndexOf("Chromium",StringComparison.OrdinalIgnoreCase)>=0;
   if(isChrome){
    string profileDir=ExtractProfileDirectory(launch);
    bool chromeRunning=IsChromeRunning();
    if(!string.IsNullOrWhiteSpace(launch)){
     if(!chromeRunning){
      string chromeExe=chromePathResolver();
      if(!string.IsNullOrWhiteSpace(chromeExe)){
       if(comActivator!=TryActivateCom&&comActivator(senderId,launch))return true;
       if(LaunchChrome(chromeExe,profileDir,targetUrl))return true;
       if(comActivator==TryActivateCom&&comActivator(senderId,launch))return true;
       return false;
      }
     }else{
      if(comActivator(senderId,launch))return true;
     }
    }
    string exe=chromePathResolver();
    if(!string.IsNullOrWhiteSpace(exe))return LaunchChrome(exe,profileDir,targetUrl);
    if(!string.IsNullOrWhiteSpace(targetUrl))return processLauncher(targetUrl,null);
    return false;
   }
   if(!string.IsNullOrWhiteSpace(launch)&&LaunchUri(launch))return true;
   if(!string.IsNullOrWhiteSpace(launch)&&TryActivateAppCom(senderId,launch))return true;
   if(LaunchPackageApp(senderId,launch))return true;
   if(!string.IsNullOrWhiteSpace(targetUrl)&&processLauncher(targetUrl,null))return true;
   if(ActivateAppExecutable(senderId,notification.App))return true;
   return false;
  }
  static bool LaunchUri(string uriString){
   if(string.IsNullOrWhiteSpace(uriString))return false;
   Uri uri;
   if(Uri.TryCreate(uriString.Trim(),UriKind.Absolute,out uri)){
    string scheme=uri.Scheme.ToLowerInvariant();
    if(scheme=="http"||scheme=="https"||scheme=="mailto"||scheme.StartsWith("ms-")||scheme=="slack"||scheme=="discord"||scheme=="zoommtg"){
     try{
      var info=new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true};
      Process.Start(info);
      return true;
     }catch{return false;}
    }
   }
   return false;
  }
  static bool LaunchPackageApp(string appUserModelId,string launchArgs){
   if(string.IsNullOrWhiteSpace(appUserModelId))return false;
   if(appUserModelId.IndexOf('!')>0){
    try{
     var info=new ProcessStartInfo("explorer.exe","shell:AppsFolder\\"+appUserModelId){UseShellExecute=true};
     Process.Start(info);
     return true;
    }catch{return false;}
   }
   return false;
  }
  static bool TryActivateAppCom(string appUserModelId,string invokedArgs){
   if(string.IsNullOrWhiteSpace(appUserModelId))return false;
   var clsids=GetAppClsids(appUserModelId);
   if(clsids.Count==0)return false;
   int initialized=CoInitializeEx(IntPtr.Zero,0);
   if(initialized<0&&initialized!=unchecked((int)0x80010106))return false;
   try{
    foreach(Guid clsid in clsids){
     try{
      Guid currentClsid=clsid,iid=CallbackIid;
      INotificationActivationCallback callback;
      int hr=CoCreateInstance(ref currentClsid,IntPtr.Zero,CLSCTX_LOCAL_SERVER,ref iid,out callback);
      if(hr>=0&&callback!=null){
       try{
        callback.Activate(appUserModelId,invokedArgs??string.Empty,new NOTIFICATION_USER_INPUT_DATA[0],0);
        return true;
       }finally{
        if(Marshal.IsComObject(callback))Marshal.ReleaseComObject(callback);
       }
      }
     }catch{}
    }
    return false;
   }finally{if(initialized>=0)CoUninitialize();}
  }
  static List<Guid> GetAppClsids(string appUserModelId){
   var list=new List<Guid>();
   if(string.IsNullOrWhiteSpace(appUserModelId))return list;
   foreach(var root in new[]{Registry.CurrentUser,Registry.LocalMachine,Registry.ClassesRoot}){
    try{
     using(var key=root.OpenSubKey(@"Software\Classes\AppUserModelId\"+appUserModelId)){
      if(key!=null){
       Guid custom;
       if(Guid.TryParse(Convert.ToString(key.GetValue("CustomActivator")),out custom)&&!list.Contains(custom))list.Add(custom);
      }
     }
    }catch{}
    try{
     using(var key=root.OpenSubKey(@"AppUserModelId\"+appUserModelId)){
      if(key!=null){
       Guid custom;
       if(Guid.TryParse(Convert.ToString(key.GetValue("CustomActivator")),out custom)&&!list.Contains(custom))list.Add(custom);
      }
     }
    }catch{}
   }
   Guid direct;
   if(Guid.TryParse(appUserModelId,out direct)&&!list.Contains(direct))list.Add(direct);
   return list;
  }
  static bool ActivateAppExecutable(string senderId,string appName){
   string[] names=new[]{senderId,appName};
   foreach(string name in names){
    if(string.IsNullOrWhiteSpace(name))continue;
    string clean=name.Trim();
    try{
     var procs=Process.GetProcessesByName(clean);
     if(procs.Length>0){
      foreach(var p in procs){
       if(p.MainWindowHandle!=IntPtr.Zero){
        SetForegroundWindow(p.MainWindowHandle);
        return true;
       }
      }
     }
    }catch{}
    foreach(var root in new[]{Registry.CurrentUser,Registry.LocalMachine}){
     foreach(string sub in new[]{
      @"Software\Microsoft\Windows\CurrentVersion\App Paths\"+clean+".exe",
      @"Software\Microsoft\Windows\CurrentVersion\App Paths\"+clean
     }){
      try{
       using(var key=root.OpenSubKey(sub)){
        if(key!=null){
         string raw=Convert.ToString(key.GetValue(null));
         string exe=ExtractExePath(raw);
         if(!string.IsNullOrEmpty(exe)&&File.Exists(exe)){
          Process.Start(new ProcessStartInfo(exe){UseShellExecute=true});
          return true;
         }
        }
       }
      }catch{}
     }
    }
   }
   return false;
  }
 bool LaunchChrome(string exe,string profileDir,string url){
  var args=new List<string>();
  if(!string.IsNullOrWhiteSpace(profileDir)&&IsSafeToken(profileDir))args.Add("--profile-directory=\""+profileDir+"\"");
  if(!string.IsNullOrWhiteSpace(url))args.Add("\""+url+"\"");
  return processLauncher(exe,string.Join(" ",args.ToArray()));
 }
 static bool IsSafeToken(string value){
  foreach(char c in value)if(c=='"'||c=='\0'||c=='\r'||c=='\n')return false;
  return true;
 }
 internal static string ExtractProfileDirectory(string launch){
  if(string.IsNullOrWhiteSpace(launch))return null;
  string[] parts=launch.Split('|');
  // Chrome toast format: index 0=type, 1=notificationType, 2=profileId, 3=appUserModelId, 4=incognito, 5=origin, 6=notificationId
  if(parts.Length>=6&&!string.IsNullOrWhiteSpace(parts[2]))return parts[2].Trim();
  return null;
 }
 internal static string ExtractTargetUrl(string launch,string body){
  if(!string.IsNullOrWhiteSpace(launch)){
   Uri direct;
   if(Uri.TryCreate(launch.Trim(),UriKind.Absolute,out direct)&&(direct.Scheme=="http"||direct.Scheme=="https"))return direct.AbsoluteUri;
   string[] parts=launch.Split('|');
   if(parts.Length>=6){
    string origin=parts[5].Trim();
    Uri uri;
    if(Uri.TryCreate(origin,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https"))return uri.AbsoluteUri;
   }
  }
  if(!string.IsNullOrWhiteSpace(body)){
   foreach(string token in body.Split(new[]{' ','\t','\r','\n','　'},StringSplitOptions.RemoveEmptyEntries)){
    string candidate=token.Trim('\"','\'','(',')','[',']','<','>','「','」','『','』');
    Uri uri;
    if(Uri.TryCreate(candidate,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https"))return uri.AbsoluteUri;
   }
  }
  return null;
 }
 static bool IsChromeRunning(){
  try{return Process.GetProcessesByName("chrome").Length>0;}catch{return false;}
 }
 internal static void PrepareForegroundActivation(){
  try{AllowSetForegroundWindow(ASFW_ANY);}catch{}
 }
 static bool TryActivateCom(string appUserModelId,string invokedArgs){
  // Run COM activation with balanced apartment initialization.
  int initialized=CoInitializeEx(IntPtr.Zero,0);
  if(initialized<0&&initialized!=unchecked((int)0x80010106))return false;
  try{
   foreach(Guid clsid in FindChromeClsids(appUserModelId)){
    try{
     Guid currentClsid=clsid,iid=CallbackIid;
     INotificationActivationCallback callback;
     int hr=CoCreateInstance(ref currentClsid,IntPtr.Zero,CLSCTX_LOCAL_SERVER,ref iid,out callback);
     if(hr>=0&&callback!=null){
      try{
       callback.Activate(appUserModelId,invokedArgs,new NOTIFICATION_USER_INPUT_DATA[0],0);
       routes.Remember(appUserModelId,clsid);
       return true;
      }finally{
       if(Marshal.IsComObject(callback))Marshal.ReleaseComObject(callback);
      }
     }
    }catch{}
   }
   return false;
  }finally{if(initialized>=0)CoUninitialize();}
 }
 internal static IEnumerable<Guid> FindChromeClsids(string appUserModelId){
  return routes.Candidates(appUserModelId);
 }
 static IEnumerable<Guid> ReadRegisteredClsids(string appUserModelId){
  var list=new List<Guid>();
  if(!string.IsNullOrWhiteSpace(appUserModelId)){
   foreach(var root in new[]{Registry.CurrentUser,Registry.LocalMachine}){
    try{
     using(var key=root.OpenSubKey(@"Software\Classes\AppUserModelId\"+appUserModelId)){
      if(key!=null){
       Guid custom;
       if(Guid.TryParse(Convert.ToString(key.GetValue("CustomActivator")),out custom)&&!list.Contains(custom))list.Add(custom);
      }
     }
    }catch{}
   }
  }
  if(!list.Contains(DefaultChromeClsid))list.Add(DefaultChromeClsid);
  return list;
 }
 static IEnumerable<Guid> ScanChromeClsids(){
  // Instant resolution using known Chrome CLSIDs without multi-thousand registry scans.
  return KnownChromeClsids;
 }
 public static string FindChromeExecutable(){
  foreach(var root in new[]{Registry.CurrentUser,Registry.LocalMachine}){
   foreach(string subKey in new[]{
    @"Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
    @"Software\Clients\StartMenuInternet\Google Chrome\shell\open\command"
   }){
    try{
     using(var key=root.OpenSubKey(subKey)){
      if(key==null)continue;
      string raw=Convert.ToString(key.GetValue(null));
      string cleaned=ExtractExePath(raw);
      if(!string.IsNullOrEmpty(cleaned)&&File.Exists(cleaned))return cleaned;
     }
    }catch{}
   }
  }
  string[] candidates={
   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Google\Chrome\Application\chrome.exe"),
   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Google\Chrome\Application\chrome.exe"),
   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Google\Chrome\Application\chrome.exe")
  };
  foreach(string candidate in candidates)if(!string.IsNullOrEmpty(candidate)&&File.Exists(candidate))return candidate;
  return null;
 }
 internal static string ExtractExePath(string command){
  if(string.IsNullOrWhiteSpace(command))return null;
  string trimmed=command.Trim();
  if(trimmed.StartsWith("\"",StringComparison.Ordinal)){
   int end=trimmed.IndexOf('"',1);
   if(end>1)return trimmed.Substring(1,end-1);
  }
  int exeIndex=trimmed.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);
  return exeIndex>0?trimmed.Substring(0,exeIndex+4).Trim():trimmed;
 }
 static bool StartProcess(string fileName,string arguments){
  try{
   var info=new ProcessStartInfo(fileName){UseShellExecute=true};
   if(!string.IsNullOrWhiteSpace(arguments))info.Arguments=arguments;
   Process.Start(info);
   return true;
  }catch{return false;}
 }
}
// Cache identifiers, never COM objects: each worker owns and releases its callback.
internal sealed class ChromeActivationRoutes {
 sealed class Entry {public Guid[] Values;public DateTime Expires;}
 readonly object gate=new object();
 readonly Dictionary<string,Entry> senders=new Dictionary<string,Entry>(StringComparer.Ordinal);
 readonly Func<string,IEnumerable<Guid>> registered;readonly Func<IEnumerable<Guid>> scan;readonly Func<DateTime> now;
 Entry fallback;
 internal ChromeActivationRoutes(Func<string,IEnumerable<Guid>> registered,Func<IEnumerable<Guid>> scan,Func<DateTime> now){this.registered=registered;this.scan=scan;this.now=now;}
 internal void Remember(string sender,Guid successful){
  lock(gate){
   Entry entry;if(!senders.TryGetValue(sender??string.Empty,out entry)||now()>=entry.Expires)return;
   var values=new List<Guid> {successful};foreach(Guid id in entry.Values)if(id!=successful)values.Add(id);
   entry.Values=values.ToArray();
  }
 }
 internal IEnumerable<Guid> Candidates(string sender){
  sender=sender??string.Empty;
  Guid[] preferred;
  lock(gate){
   Entry entry;DateTime time=now();
   if(!senders.TryGetValue(sender,out entry)||time>=entry.Expires){
    if(senders.Count>=64)senders.Clear();
    entry=new Entry {Values=new List<Guid>(registered(sender)).ToArray(),Expires=time.AddMinutes(5)};senders[sender]=entry;
   }
   preferred=entry.Values;
  }
  var seen=new HashSet<Guid>();
  foreach(Guid id in preferred)if(seen.Add(id))yield return id;
  Guid[] extra;
  lock(gate){
   DateTime time=now();
   if(fallback==null||time>=fallback.Expires)fallback=new Entry {Values=new List<Guid>(scan()).ToArray(),Expires=time.AddMinutes(5)};
   extra=fallback.Values;
  }
  foreach(Guid id in extra)if(seen.Add(id))yield return id;
 }
}
}
