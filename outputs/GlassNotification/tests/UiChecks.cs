using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Glass {
public static partial class Tests {
 static partial void Ui() {
  BackdropServiceTests();
  BackdropContentTests();
  NotificationColorTests();
  NotificationTextOutlineTests();
  NotificationInteractionTests();
  NotificationAppIconTests();
  NotificationAppearanceTests();
  var monitors=NativeWindow.GetMonitors(); Check(monitors.Length>0,"monitor discovery");
  var sample=new MonitorInfo {WorkAreaPx=new Rect(-1920,0,1920,1080),DpiScale=1.5};
  Check(sample.ToPixels(new Rect(20,20,360,88))==new Rect(-1890,30,540,132),"negative monitor coordinates at 150%");
  sample.DpiScale=2; Check(sample.ToPixels(new Rect(20,20,360,88)).Width==720,"200% conversion");
  sample.DpiScale=1; Check(sample.ToPixels(new Rect(20,20,360,88)).Width==360,"100% conversion");
  var w=new NotificationWindow(); var s=new AppSettings {Width=280,FontSize=24};
  s=w.Apply(s,monitors[0]); Check(w.MeasuredMinimumHeight<=w.Height,"large font minimum height");
  w.SetSampleText("長い日本語の本文でも、文字とアイコンが重ならず、行の上下が欠けないように表示を確認します。さらに長い本文を追加します。");
  w.Apply(s,monitors[0]);Check(w.MeasuredMinimumHeight<=w.Height,"long Japanese minimum height");
  Check(s.Height>88,"large font expands height");
  Check(w.BodyLines<=2&&w.BodyLines>=1,"body line count");
  s.Margin=0; w.Apply(s,monitors[0]); Check(Math.Abs(w.EntryOffset)>w.Width,"zero margin still enters from outside the screen");
  w.SetSampleText("ちょっと気分のいい通知を、ここに。");w.Apply(new AppSettings(),monitors[0]);
  IntPtr before=NativeWindow.GetForegroundWindow(); w.ShowCard();
  Check(!w.BackdropEnabled||(GetWindowLong(new WindowInteropHelper(w).Handle,-20)&0x80000)==0,"glass entrance must not switch the native window to layered rendering");
  Check(!w.BackdropEnabled||((System.Windows.Controls.Border)w.Content).Opacity==1,"slide entrance keeps content fully opaque");
  Pump(720);
  Check(NativeWindow.GetForegroundWindow()==before,"notification does not steal focus");
  var bounds=NativeWindow.GetBounds(new WindowInteropHelper(w).Handle);
  Check(monitors[0].WorkAreaPx.Contains(bounds),"notification stays in work area");
  w.SavePreview(Path.Combine(Root,"preview-card.png"));
  w.Apply(new AppSettings {GlassEffectEnabled=false},monitors[0]);
  Check(!w.BackdropEnabled,"glass off disables native backdrop on visible card");
  Check(((SolidColorBrush)((System.Windows.Controls.Border)w.Content).Background).Color.A==255,"glass off background fully opaque");
  w.SavePreview(Path.Combine(Root,"preview-glass-off.png"));
  w.Apply(new AppSettings {GlassEffectEnabled=true},monitors[0]);
  Console.WriteLine("GLASS: native backdrop active = "+w.BackdropEnabled);
  Check(((SolidColorBrush)((System.Windows.Controls.Border)w.Content).Background).Color.A<255==w.BackdropEnabled,"glass surface leaves compositor visible; fallback stays opaque");
  w.ForceFallback=true; w.Apply(new AppSettings(),monitors[0]);
  Check(!w.BackdropEnabled,"fallback selectable"); w.SavePreview(Path.Combine(Root,"preview-fallback.png"));
  Console.WriteLine("CARD: native background requested; fallback path verified");
  w.ForceFallback=false;w.Apply(new AppSettings {Mode=BackdropMode.Off},monitors[0]);w.ShowCard();Pump(250);w.FadeOut(delegate{});Pump(400);
  Check(!w.IsVisible,"opaque exit slide hides notification");
  w.Apply(new AppSettings {Mode=BackdropMode.Acrylic},monitors[0]);w.ShowCard();Pump(280);
  Check(!w.BackdropEnabled||(GetWindowLong(new WindowInteropHelper(w).Handle,-20)&0x80000)==0,"Off fade-out then Acrylic replay must clear native layered alpha");
  w.HideCard(); Check(!w.IsVisible,"hide card"); w.Close();
  Console.WriteLine("MONITORS: "+monitors.Length+"; primary scale: "+monitors[0].DpiScale);
  IntegratedUi();
 }
 static partial void IntegratedUi();
 public static void Pump(int ms) {
  var frame=new DispatcherFrame(); var t=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(ms)};
  t.Tick+=delegate { t.Stop(); frame.Continue=false; }; t.Start(); Dispatcher.PushFrame(frame);
 }
}
}
