using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace Glass {
public static partial class Tests {
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
 [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
 static void NotificationAppearanceTests() {
  NotificationSlideTests();
  CompositionSlideTests();
  NotificationCloseButtonTests();
  NotificationThumbnailSizeTests();
  var card=new NotificationWindow();var monitor=NativeWindow.GetMonitors()[0];var settings=new AppSettings {FontFamilyName="Consolas",FontSize=18,RoundedCorners=true,Mode=BackdropMode.Acrylic};
  card.Apply(settings,monitor);card.ShowCard();Pump(300);
  Check(card.FontFamily.Source=="Consolas"&&card.MeasuredMinimumHeight<=card.Height,"font changes notification measurement and text family");
  IntPtr hwnd=new WindowInteropHelper(card).Handle;int preference;
  Check(DwmGetWindowAttribute(hwnd,33,out preference,4)==0&&preference==2,"rounded notification stores the requested DWM corner preference (not a pixel test)");
  // DWMWA_BORDER_COLOR is a setter attribute; getter support is not guaranteed.
  Check(NativeWindow.ConfigureCorners(hwnd,true),"DWM accepts border suppression and corner preference on the real toast (not a pixel test)");
  IntPtr region=CreateRectRgn(0,0,0,0);try {Check(GetWindowRgn(hwnd,region)==0,"no custom region that prevents DWM rounding");}finally {DeleteObject(region);}
  foreach(var mode in new[]{BackdropMode.Acrylic,BackdropMode.Mica,BackdropMode.Off}) {
   settings.Mode=mode;
   foreach(bool rounded in new[]{false,true}) {
    settings.RoundedCorners=rounded;settings.Width=rounded?480:360;card.Apply(settings,monitor);card.HideCard();card.ShowCard();Pump(260);
    Check(DwmGetWindowAttribute(hwnd,33,out preference,4)==0&&preference==(rounded?2:1),"DWM preference persists across mode/resize/replay: "+mode+" "+rounded);
    Check(((Border)card.Content).CornerRadius.TopLeft==(rounded?8:0),"WPF surface radius follows the selected corner mode (visual clipping checked separately)");
    Check(((SolidColorBrush)((Border)card.Content).BorderBrush).Color.A==0,"ordinary toast has no white foreground outline in any backdrop mode");
    Check(NativeWindow.ConfigureCorners(hwnd,rounded),"DWM accepts border suppression across mode switches and replay (not a pixel test)");
   }
  }
  settings.FontFamilyName="missing-font-that-does-not-exist";card.Apply(settings,monitor);
  Check(card.FontFamily.Source==NotificationFonts.Default,"missing font safely falls back without crashing card");
  card.Close();
 }
 static void NotificationSlideTests(){
  Check(NotificationWindow.EntryDurationSeconds==.66&&NotificationWindow.ExitDurationSeconds==.33&&NotificationWindow.AnimationFramesPerSecond==120,"notification slide uses requested durations and target frame rate");
  foreach(var screen in new[]{new Rect(0,0,1920,1080),new Rect(-2560,-100,2560,1440)})foreach(bool left in new[]{true,false}){
   var target=new Rect(left?screen.Left+20:screen.Right-380,screen.Top+20,360,88);
   var start=NotificationWindow.SlidePosition(target,screen,left,0,true,16);
   var shown=NotificationWindow.SlidePosition(target,screen,left,1,true,16);
   var exit=NotificationWindow.SlidePosition(target,screen,left,1,false,16);
   Check((left?start.Right<screen.Left:start.Left>screen.Right)&&shown==target,"entry slides from fully outside its monitor to the target, including negative monitor coordinates");
   Check(exit==start&&NotificationWindow.SlidePosition(target,screen,left,0,false,16)==target,"exit slides completely outside the same monitor edge");
   var middle=NotificationWindow.SlidePosition(target,screen,left,.5,true,16);
   Check(middle.Y==target.Y&&middle.Width==target.Width&&middle.Height==target.Height,"slide preserves vertical stack position and card size");
  }
 }
 static void NotificationCloseButtonTests(){
  var card=new NotificationWindow();
  try{
   var monitor=new MonitorInfo {WorkAreaPx=new Rect(0,0,1920,1080),DpiScale=1};
   var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,120,240,255},4);
   card.SetNotification(new SystemNotification {ContentImage=bitmap});
   var surface=(Border)card.Content;var grid=(Grid)surface.Child;
   var overlay=(Canvas)grid.Children[grid.Children.Count-1];var button=(Button)overlay.Children[0];
   int dismisses=0,opens=0;card.NotificationDismissRequested+=delegate {dismisses++;};card.NotificationClicked+=delegate {opens++;};
   foreach(string position in new[]{"Right","Left"})foreach(int size in new[]{24,56,128}){
    card.Apply(new AppSettings {ImagePosition=position,ImageSize=size,Mode=BackdropMode.Off},monitor);
    surface.Measure(new Size(card.Width,card.Height));surface.Arrange(new Rect(0,0,card.Width,card.Height));surface.UpdateLayout();
    double before=((StackPanel)grid.Children[1]).ActualWidth;
    Check(button.Visibility==Visibility.Collapsed,"close overlay is hidden before hover");
    card.SetHoverState(true);surface.UpdateLayout();
    Check(button.Visibility==Visibility.Visible&&button.Width==size&&button.Height==size,"hover close button matches configured image size: "+size);
    Check(Canvas.GetLeft(button)==(position=="Right"?0:Math.Max(0,overlay.ActualWidth-size)),"hover close button floats opposite the selected image side: "+position);
    Check(((StackPanel)grid.Children[1]).ActualWidth==before,"hover overlay does not move or narrow the notification text");
    card.SetHoverState(false);Check(button.Visibility==Visibility.Collapsed,"close overlay disappears when hover ends");
   }
   card.SetHoverState(true);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(dismisses==1&&opens==0&&button.Visibility==Visibility.Collapsed,"close button requests dismissal without activating notification link");
  }finally{card.Close();}
 }
 static void NotificationThumbnailSizeTests() {
  var card=new NotificationWindow();
  try {
   var monitor=new MonitorInfo {WorkAreaPx=new Rect(0,0,1920,1080),DpiScale=1};
   var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,120,240,255},4);
   card.SetNotification(new SystemNotification {App="Image sender",Title="Long title to trim",Body="A long message wraps without drawing over the content image.",LogoImage=bitmap,ContentImage=bitmap});
   var surface=(Border)card.Content;var grid=(Grid)surface.Child;Image thumbnail=null;
   foreach(var child in grid.Children)if(child is Image)thumbnail=(Image)child;
   Check(thumbnail!=null,"notification exposes a content thumbnail");
   var text=(StackPanel)grid.Children[1];var icon=(Border)grid.Children[0];
   AppSettings applied=null;
   foreach(string position in new[]{"Right","Left"})foreach(int size in new[]{24,56,128}) {
    var settings=new AppSettings {Width=280,Height=88,ImageSize=size,ImagePosition=position,Mode=BackdropMode.Off};
    applied=card.Apply(settings,monitor);
    surface.Measure(new Size(card.Width,card.Height));surface.Arrange(new Rect(0,0,card.Width,card.Height));surface.UpdateLayout();
    Check(thumbnail.ActualWidth==size&&thumbnail.ActualHeight==size,"requested image size controls visible thumbnail dimensions: "+size);
    var imageBounds=thumbnail.TransformToAncestor(surface).TransformBounds(new Rect(new Size(thumbnail.ActualWidth,thumbnail.ActualHeight)));
    var textBounds=text.TransformToAncestor(surface).TransformBounds(new Rect(new Size(text.ActualWidth,text.ActualHeight)));
    Check(imageBounds.Top>=11&&imageBounds.Bottom<=card.Height-11,"content thumbnail stays inside card padding: "+size);
    Check(textBounds.Width>0&&(position=="Right"?textBounds.Right==imageBounds.Left-10:imageBounds.Right==textBounds.Left-10),"left/right image and text keep a ten-pixel gap at minimum card width: "+position+" "+size);
    Check(icon.Visibility==Visibility.Collapsed&&textBounds.Width==238-size,"hidden small icon reserves no width or gap: "+position+" "+size);
    Check(position=="Right"?textBounds.Left==16:imageBounds.Left==16,"selected image side starts at the card's content edge: "+position);
    Check(applied.Height==88&&card.MeasuredMinimumHeight<=88,"temporary image height does not alter the requested card height or text minimum: "+size);
   }
   Check(card.Height==152,"maximum image expands the rendered card to fit image and padding");
   var smaller=applied.Copy();smaller.ImageSize=24;card.Apply(smaller,monitor);
   Check(card.Height==88&&thumbnail.Width==24&&thumbnail.Height==24,"decreasing image size also shrinks the rendered card without waiting for the next notification");
   card.Apply(applied,monitor);
   card.SetNotification(new SystemNotification {App="Text sender",Title="Next notification",Body="No image"});
   foreach(string position in new[]{"Right","Left"}) {
    var cleared=applied.Copy();cleared.ImagePosition=position;card.Apply(cleared,monitor);
    surface.Measure(new Size(card.Width,card.Height));surface.Arrange(new Rect(0,0,card.Width,card.Height));surface.UpdateLayout();
    var clearedTextBounds=text.TransformToAncestor(surface).TransformBounds(new Rect(new Size(text.ActualWidth,text.ActualHeight)));
    Check(card.Height==88&&thumbnail.Source==null&&thumbnail.Visibility==Visibility.Collapsed,"next image-free notification returns to the requested normal height: "+position);
    Check(clearedTextBounds.Left==16&&clearedTextBounds.Width==248,"image-free text uses the full card content width with no reserved image columns: "+position);
   }
   card.SetNotification(new SystemNotification {ContentImage=bitmap});card.Apply(applied,monitor);
   card.SetNotification(null);card.Apply(applied,monitor);
   Check(card.Height==88,"clearing the thumbnail restores the normal card height");
  } finally {card.Close();}
 }
}
}
