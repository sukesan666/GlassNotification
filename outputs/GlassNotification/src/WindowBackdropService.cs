using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
namespace Glass {
public interface IBackdropNative {
 string UnavailableReason();
 int SetType(IntPtr hwnd,int type);
 int ExtendFrame(IntPtr hwnd,bool enabled);
}
public interface IBackdropStrengthNative {
 int SetStrength(BackdropStrength strength);
}
public sealed class DwmBackdropNative : IBackdropNative {
 [StructLayout(LayoutKind.Sequential)] struct MARGINS {public int Left,Right,Top,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct POWER {public byte AC,Battery,Percent,Saver;public uint Life,FullLife;}
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref MARGINS margins);
 [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out POWER status);
 public string UnavailableReason() {
  try {
   using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
    int build;if(key==null||!int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build)||build<22621)
     return "Windows 11 22H2以降が必要です";
   }
   if(SystemParameters.HighContrast)return "ハイコントラストが有効です";
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
    if(key!=null&&Convert.ToInt32(key.GetValue("EnableTransparency",1))==0)return "Windowsの透明効果がOFFです";
   POWER power;if(GetSystemPowerStatus(out power)&&power.Saver==1)return "省電力モードが有効です";
   return null;
  }catch(SecurityException){return "Windowsの表示設定を確認できません";}
   catch(UnauthorizedAccessException){return "Windowsの表示設定を確認できません";}
   catch(FormatException){return "Windowsの表示設定を確認できません";}
   catch(InvalidCastException){return "Windowsの表示設定を確認できません";}
 }
 public int SetType(IntPtr hwnd,int type){return DwmSetWindowAttribute(hwnd,38,ref type,4);}
 public int ExtendFrame(IntPtr hwnd,bool enabled) {
  var margins=enabled?new MARGINS {Left=-1,Right=-1,Top=-1,Bottom=-1}:new MARGINS();
  return DwmExtendFrameIntoClientArea(hwnd,ref margins);
 }
}
// Owns only this Window's backgrounds and DWM backdrop/frame extension.
// An accepted request is not proof of visible blur: Windows may render fallback material.
public sealed class WindowBackdropService : IDisposable {
 readonly Window window;readonly DependencyObject root;readonly DependencyProperty rootBackground;
 readonly IBackdropNative native;readonly Brush originalWindow,originalRoot;
 readonly Brush materialTint=new SolidColorBrush(Color.FromArgb(100,245,247,250));
 Brush selectedTint,selectedOpaque;string selectedColor=NotificationColors.Default;
 HwndSource source;IntPtr handle;Color originalTarget;DispatcherOperation pending;bool disposed;
 public BackdropMode RequestedMode {get;private set;}
 public BackdropStrength Strength {get;private set;}
 public BackdropMode AppliedMode {get;private set;}
 public string Status {get;private set;}
 public int LastHResult {get;private set;}
 public event EventHandler Changed;
 public WindowBackdropService(Window window,DependencyObject root,DependencyProperty background):this(window,root,background,new SdkBackdropNative()){}
 public WindowBackdropService(Window window,DependencyObject root,DependencyProperty background,IBackdropNative native) {
  this.window=window;this.root=root;rootBackground=background;this.native=native;Strength=BackdropStrength.Standard;
  originalWindow=window.Background;originalRoot=(Brush)root.GetValue(background);Status="表示待ち";
  window.SourceInitialized+=SourceInitialized;window.ContentRendered+=ContentRendered;
  window.StateChanged+=StateChanged;window.Closed+=Closed;
  SystemEvents.UserPreferenceChanged+=PreferenceChanged;SystemEvents.PowerModeChanged+=PowerChanged;
  SystemParameters.StaticPropertyChanged+=SystemPropertyChanged;
 }
 public void SetMode(BackdropMode mode) {
  if(disposed)return;
  if(!Enum.IsDefined(typeof(BackdropMode),mode))mode=BackdropMode.Off;
  if(RequestedMode==mode&&handle!=IntPtr.Zero)return;
  RequestedMode=mode;Refresh();
 }
 public void SetStrength(BackdropStrength strength){
  if(disposed)return;
  strength=Enum.IsDefined(typeof(BackdropStrength),strength)?strength:BackdropStrength.Standard;
  if(Strength==strength)return;
  Strength=strength;
  if(handle!=IntPtr.Zero)Refresh();
 }
 public void SetCardColor(string value){
  value=NotificationColors.Normalize(value);if(disposed||value==selectedColor)return;
  selectedColor=value;
  if(value==NotificationColors.Default){selectedTint=null;selectedOpaque=null;}
  else {var color=NotificationColors.Color(value,Colors.White);selectedOpaque=new SolidColorBrush(color);color.A=100;selectedTint=new SolidColorBrush(color);}
  if(handle==IntPtr.Zero){root.SetCurrentValue(rootBackground,selectedOpaque??originalRoot);return;}
  Refresh();
 }
 void SourceInitialized(object sender,EventArgs args) {
  if(disposed)return;
  DetachSource();handle=new WindowInteropHelper(window).Handle;source=HwndSource.FromHwnd(handle);
  if(source==null){handle=IntPtr.Zero;return;}
  originalTarget=source.CompositionTarget.BackgroundColor;source.AddHook(Hook);source.Disposed+=SourceDisposed;Refresh();
 }
 void SourceDisposed(object sender,EventArgs args){var owned=native as IDisposable;if(owned!=null)owned.Dispose();DetachSource();CancelPending();RestoreBackgrounds();}
 void DetachSource() {
  if(source!=null){source.Disposed-=SourceDisposed;source.RemoveHook(Hook);source=null;}
  handle=IntPtr.Zero;
 }
 void RestoreBackgrounds() {
  window.SetCurrentValue(Window.BackgroundProperty,originalWindow);root.SetCurrentValue(rootBackground,selectedOpaque??originalRoot);
  if(source!=null&&!source.IsDisposed&&source.CompositionTarget!=null)source.CompositionTarget.BackgroundColor=originalTarget;
 }
 void ResetNative() {
  if(handle==IntPtr.Zero)return;
  native.SetType(handle,1);native.ExtendFrame(handle,false);
 }
 public void Refresh() {
  if(disposed||handle==IntPtr.Zero||window.WindowState==WindowState.Minimized)return;
  try {
   string reason=RequestedMode==BackdropMode.Off?null:native.UnavailableReason();
   if(RequestedMode==BackdropMode.Off||reason!=null) {
    ResetNative();RestoreBackgrounds();AppliedMode=BackdropMode.Off;LastHResult=0;
    Status=reason==null?"OFF（不透明）":"不透明："+reason;
   }else {
    LastHResult=native.ExtendFrame(handle,true);
    if(LastHResult>=0)LastHResult=native.SetType(handle,RequestedMode==BackdropMode.Acrylic?3:2);
    if(LastHResult>=0){var colored=native as IBackdropColorNative;if(colored!=null)LastHResult=colored.SetColor(selectedColor);}
    if(LastHResult>=0&&RequestedMode==BackdropMode.Acrylic){
     var adjustable=native as IBackdropStrengthNative;if(adjustable!=null)LastHResult=adjustable.SetStrength(Strength);
    }
    if(LastHResult<0) {
     ResetNative();RestoreBackgrounds();AppliedMode=BackdropMode.Off;
     Status="不透明：背景効果を利用できません（0x"+LastHResult.ToString("X8")+"）";
    }else {
     window.SetCurrentValue(Window.BackgroundProperty,Brushes.Transparent);
     // Acrylic already owns the tint/luminosity layers. This same WPF visual is
     // also uploaded as native foreground: tinting it would mask the material
     // and apply the background color again when capturing notification content.
     var content=native as IBackdropContentNative;
     root.SetCurrentValue(rootBackground,RequestedMode==BackdropMode.Acrylic&&content!=null&&content.HasContentTarget?Brushes.Transparent:selectedTint??materialTint);
     source.CompositionTarget.BackgroundColor=Colors.Transparent;AppliedMode=RequestedMode;
     Status=RequestedMode+" 設定済み（見え方はWindowsが調整）";
    }
   }
  }catch(DllNotFoundException){Unavailable();}catch(EntryPointNotFoundException){Unavailable();}
  if(Changed!=null)Changed(this,EventArgs.Empty);
 }
 void Unavailable(){RestoreBackgrounds();AppliedMode=BackdropMode.Off;LastHResult=unchecked((int)0x80004001);Status="不透明：背景効果APIを利用できません";}
 public void QueueRefresh() {
  if(disposed||window.Dispatcher.HasShutdownStarted)return;
  if(!window.Dispatcher.CheckAccess()){window.Dispatcher.BeginInvoke(new Action(QueueRefresh));return;}
  if(pending!=null&&pending.Status==DispatcherOperationStatus.Pending)return;
  pending=window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate {pending=null;if(!disposed)Refresh();}));
 }
 public void PresentContent(FrameworkElement visual) {
  var content=native as IBackdropContentNative;
  if(disposed||content==null||!content.HasContentTarget||AppliedMode==BackdropMode.Off||visual.ActualWidth<=0||visual.ActualHeight<=0)return;
  var transform=source.CompositionTarget.TransformToDevice;
  int width=(int)Math.Ceiling(visual.ActualWidth*transform.M11),height=(int)Math.Ceiling(visual.ActualHeight*transform.M22);
  double opacity=visual.Opacity;
  try {
   visual.Opacity=1;
   var bitmap=new RenderTargetBitmap(width,height,96*transform.M11,96*transform.M22,PixelFormats.Pbgra32);
   bitmap.Render(visual);int stride=checked(width*4);var pixels=new byte[checked(stride*height)];bitmap.CopyPixels(pixels,stride,0);
   int hr=content.Present(width,height,stride,pixels);
   if(hr<0)ContentFallback(hr);
   else content.SetOpacity(opacity);
  }finally{visual.Opacity=opacity;}
 }
 public void SetContentOpacity(double opacity){var content=native as IBackdropContentNative;if(content!=null&&content.HasContentTarget)content.SetOpacity(opacity);}
 public void CheckGraphicsDevice(){var content=native as IBackdropContentNative;if(!disposed&&content!=null&&content.HasContentTarget){int hr=content.DeviceStatus;if(hr<0)ContentFallback(hr);}}
 void ContentFallback(int hr){
  ResetNative();RestoreBackgrounds();AppliedMode=BackdropMode.Off;LastHResult=hr;Status="不透明：通知の文字を描画できません（0x"+hr.ToString("X8")+"）";
  if(Changed!=null)Changed(this,EventArgs.Empty);
 }
 void CancelPending(){if(pending!=null){pending.Abort();pending=null;}}
 IntPtr Hook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) {
  if(message==0x31E||message==0x320||message==0x1A||message==0x15||message==0x2E0||message==0x218)QueueRefresh();
  return IntPtr.Zero;
 }
 void ContentRendered(object sender,EventArgs args){QueueRefresh();}
 void StateChanged(object sender,EventArgs args){if(window.WindowState!=WindowState.Minimized)QueueRefresh();}
 void PreferenceChanged(object sender,UserPreferenceChangedEventArgs args){QueueRefresh();}
 void PowerChanged(object sender,PowerModeChangedEventArgs args){QueueRefresh();}
 void SystemPropertyChanged(object sender,PropertyChangedEventArgs args){if(args.PropertyName=="HighContrast")QueueRefresh();}
 void Closed(object sender,EventArgs args){Dispose();}
 public void Dispose() {
  if(disposed)return;disposed=true;CancelPending();
  SystemEvents.UserPreferenceChanged-=PreferenceChanged;SystemEvents.PowerModeChanged-=PowerChanged;SystemParameters.StaticPropertyChanged-=SystemPropertyChanged;
  window.SourceInitialized-=SourceInitialized;window.ContentRendered-=ContentRendered;window.StateChanged-=StateChanged;window.Closed-=Closed;
  // Closed may arrive after the HWND is destroyed. Do not issue calls to a stale handle.
  var owned=native as IDisposable;if(owned!=null)owned.Dispose();
  RestoreBackgrounds();DetachSource();Changed=null;
 }
}
}
