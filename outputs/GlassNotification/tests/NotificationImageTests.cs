using System;
using System.IO;
using System.Threading;
namespace Glass {
public static partial class Tests {
 sealed class ImagePayloadFixture:INotificationPayloadSource {
  public string Xml;public int Reads,ReadThread;public bool IsAvailable {get{return true;}}public string FindPayload(uint id){Interlocked.Increment(ref Reads);ReadThread=Thread.CurrentThread.ManagedThreadId;return Xml;}
 }
 sealed class WaitingImagePayloadFixture:INotificationPayloadSource,IDisposable {
  public string Xml;int reads;public readonly ManualResetEvent Entered=new ManualResetEvent(false),Continue=new ManualResetEvent(false);
  public bool IsAvailable {get{return true;}}public string FindPayload(uint id){if(Interlocked.Increment(ref reads)==1){Entered.Set();Continue.WaitOne(3000);}return Xml;}
  public void Dispose(){Entered.Dispose();Continue.Dispose();}
 }
 static void NotificationImageTests(string folder){
  NotificationActivationRouteTests();
  string path=Path.Combine(folder,"toast.png");var fixture=new ImagePayloadFixture();var resolver=new NotificationImageResolver(fixture);var notice=new SystemNotification {Id=7};
  Check(!resolver.TryAttach(notice),"toast payload not yet committed is retried");
  fixture.Xml="<toast launch='0|0|Default|Chrome|0|https://x.com/|p#https://x.com/#1tweet-123'><visual><binding><image placement='appLogoOverride' hint-crop='circle' src='"+new Uri(path).AbsoluteUri+"'/><image placement='hero' src='"+path+"'/></binding></visual></toast>";
  Check(!resolver.TryAttach(notice),"image file arriving after toast payload is retried");
  File.WriteAllBytes(path,Convert.FromBase64String(TestIcon(60,100,180)));
  Check(resolver.TryAttach(notice)&&notice.LogoImage!=null&&notice.ContentImage!=null&&notice.LogoCircle,"toast images load from local paths and file URIs");
  Check(notice.LaunchArguments=="0|0|Default|Chrome|0|https://x.com/|p#https://x.com/#1tweet-123","toast launch arguments are extracted from payload XML");
  Check(notice.LogoImage.IsFrozen&&notice.ContentImage.PixelWidth<=256,"toast images are frozen and bounded");
  Check(NotificationImageResolver.ResolveLocalPath("https://example.com/image.png")==null&&NotificationImageResolver.ResolveLocalPath(@"\\server\share\image.png")==null,"toast resolver rejects remote paths");
  Check(NotificationImageResolver.ParseImages("<!DOCTYPE toast [<!ENTITY x SYSTEM 'file:///C:/test'>]><toast>&x;</toast>").Count==0,"toast parser does not expand external entities");
  fixture.Xml="<toast><visual/></toast>";Check(resolver.TryAttach(new SystemNotification()),"text only notifications are delivered without image delay");
  string comSender=null,comLaunch=null,procFile=null,procArgs=null;
  var comActivator=new ChromeNotificationActivator(
   delegate(string sender,string launch){comSender=sender;comLaunch=launch;return true;},
   delegate(string file,string args){procFile=file;procArgs=args;return true;},
   delegate{return @"C:\Program Files\Google\Chrome\Application\chrome.exe";});
  Check(comActivator.Activate(new SystemNotification {SenderId="Chrome",LaunchArguments="0|0|Default|Chrome|0|https://www.youtube.com/|id1"})&&comSender=="Chrome"&&comLaunch=="0|0|Default|Chrome|0|https://www.youtube.com/|id1","notification activator invokes Chrome COM activator with launch arguments");
  var fallbackActivator=new ChromeNotificationActivator(
   delegate{return false;},
   delegate(string file,string args){procFile=file;procArgs=args;return true;},
   delegate{return @"C:\Program Files\Google\Chrome\Application\chrome.exe";});
  Check(fallbackActivator.Activate(new SystemNotification {SenderId="Chrome",LaunchArguments="0|0|Default|Chrome|0|https://x.com/|id2"})&&procFile==@"C:\Program Files\Google\Chrome\Application\chrome.exe"&&procArgs=="--profile-directory=\"Default\" \"https://x.com/\"","notification activator falls back to launching chrome.exe with profile and origin URL when COM activation is unavailable");
  procFile=null;procArgs=null;
  Check(fallbackActivator.Activate(new SystemNotification {SenderId="Chrome",Body="配信が始まりました https://www.twitch.tv/example をチェック"})&&procFile==@"C:\Program Files\Google\Chrome\Application\chrome.exe"&&procArgs=="\"https://www.twitch.tv/example\"","notification activator opens URL found in body when toast has no launch payload");
  // A Chrome image may disappear while its notification waits behind an earlier card.
  File.WriteAllBytes(path,Convert.FromBase64String(TestIcon(60,100,180)));
  fixture.Xml="<toast launch='0|2|Default|Chrome|0|chrome-extension://ext/|notif1'><visual><binding><image placement='hero' src='"+path+"'/></binding></visual></toast>";
  using(var capture=new NotificationImageCapture(resolver)){
   var feed=new NotificationFeed(3);feed.Observe(new SystemNotification[0],delegate{return true;},capture.Begin);
   var arriving=new SystemNotification {Id=17};feed.Observe(new[]{arriving},delegate{return true;},capture.Begin);
   var ready=new SystemNotification {Id=17};
   Check(SpinWait.SpinUntil(delegate{return capture.TryApply(ready);},3000),"arrival image capture finishes in the background");
   File.Delete(path);
   var queued=feed.Take();capture.TryApply(queued);
   Check(queued.ContentImage!=null&&queued.ContentImage.IsFrozen&&queued.LaunchArguments=="0|2|Default|Chrome|0|chrome-extension://ext/|notif1","queued toast keeps an image and launch arguments captured before its temporary file disappears");
   Check(fixture.ReadThread!=Thread.CurrentThread.ManagedThreadId,"toast database and image reads stay off the UI thread");
   capture.Begin(queued);var repeated=new SystemNotification {Id=17};
   Check(capture.TryApply(repeated)&&repeated.ContentImage!=null,"the same pending notification reuses its completed capture");
   capture.Retain(feed.Pending,null);var removed=new SystemNotification {Id=17};
   Check(capture.TryApply(removed)&&removed.ContentImage==null,"delivered or removed notifications release captured images");
   Check(capture.TryApply(queued)&&queued.ContentImage!=null&&queued.LaunchArguments=="0|2|Default|Chrome|0|chrome-extension://ext/|notif1","already attached images and launch arguments on a notification are never overwritten with null after capture cleanup");
  }
  int arrivals=0;var incoming=new NotificationFeed(2);Action<SystemNotification> received=delegate{arrivals++;};
  incoming.Observe(new[]{new SystemNotification {Id=41}},delegate{return true;},received);
  Check(arrivals==0,"baseline notifications do not start image capture");
  incoming.Observe(new[]{new SystemNotification {Id=41},new SystemNotification {Id=42},new SystemNotification {Id=43}},delegate(SystemNotification n){return n.Id!=43;},received);
  incoming.Observe(new[]{new SystemNotification {Id=41},new SystemNotification {Id=42},new SystemNotification {Id=43}},delegate{return true;},received);
  Check(arrivals==1,"only genuinely new allowed notifications start image capture once");
  File.WriteAllBytes(path,Convert.FromBase64String(TestIcon(60,100,180)));
  using(var blocked=new WaitingImagePayloadFixture {Xml=fixture.Xml})using(var capture=new NotificationImageCapture(new NotificationImageResolver(blocked))){
   capture.Begin(new SystemNotification {Id=51});
   Check(blocked.Entered.WaitOne(3000),"a capture can be canceled while its payload read is in flight");
   capture.Retain(new SystemNotification[0],null);capture.Begin(new SystemNotification {Id=52});blocked.Continue.Set();
   var next=new SystemNotification {Id=52};Check(SpinWait.SpinUntil(delegate{return capture.TryApply(next);},3000)&&next.ContentImage!=null,"later captures continue after a pending notification is filtered or removed");
   var removed=new SystemNotification {Id=51};Check(capture.TryApply(removed)&&removed.ContentImage==null,"an in-flight removed capture cannot restore discarded images");
   capture.Dispose();var stopped=new SystemNotification {Id=52};Check(capture.TryApply(stopped)&&stopped.ContentImage==null,"stopping capture releases completed image results");
  }
  using(var capture=new NotificationImageCapture(resolver)){
   var waiting=new SystemNotification {Id=61};capture.Begin(waiting);var ready=new SystemNotification {Id=61};
   Check(SpinWait.SpinUntil(delegate{return capture.TryApply(ready);},3000)&&ready.ContentImage!=null,"waiting card has a captured image before a queue burst");
   for(uint id=62;id<92;id++)capture.Begin(new SystemNotification {Id=id});
   var replacement=new System.Collections.Generic.List<SystemNotification>();for(uint id=100;id<130;id++)replacement.Add(new SystemNotification {Id=id});
   capture.Begin(replacement,replacement,waiting);
   var delivery=new SystemNotification {Id=61};capture.TryApply(delivery);
   Check(delivery.ContentImage!=null,"replacing a full queue retains the active waiting card image");
  }
  string resourceDir=Path.Combine(folder,"Notification Resources");Directory.CreateDirectory(resourceDir);
  string tmpPath=Path.Combine(resourceDir,"chrome-fast-delete.tmp");
  File.WriteAllBytes(tmpPath,Convert.FromBase64String(TestIcon(180,80,40)));
  NotificationResourceCache.CaptureFile(tmpPath);
  File.Delete(tmpPath);
  fixture.Xml="<toast><visual><binding><image placement='appLogoOverride' hint-crop='circle' src='"+tmpPath+"'/></binding></visual></toast>";
  var cachedNotice=new SystemNotification {Id=95};
  Check(resolver.TryAttach(cachedNotice)&&cachedNotice.LogoImage!=null&&cachedNotice.LogoCircle,"notification resource cache recovers Chrome temporary image even after the .tmp file is deleted from disk");
 }
}}
