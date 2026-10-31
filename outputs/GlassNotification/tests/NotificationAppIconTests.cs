using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Glass {
public static partial class Tests {
 static string TestIcon(byte red,byte green,byte blue){var pixels=new byte[16*16*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=blue;pixels[i+1]=green;pixels[i+2]=red;pixels[i+3]=255;}var bitmap=BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,pixels,64);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){encoder.Save(stream);return Convert.ToBase64String(stream.ToArray());}}
 static void NotificationAppIconTests(){
  var red=NotificationAppIcons.Decode(TestIcon(220,40,60));var green=NotificationAppIcons.Decode(TestIcon(20,180,90));
  Check(red!=null&&red.IsFrozen&&red.PixelWidth<=64,"OS app logo bytes decode to a bounded frozen image");
  Check(NotificationAppIcons.Decode("not base64")==null&&NotificationAppIcons.Decode(Convert.ToBase64String(new byte[]{1,2,3}))==null,"unavailable or corrupt app logos safely use fallback");
  Check(NotificationAppIcons.Decode(new string('A',100000))==null,"oversized logo data is rejected before image decoding");
  var cache=new NotificationAppIcons();var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
  var first=SystemNotificationSource.DecodeSnapshot(serializer.Serialize(new {Icons=new System.Collections.Generic.Dictionary<string,string> {{"App.A",TestIcon(220,40,60)},{"App.B",TestIcon(20,180,90)}},Notifications=new[]{new {Id=1,AppId="App.A",App="A",Title="A",Body="本文",Ticks=0.0}}}),cache);
  Check(first.Count==1&&ReferenceEquals(first[0].AppIcon,cache.Get("App.A")),"OS snapshot associates app logo with exact sender identity");
  var second=SystemNotificationSource.DecodeSnapshot("{\"Icons\":{},\"Notifications\":[{\"Id\":2,\"AppId\":\"App.B\",\"App\":\"B\"},{\"Id\":3,\"AppId\":\"App.Missing\"}]}",cache);
  Check(ReferenceEquals(second[0].AppIcon,cache.Get("App.B"))&&second[1].AppIcon==null,"subsequent notifications reuse their sender's cached logo and never another sender's logo");
  var preserved=SystemNotificationSource.DecodeSnapshot("{\"Icons\":{\"App.B\":\"\"},\"Notifications\":[{\"Id\":4,\"AppId\":\"App.B\",\"App\":\"B\"}]}",cache);
  Check(preserved[0].AppIcon!=null&&ReferenceEquals(preserved[0].AppIcon,cache.Get("App.B")),"cached sender logo is preserved when a later snapshot reports an empty logo stream");
  var manyIcons=new System.Collections.Generic.Dictionary<string,string>();var keys=new System.Collections.Generic.List<string>();string logo=TestIcon(220,40,60);for(int i=0;i<64;i++){string key="Sender"+i;manyIcons[key]=logo;keys.Add(key);}
  SystemNotificationSource.DecodeSnapshot(serializer.Serialize(new {Icons=manyIcons,IconKeys=keys,Notifications=new object[0]}),cache);keys.Remove("Sender31");keys.Add("NewSender");
  SystemNotificationSource.DecodeSnapshot(serializer.Serialize(new {Icons=new System.Collections.Generic.Dictionary<string,string> {{"NewSender",logo}},IconKeys=keys,Notifications=new object[0]}),cache);
  Check(cache.Get("Sender0")!=null&&cache.Get("Sender31")==null&&cache.Get("NewSender")!=null,"managed logo cache follows native active keys instead of independently evicting a still-active sender");
  NotificationSelectedImageTests();
 }
 static void NotificationSelectedImageTests(){
  var content=NotificationAppIcons.Decode(TestIcon(220,40,60));var sender=NotificationAppIcons.Decode(TestIcon(20,180,90));var app=NotificationAppIcons.Decode(TestIcon(40,80,220));
  var card=new NotificationWindow();
  try {
   var monitor=new MonitorInfo {WorkAreaPx=new Rect(0,0,1920,1080),DpiScale=1};
   card.Apply(new AppSettings {ImageSize=96,Mode=BackdropMode.Off},monitor);
   var grid=(Grid)((Border)card.Content).Child;var icon=(Border)grid.Children[0];Image image=null;foreach(var child in grid.Children)if(child is Image)image=(Image)child;
   card.SetNotification(new SystemNotification {App="App fallback",AppIcon=app,LogoCircle=true});
   Check(ReferenceEquals(image.Source,app)&&image.Visibility==Visibility.Visible&&image.Clip==null&&image.Stretch==Stretch.Uniform,"missing sender/content images enlarge the app icon without a circular crop");
   var logical=card.Apply(new AppSettings {ImageSize=96,Mode=BackdropMode.Off},monitor);
   Check(card.Height==120&&logical.Height==88,"an enlarged app fallback also receives temporary card height without persisting the expansion");
   Check(icon.Visibility==Visibility.Collapsed,"upper-left small icon stays collapsed when the enlarged app icon is present");
   card.SetNotification(new SystemNotification {AppIcon=app,LogoImage=sender,LogoCircle=true});
   Check(ReferenceEquals(image.Source,sender)&&image.Clip is EllipseGeometry&&image.Stretch==Stretch.UniformToFill,"sender logo takes priority over app icon and preserves its circular crop");
   var circle=(EllipseGeometry)image.Clip;Check(circle.Center==new Point(48,48)&&circle.RadiusX==48&&circle.RadiusY==48,"sender logo circle scales to the enlarged image size");
   card.Apply(new AppSettings {ImageSize=128,Mode=BackdropMode.Off},monitor);
   circle=(EllipseGeometry)image.Clip;Check(circle.Center==new Point(64,64)&&circle.RadiusX==64&&card.Height==152,"sender circle and temporary card height follow a changed image size");
   card.SetNotification(new SystemNotification {AppIcon=app,LogoImage=sender,ContentImage=content,LogoCircle=true});
   Check(ReferenceEquals(image.Source,content)&&image.Clip==null&&image.Stretch==Stretch.Uniform,"content image takes priority over sender/app images and clears the sender crop");
   card.SetNotification(new SystemNotification {LogoImage=sender,LogoCircle=false});
   Check(ReferenceEquals(image.Source,sender)&&image.Clip==null&&image.Stretch==Stretch.Uniform,"a noncircular sender logo remains uncropped");
   card.SetNotification(new SystemNotification {LogoImage=sender,LogoCircle=true});
   card.SetNotification(new SystemNotification {AppIcon=app});
   Check(ReferenceEquals(image.Source,app)&&image.Clip==null&&image.Stretch==Stretch.Uniform,"next app fallback clears a previous sender's circular crop");
   card.SetNotification(new SystemNotification {App="No images"});
   Check(image.Source==null&&image.Visibility==Visibility.Collapsed&&image.Clip==null&&icon.Visibility==Visibility.Collapsed,"an image-free notification clears the enlarged image and keeps the small icon hidden");
   card.SetNotification(new SystemNotification {LogoImage=sender,LogoCircle=true});card.SetNotification(null);
   Check(image.Source==null&&image.Visibility==Visibility.Collapsed&&image.Clip==null&&icon.Visibility==Visibility.Collapsed,"sample notification clears previous image source and crop with no small fallback icon");
  } finally {card.Close();}
 }
}
}
