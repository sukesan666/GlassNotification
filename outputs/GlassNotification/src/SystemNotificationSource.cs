using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
namespace Glass {
public sealed class SystemNotificationSource : IDisposable {
 IntPtr context;bool disposed;
 readonly NotificationAppIcons icons=new NotificationAppIcons();
 readonly NotificationImageCapture images=new NotificationImageCapture(new NotificationImageResolver(new WpnDatabasePayloadSource()));
 internal void CaptureImages(IEnumerable<SystemNotification> arriving,IEnumerable<SystemNotification> pending,SystemNotification waiting){images.Begin(arriving,pending,waiting);}
 internal void RetainImages(IEnumerable<SystemNotification> pending,SystemNotification waiting){images.Retain(pending,waiting);}
 internal bool TryAttachImages(SystemNotification notification){
  if(notification!=null&&notification.AppIcon==null)notification.AppIcon=icons.Get(notification.SenderId);
  return images.TryApply(notification);
 }
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsCreate(out IntPtr context);
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsAccess(IntPtr context,int request,out int status);
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsRead(IntPtr context,out int ready,out IntPtr json);
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsDestroy(IntPtr context);
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsSendTest();
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsPackageInfo(out IntPtr text);
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall)]static extern int NotificationsRegisteredAppId(out IntPtr text);
 [DllImport("GlassNotifications.dll",CallingConvention=CallingConvention.StdCall,CharSet=CharSet.Unicode)]static extern int NotificationsRegister(string manifest,string folder,out int ready);
 internal const string PackageFileName="GlassNotification.OSLink.msix";
 // Prefer the signed package (no developer mode needed); fall back to the loose manifest, which does need developer mode.
 public static bool Register(){
  if(PackageRuntime.Current.IsInstalledPackage)return true;
  int ready;string folder=AppDomain.CurrentDomain.BaseDirectory;string package=System.IO.Path.Combine(folder,PackageFileName);
  string source=System.IO.File.Exists(package)?package:System.IO.Path.Combine(folder,"identity/AppxManifest.xml");
  int hr=NotificationsRegister(new Uri(source).AbsoluteUri,new Uri(folder).AbsoluteUri,out ready);
  if(hr==unchecked((int)0x800B0109)||hr==unchecked((int)0x800B010A))throw new InvalidOperationException("連動版の署名証明書がこのPCで信頼されていません。identity\\trust-certificate.ps1 を管理者権限で一度だけ実行してください。");
  if(hr==unchecked((int)0x80073CFF))throw new InvalidOperationException("このPCでは未署名アプリを登録できません。署名済みパッケージ（GlassNotification.OSLink.msix）を配置するか、Windowsの開発者モードをONにしてください。");
  Marshal.ThrowExceptionForHR(hr);return ready!=0;
 }
 public static string InstalledPath(){IntPtr text;Marshal.ThrowExceptionForHR(NotificationsPackageInfo(out text));return Marshal.PtrToStringUni(text);}
 public static string RegisteredAppId(){IntPtr text;Marshal.ThrowExceptionForHR(NotificationsRegisteredAppId(out text));return Marshal.PtrToStringUni(text)??"";}
 public void Open(){if(disposed)throw new ObjectDisposedException("SystemNotificationSource");if(context!=IntPtr.Zero)return;int hr=NotificationsCreate(out context);if(hr==unchecked((int)0x80073D54))throw new InvalidOperationException("Chromeの通知との連動にはWindowsへのアプリ登録が必要です。「Chromeに登録して連動版を開く」から連動版を起動してください。");Marshal.ThrowExceptionForHR(hr);}
 public int Access(bool request){Open();int status;Marshal.ThrowExceptionForHR(NotificationsAccess(context,request?1:0,out status));return status;}
 public IList<SystemNotification> Read(){Open();int ready;IntPtr json;Marshal.ThrowExceptionForHR(NotificationsRead(context,out ready,out json));if(ready==0)return null;
  return DecodeSnapshot(Marshal.PtrToStringUni(json),icons);
 }
 internal static IList<SystemNotification> DecodeSnapshot(string json,NotificationAppIcons cache){var snapshot=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.Deserialize<NativeSnapshot>(json);cache.Apply(snapshot.Icons,snapshot.IconKeys);var result=new List<SystemNotification>();
  foreach(var n in snapshot.Notifications){DateTimeOffset created;try{created=DateTimeOffset.FromFileTime((long)n.Ticks);}catch(ArgumentOutOfRangeException){created=DateTimeOffset.UtcNow;}result.Add(new SystemNotification {Id=n.Id,SenderId=Trim(n.AppId,1024),App=Trim(n.App,256),Title=Trim(n.Title,512),Body=Trim(n.Body,4096),Created=created,AppIcon=cache.Get(n.AppId)});}return result;
 }
 static string Trim(string value,int limit){value=value??"";return value.Length<=limit?value:value.Substring(0,limit);}
 public static void SendTest(){Marshal.ThrowExceptionForHR(NotificationsSendTest());}
 public void Dispose(){if(disposed)return;disposed=true;images.Dispose();if(context!=IntPtr.Zero){NotificationsDestroy(context);context=IntPtr.Zero;}}
 sealed class NativeSnapshot {public List<NativeNotice> Notifications {get;set;}public Dictionary<string,string> Icons {get;set;}public List<string> IconKeys {get;set;}}
 sealed class NativeNotice {public uint Id {get;set;}public string AppId {get;set;}public string App {get;set;}public string Title {get;set;}public string Body {get;set;}public double Ticks {get;set;}}
}
}
