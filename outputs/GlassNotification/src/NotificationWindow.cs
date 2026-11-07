using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Glass {
public interface INotificationView {void ShowCard(); void HideCard(); void FadeOut(Action completed);}
public interface IStaticNotificationView {void ShowStaticCard();}
public interface INotificationContentView {void SetNotification(SystemNotification value);}
public interface IStackableNotificationView {
 double CardHeightDip {get;}
 double StackOffsetDip {get;}
 void SetStackOffset(double offsetDip);
}
public sealed class NotificationWindow : Window,INotificationView,IStaticNotificationView,INotificationContentView,IStackableNotificationView {
 internal const double EntryDurationSeconds=.66,ExitDurationSeconds=.33;
 internal const int AnimationFramesPerSecond=120;
 struct LineMetrics {public double App,Title,Body;}
 static readonly Dictionary<string,LineMetrics> lineMetricsCache=new Dictionary<string,LineMetrics>(StringComparer.Ordinal);
 readonly Border surface; readonly NotificationTextBlock appName,title,body; readonly StackPanel text;
 readonly Image thumbnail;bool circleImage;SystemNotification currentNotification;
 IntPtr handle; MonitorInfo monitor; Rect pixels; AppSettings settings; DispatcherTimer animation; EventHandler renderFrame;
 readonly WindowBackdropService backdrop;HwndSource source;
 ICompositionSlide compositionSlide;Action compositionCompleted;bool compositionEntering;
 static long nextCompositionToken;long compositionToken;bool compositionHandoffPending;
 internal Func<CompositionSlideRequest,ICompositionSlide> CompositionSlideFactory;
 DispatcherOperation contentRefresh;
 double stackOffsetDip;
 bool highContrastEnabled;
 double measuredMinimumHeight;
 public double CardHeightDip {get{return Height>0?Height:(settings==null?88:settings.Height);}}
 public double StackOffsetDip {get{return stackOffsetDip;}}
 public Rect TargetPixels {get{return pixels;}}
 public bool ForceFallback {get;set;}
 public bool BackdropEnabled {get{return !HighContrastEnabled&&(deferredSettings!=null?(!ForceFallback&&deferredSettings.Mode!=BackdropMode.Off):backdrop.AppliedMode!=BackdropMode.Off);}}
 public bool HighContrastEnabled {get{return deferredSettings!=null?deferredSettings.HighContrast:highContrastEnabled;}}
 public string BackdropStatus {get{return backdrop.Status;}}
 public event EventHandler BackdropChanged;
 public event Action<SystemNotification> NotificationClicked;
 public double MeasuredMinimumHeight {get{return deferredSettings!=null?EstimateMinimumHeight(deferredSettings):measuredMinimumHeight;}}
 public int BodyLines {get;private set;}
 public double EntryOffset {get;private set;}
 public event EventHandler DpiEnvironmentChanged;
 public NotificationWindow() {
  Title="Glass Notification Card"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
  ShowInTaskbar=false; ShowActivated=false; Topmost=true; Background=Brushes.Transparent;
  FontFamily=new FontFamily("Yu Gothic UI"); UseLayoutRounding=true; Cursor=Cursors.Hand;
  surface=new Border {Background=new SolidColorBrush(Color.FromRgb(241,245,250)),CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(0),BorderBrush=Brushes.Transparent,Padding=new Thickness(16,11,16,11),Cursor=Cursors.Hand};
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
  grid.Children.Add(new Border {Visibility=Visibility.Collapsed});
  text=new StackPanel {VerticalAlignment=VerticalAlignment.Center}; Grid.SetColumn(text,1);
  appName=new NotificationTextBlock {Text="メッセージ",Foreground=new SolidColorBrush(Color.FromRgb(102,111,124)),TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,0,0,2)};
  title=new NotificationTextBlock {Text="デザインの時間",FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(31,42,56)),TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,0,0,2)};
  body=new NotificationTextBlock {Text="ちょっと気分のいい通知を、ここに。",Foreground=new SolidColorBrush(Color.FromRgb(67,78,93)),TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis};
  text.Children.Add(appName);text.Children.Add(title);text.Children.Add(body);grid.Children.Add(text);
  thumbnail=new Image {Width=56,Height=56,Margin=new Thickness(10,0,0,0),Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed};Grid.SetColumn(thumbnail,2);grid.Children.Add(thumbnail);surface.Child=grid;Content=surface;
  MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(!e.Handled){e.Handled=true;RaiseClicked();}};
  MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(!e.Handled)RaiseClicked();};
  SourceInitialized+=delegate {
   handle=new WindowInteropHelper(this).Handle; NativeWindow.NonActivating(handle);
   source=HwndSource.FromHwnd(handle);
   source.AddHook(Hook);
  };
  backdrop=new WindowBackdropService(this,surface,Border.BackgroundProperty);
  backdrop.Changed+=delegate {ApplyCorners();QueueContentRefresh();if(BackdropChanged!=null)BackdropChanged(this,EventArgs.Empty);};
 }
 internal void RaiseClicked(){if(NotificationClicked!=null)NotificationClicked(currentNotification);}
 void RefreshSurfaceContent(){
  if(contentRefresh!=null){contentRefresh.Abort();contentRefresh=null;}
  if(surface.Width>0&&surface.Height>0){surface.Measure(new Size(surface.Width,surface.Height));surface.Arrange(new Rect(0,0,surface.Width,surface.Height));}
  UpdateLayout();backdrop.PresentContent(surface);
  if(compositionSlide!=null){try{compositionSlide.Update(surface);}catch{FinishCompositionSlide();}}
 }
 public void SetSampleText(string value) {FlushDeferred();currentNotification=null;body.Text=value;if(IsVisible)RefreshSurfaceContent();}
 public void SetNotification(SystemNotification value){
  currentNotification=value;
  appName.Text=value==null?"メッセージ":value.App;title.Text=value==null?"デザインの時間":value.Title;body.Text=value==null?"ちょっと気分のいい通知を、ここに。":value.Body;
  thumbnail.Source=value==null?null:(value.ContentImage??value.LogoImage??value.AppIcon);thumbnail.Visibility=thumbnail.Source==null?Visibility.Collapsed:Visibility.Visible;
  circleImage=value!=null&&value.ContentImage==null&&value.LogoImage!=null&&value.LogoCircle;
  ApplyImageCrop();
  if(IsVisible)RefreshSurfaceContent();
 }
 AppSettings deferredSettings;MonitorInfo deferredMonitor;
 // Hidden cards are updated lazily; the pending settings are applied before the card is shown or re-stacked.
 public void ApplyWhenShown(AppSettings value,MonitorInfo display) {
  if(IsVisible){Apply(value,display);return;}
  deferredSettings=value==null?null:value.Normalize();deferredMonitor=display;
 }
 void FlushDeferred() {
  if(deferredSettings==null)return;
  var pending=deferredSettings;var pendingMonitor=deferredMonitor;deferredSettings=null;deferredMonitor=null;
  Apply(pending,pendingMonitor);
 }
 static LineMetrics GetLineMetrics(string fontFamilyName,double fontSize,bool highContrast){
  string resolvedFamily=NotificationFonts.Resolve(fontFamilyName);
  string key=resolvedFamily+"|"+fontSize.ToString(CultureInfo.InvariantCulture)+"|"+(highContrast?"1":"0");
  LineMetrics metrics;
  if(lineMetricsCache.TryGetValue(key,out metrics))return metrics;
  var family=new FontFamily(resolvedFamily);
  double appFont=Math.Max(highContrast?14:11,fontSize-2);
  double titleFont=Math.Max(highContrast?15:0,fontSize+1);
  double bodyFont=Math.Max(highContrast?14:0,fontSize);
  metrics=new LineMetrics {
   App=MeasureLineHeight(family,highContrast?FontWeights.Bold:FontWeights.Normal,appFont),
   Title=MeasureLineHeight(family,highContrast?FontWeights.Bold:FontWeights.SemiBold,titleFont),
   Body=MeasureLineHeight(family,highContrast?FontWeights.SemiBold:FontWeights.Normal,bodyFont)
  };
  lineMetricsCache[key]=metrics;
  return metrics;
 }
 static double MeasureLineHeight(FontFamily family,FontWeight weight,double size){
  var formatted=new FormattedText("国Ag",CultureInfo.GetCultureInfo("ja-JP"),FlowDirection.LeftToRight,new Typeface(family,FontStyles.Normal,weight,FontStretches.Normal),size,Brushes.Black,1);
  return Math.Ceiling(formatted.Height+1);
 }
 internal static double EstimateMinimumHeight(AppSettings value){
  var s=(value??new AppSettings()).Normalize();
  var m=GetLineMetrics(s.FontFamilyName,s.FontSize,s.HighContrast);
  return 22+m.App+m.Title+m.Body+4;
 }
 public AppSettings Apply(AppSettings value,MonitorInfo display) {
  deferredSettings=null;deferredMonitor=null;
  var previous=settings;settings=value.Normalize(); monitor=display;
  highContrastEnabled=settings.HighContrast;
  if(FontFamily.Source!=settings.FontFamilyName)FontFamily=new FontFamily(settings.FontFamilyName);
  backdrop.SetCardColor(HighContrastEnabled?"#000000":settings.CardColor);
  SetTextForeground(appName,HighContrastEnabled?Color.FromRgb(0,255,255):NotificationColors.Color(settings.TextColor,Color.FromRgb(102,111,124)));
  SetTextForeground(title,HighContrastEnabled?Color.FromRgb(255,255,0):NotificationColors.Color(settings.TextColor,Color.FromRgb(31,42,56)));
  SetTextForeground(body,HighContrastEnabled?Colors.White:NotificationColors.Color(settings.TextColor,Color.FromRgb(67,78,93)));
  if(previous==null||previous.HighContrast!=settings.HighContrast||previous.TextOutlineColor!=settings.TextOutlineColor){
   var outline=HighContrastEnabled||settings.TextOutlineColor==NotificationColors.Default?null:new SolidColorBrush(NotificationColors.Color(settings.TextOutlineColor,Colors.Black));
   if(outline!=null)outline.Freeze();appName.OutlineBrush=title.OutlineBrush=body.OutlineBrush=outline;
  }
  surface.BorderBrush=HighContrastEnabled?Brushes.White:Brushes.Transparent;
  surface.BorderThickness=new Thickness(HighContrastEnabled?2:0);
  // Keep the former border's spacing inside the fill instead of leaving a transparent edge.
  surface.Padding=HighContrastEnabled?new Thickness(15,10,15,10):new Thickness(16,11,16,11);
  surface.CornerRadius=new CornerRadius(settings.RoundedCorners?8:0);
  appName.FontSize=Math.Max(HighContrastEnabled?14:11,settings.FontSize-2);
  title.FontSize=Math.Max(HighContrastEnabled?15:0,settings.FontSize+1);
  body.FontSize=Math.Max(HighContrastEnabled?14:0,settings.FontSize);
  appName.FontWeight=HighContrastEnabled?FontWeights.Bold:FontWeights.Normal;
  title.FontWeight=HighContrastEnabled?FontWeights.Bold:FontWeights.SemiBold;
  body.FontWeight=HighContrastEnabled?FontWeights.SemiBold:FontWeights.Normal;
  thumbnail.Width=thumbnail.Height=settings.ImageSize;
  bool imageLeft=settings.ImagePosition=="Left";Grid.SetColumn(thumbnail,imageLeft?0:2);thumbnail.Margin=imageLeft?new Thickness(0,0,10,0):new Thickness(10,0,0,0);
  ApplyImageCrop();
  var metrics=GetLineMetrics(settings.FontFamilyName,settings.FontSize,HighContrastEnabled);
  double appLine=metrics.App,titleLine=metrics.Title,bodyLine=metrics.Body;
  // One fully visible body line is the minimum. A second line is enabled only when it fits.
  measuredMinimumHeight=22+appLine+titleLine+bodyLine+4;
  var requested=Layout.Resolve(settings,monitor.AvailableDip,measuredMinimumHeight);
  double contentMinimum=thumbnail.Source==null?measuredMinimumHeight:Math.Max(measuredMinimumHeight,settings.ImageSize+surface.Padding.Top+surface.Padding.Bottom+surface.BorderThickness.Top+surface.BorderThickness.Bottom);
  var result=Layout.Resolve(settings,monitor.AvailableDip,contentMinimum,stackOffsetDip);settings=result.Settings;
  double spare=settings.Height-22-appLine-titleLine-4;
  BodyLines=spare>=bodyLine*2?2:1;
  appName.Height=appLine; title.Height=titleLine; body.LineHeight=bodyLine;body.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;body.Height=bodyLine*BodyLines;
  Width=settings.Width;Height=settings.Height; surface.Width=settings.Width;surface.Height=settings.Height;
  pixels=monitor.ToPixels(result.Bounds);EntryOffset=(SlidePosition(pixels,AnimationScreen(),IsLeftCorner(),0,true,16*monitor.DpiScale).X-pixels.X)/monitor.DpiScale;
  backdrop.SetStrength(settings.BackdropStrength);
  if(HighContrastEnabled)backdrop.SetMode(BackdropMode.Off);else ApplyBackdrop();
  ApplyCorners();if(handle!=IntPtr.Zero&&IsVisible&&animation==null&&compositionSlide==null)Place();
  if(IsVisible){Measure(new Size(Width,Height));Arrange(new Rect(0,0,Width,Height));QueueContentRefresh();}
  // A content image can temporarily enlarge a notification without changing the user's card height.
  return requested.Settings.Copy();
 }
 static void SetTextForeground(TextBlock block,Color color){
  var current=block.Foreground as SolidColorBrush;if(current!=null&&current.Color==color)return;
  var brush=new SolidColorBrush(color);brush.Freeze();block.Foreground=brush;
 }
 public void SetStackOffset(double offsetDip) {
  if(IsVisible)FlushDeferred();
  double normalized=Math.Max(0,double.IsNaN(offsetDip)||double.IsInfinity(offsetDip)?0:offsetDip);
  stackOffsetDip=normalized;
  if(settings!=null&&monitor!=null&&IsVisible) {
   double contentMinimum=thumbnail.Source==null?measuredMinimumHeight:Math.Max(measuredMinimumHeight,settings.ImageSize+surface.Padding.Top+surface.Padding.Bottom+surface.BorderThickness.Top+surface.BorderThickness.Bottom);
   var result=Layout.Resolve(settings,monitor.AvailableDip,contentMinimum,stackOffsetDip);
   pixels=monitor.ToPixels(result.Bounds);
   if(handle!=IntPtr.Zero&&IsVisible&&animation==null&&compositionSlide==null)Place();
  }
 }
 void ApplyImageCrop(){double radius=thumbnail.Width/2;thumbnail.Clip=circleImage?new EllipseGeometry(new Point(radius,radius),radius,radius):null;thumbnail.Stretch=circleImage?Stretch.UniformToFill:Stretch.Uniform;}
 IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled) {
  if(msg==0x21) {handled=true;return new IntPtr(3);} // MA_NOACTIVATE
  if(msg==0x2e0&&DpiEnvironmentChanged!=null)DpiEnvironmentChanged(this,EventArgs.Empty);
  if(msg==0x8051)backdrop.CheckGraphicsDevice();
  if(msg==0x8056){handled=true;CompositionAnimationCompleted(w.ToInt64());return IntPtr.Zero;}
  if(msg==0x8055&&compositionSlide!=null){handled=true;FinishCompositionSlide();return IntPtr.Zero;}
  if(msg==0x8053&&compositionSlide!=null){handled=true;RaiseClicked();return IntPtr.Zero;}
  return IntPtr.Zero;
 }
 void ApplyBackdrop() {
  backdrop.SetMode(settings==null||ForceFallback?BackdropMode.Off:settings.Mode);
 }
 void ApplyCorners(){if(handle!=IntPtr.Zero&&settings!=null)NativeWindow.ConfigureCorners(handle,settings.RoundedCorners);}
 void Place() {NativeWindow.Place(handle,pixels);}
 bool IsLeftCorner(){return settings!=null&&(settings.Corner==Corner.TopLeft||settings.Corner==Corner.BottomLeft);}
 Rect AnimationScreen(){return monitor.ScreenBoundsPx.Width>0?monitor.ScreenBoundsPx:monitor.WorkAreaPx;}
 internal static Rect SlidePosition(Rect target,Rect screen,bool left,double progress,bool entering,double padding){
  double t=Math.Max(0,Math.Min(1,progress));
  double offscreen=left?screen.Left-target.Width-padding:screen.Right+padding;
  double amount=entering?Math.Pow(1-t,3):Math.Pow(t,3);
  return new Rect(target.X+(offscreen-target.X)*amount,target.Y,target.Width,target.Height);
 }
 void ReapplyAfterRender() {
  backdrop.QueueRefresh();
  QueueContentRefresh();
 }
 void QueueContentRefresh(){
  if(!IsVisible||handle==IntPtr.Zero||(contentRefresh!=null&&contentRefresh.Status==DispatcherOperationStatus.Pending))return;
  contentRefresh=Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {contentRefresh=null;if(IsVisible&&handle!=IntPtr.Zero)RefreshSurfaceContent();}));
 }
 public void ShowStaticCard() {
  FlushDeferred();
  bool wasVisible=IsVisible;StopAnimation();surface.Opacity=1;
  if(!wasVisible){
   new WindowInteropHelper(this).EnsureHandle();
   Place();Show();NativeWindow.Alpha(handle,1);ApplyBackdrop();backdrop.Refresh();RefreshSurfaceContent();
  }else{
   Place();NativeWindow.Alpha(handle,1);ApplyBackdrop();QueueContentRefresh();backdrop.SetContentOpacity(1);
  }
 }
 public void ShowCard() {
  FlushDeferred();
  bool wasVisible=IsVisible;StopAnimation();surface.Opacity=1;
  if(!wasVisible) {new WindowInteropHelper(this).EnsureHandle();NativeWindow.Place(handle,SlidePosition(pixels,AnimationScreen(),IsLeftCorner(),0,true,16*monitor.DpiScale));Show();NativeWindow.Alpha(handle,1);ApplyBackdrop();backdrop.Refresh();RefreshSurfaceContent();Animate(true,null);} else {
   Place();
   NativeWindow.Alpha(handle,1);
   ApplyBackdrop();
   backdrop.Refresh();
   QueueContentRefresh();
   backdrop.SetContentOpacity(1);
  }
 }
 public void HideCard() {StopAnimation();surface.Opacity=1;if(handle!=IntPtr.Zero)NativeWindow.Alpha(handle,1);Hide();}
 public void FadeOut(Action completed) {StopAnimation();Animate(false,completed);}
 void Animate(bool entering,Action completed) {
  double seconds=entering?EntryDurationSeconds:ExitDurationSeconds;
  surface.Opacity=1;backdrop.SetContentOpacity(1);
  Rect exitStart=entering?pixels:NativeWindow.GetBounds(handle);
  // Mica remains on the existing path: a temporary host must not substitute Acrylic for it.
   if(CompositionSlideFactory!=null&&(!BackdropEnabled||settings.Mode!=BackdropMode.Mica)){
   var offscreen=SlidePosition(pixels,AnimationScreen(),IsLeftCorner(),entering?0:1,entering,16*monitor.DpiScale);
   var request=new CompositionSlideRequest {Owner=handle,Token=System.Threading.Interlocked.Increment(ref nextCompositionToken),Content=surface,From=entering?offscreen:exitStart,To=entering?pixels:offscreen,Scale=monitor.DpiScale,Seconds=seconds,Entering=entering,Acrylic=BackdropEnabled,Strength=(int)settings.BackdropStrength,Radius=settings.RoundedCorners?8:0,CardColor=HighContrastEnabled?"#000000":settings.CardColor};
   compositionSlide=CompositionSlideFactory(request);
   if(compositionSlide!=null&&!compositionSlide.IsVisible){compositionSlide.Dispose();compositionSlide=null;}
   if(compositionSlide!=null){
    // Native host has cloaked the real window; now move it without flashing the final position.
    Place();
    compositionEntering=entering;compositionCompleted=completed;compositionToken=request.Token;
    // Completion comes from the compositor batch, not an independent UI timer.
    var session=compositionSlide;
    // Check once after presentation as well; accepting the API call is not proof
    // that DWM actually made the animation host visible.
    Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate {
     if(!ReferenceEquals(compositionSlide,session))return;
     bool visible;try{visible=session.IsVisible;}catch{visible=false;}
     if(visible)return;
     StopAnimation();NativeWindow.Place(handle,entering?offscreen:exitStart);
     StartCpuAnimation(entering,completed,exitStart);
    }));return;
   }
   if(!entering)NativeWindow.Place(handle,exitStart);
  }
  StartCpuAnimation(entering,completed,exitStart);
 }
  void StartCpuAnimation(bool entering,Action completed,Rect exitStart){
   StopAnimation();
   var watch=System.Diagnostics.Stopwatch.StartNew();double duration=entering?320.0:220.0;
   Rect startRect=entering?pixels:exitStart;
   bool finished=false;
   Action frame=delegate {
    if(finished)return;
    double t=Math.Min(1,watch.Elapsed.TotalMilliseconds/duration);
    var pos=SlidePosition(startRect,AnimationScreen(),IsLeftCorner(),t,entering,16*monitor.DpiScale);
    NativeWindow.SlideMove(handle,(int)Math.Round(pos.X),(int)Math.Round(pos.Y));
    if(t>=1) {
     finished=true;
     StopAnimation();
     if(entering){Place();RefreshSurfaceContent();}else Hide();
     if(completed!=null)completed();
    }
   };
   renderFrame=delegate(object sender,EventArgs e){frame();};
   CompositionTarget.Rendering+=renderFrame;
   animation=new DispatcherTimer(DispatcherPriority.Render) {Interval=TimeSpan.FromTicks(TimeSpan.TicksPerSecond/AnimationFramesPerSecond)};
   animation.Tick+=delegate(object sender,EventArgs e){frame();};
   frame();if(animation!=null)animation.Start();
  }
 internal void CompositionAnimationCompleted(long token){if(token!=0&&token==compositionToken)FinishCompositionSlide();}
 void FinishCompositionSlide(){
  if(compositionSlide==null||compositionHandoffPending)return;
  bool entering=compositionEntering;var completed=compositionCompleted;
  if(!entering){Hide();StopAnimation();if(completed!=null)completed();return;}
  var session=compositionSlide;compositionHandoffPending=true;
  // Upload the real foreground and restore its HWND while the final proxy frame
  // still covers it. Let WPF process rendering before the presentation barrier.
  try{Place();RefreshSurfaceContent();session.RevealOwner();}
  catch{StopAnimation();Place();ReapplyAfterRender();if(completed!=null)completed();return;}
  Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate {
   if(!ReferenceEquals(compositionSlide,session))return;
   try{session.SynchronizeOwner();}
   catch{compositionHandoffPending=false;StopAnimation();Place();ReapplyAfterRender();if(completed!=null)completed();return;}
   compositionSlide=null;compositionCompleted=null;compositionToken=0;compositionHandoffPending=false;
   session.Dispose();if(completed!=null)completed();
  }));
 }
 void StopAnimation() {
  compositionToken=0;compositionHandoffPending=false;
  if(renderFrame!=null){CompositionTarget.Rendering-=renderFrame;renderFrame=null;}
  if(animation!=null){animation.Stop();animation=null;}
  if(compositionSlide!=null){var old=compositionSlide;compositionSlide=null;compositionCompleted=null;try{if(handle!=IntPtr.Zero&&IsVisible)NativeWindow.Place(handle,old.CurrentBounds);}finally{old.Dispose();}}
 }
 protected override void OnClosed(EventArgs e) {StopAnimation();if(contentRefresh!=null){contentRefresh.Abort();contentRefresh=null;}backdrop.Dispose();if(source!=null){source.RemoveHook(Hook);source=null;}handle=IntPtr.Zero;BackdropChanged=null;NotificationClicked=null;base.OnClosed(e);}
 public void SavePreview(string path) {
  FlushDeferred();RefreshSurfaceContent();var bmp=new RenderTargetBitmap((int)Math.Ceiling(Width*2),(int)Math.Ceiling(Height*2),192,192,PixelFormats.Pbgra32);bmp.Render(surface);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var stream=File.Create(path))encoder.Save(stream);
 }
}
}
