using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
namespace Glass {
// Only image bytes supplied by Windows AppInfo are decoded. No file paths or URLs.
public sealed class NotificationAppIcons {
 readonly Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>(StringComparer.Ordinal);readonly Queue<string> order=new Queue<string>();
 static BitmapSource chromeFallbackIcon;static bool chromeFallbackResolved;
 public void Apply(IDictionary<string,string> values,IEnumerable<string> activeKeys){
  HashSet<string> active=null;if(activeKeys!=null){active=new HashSet<string>(activeKeys,StringComparer.Ordinal);if(active.Count>64)throw new InvalidDataException("通知アイコンの取得件数が上限を超えています。");foreach(var key in new List<string>(images.Keys))if(!active.Contains(key))images.Remove(key);order.Clear();foreach(var key in images.Keys)order.Enqueue(key);}
  if(values==null)return;
  foreach(var pair in values){
   if(string.IsNullOrEmpty(pair.Key)||pair.Key.Length>1024||(active!=null&&!active.Contains(pair.Key)))continue;
   var decoded=Decode(pair.Value);
   BitmapSource existing;
   if(images.TryGetValue(pair.Key,out existing)){
    if(decoded!=null)images[pair.Key]=decoded;
   }else{
    if(images.Count>=64)images.Remove(order.Dequeue());
    order.Enqueue(pair.Key);
    images[pair.Key]=decoded;
   }
  }
 }
 public BitmapSource Get(string appId){
  if(appId==null)return null;
  BitmapSource image;
  if(images.TryGetValue(appId,out image)&&image!=null)return image;
  if(IsChromeSender(appId))return GetChromeFallbackIcon();
  return null;
 }
 static bool IsChromeSender(string appId){
  return string.Equals(appId,"Chrome",StringComparison.OrdinalIgnoreCase)
   ||appId.StartsWith("Chrome.",StringComparison.OrdinalIgnoreCase)
   ||appId.StartsWith("Google.Chrome",StringComparison.OrdinalIgnoreCase);
 }
 static BitmapSource GetChromeFallbackIcon(){
  if(chromeFallbackResolved)return chromeFallbackIcon;
  chromeFallbackResolved=true;
  try{
   string exe=ChromeNotificationActivator.FindChromeExecutable();
   if(!string.IsNullOrEmpty(exe)&&File.Exists(exe)){
    using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(exe))
    using(var bmp=icon==null?null:icon.ToBitmap())
    using(var stream=new MemoryStream()){
     if(bmp!=null){
      bmp.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
      chromeFallbackIcon=Decode(Convert.ToBase64String(stream.ToArray()));
     }
    }
   }
  }catch(Exception){}
  return chromeFallbackIcon;
 }
 public static BitmapSource Decode(string encoded){
  if(string.IsNullOrEmpty(encoded)||encoded.Length>87384)return null;
  try {var bytes=Convert.FromBase64String(encoded);if(bytes.Length>65536)return null;
   using(var stream=new MemoryStream(bytes,false)){
    var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);if(decoder.Frames.Count==0)return null;var frame=decoder.Frames[0];int width=frame.PixelWidth,height=frame.PixelHeight;if(width<1||height<1||width>2048||height>2048)return null;
    double scale=Math.Min(1,64.0/Math.Max(width,height));stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
    if(width>=height)image.DecodePixelWidth=Math.Max(1,(int)Math.Round(width*scale));
    else image.DecodePixelHeight=Math.Max(1,(int)Math.Round(height*scale));
    image.StreamSource=stream;image.EndInit();image.Freeze();return image;
   }
  }catch(FormatException){return null;}catch(NotSupportedException){return null;}catch(IOException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
 }
}
}
