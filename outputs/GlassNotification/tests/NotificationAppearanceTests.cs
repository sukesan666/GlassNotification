using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Glass {
public static partial class Tests {
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
 [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
 static void NotificationAppearanceTests() {
  NotificationOpaqueEdgeTests();
  NotificationSlideTests();
  CompositionSlideTests();
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
 static void NotificationOpaqueEdgeTests(){
  var card=new NotificationWindow();
  try{
   var monitor=NativeWindow.GetMonitors()[0];var surface=(Border)card.Content;
   var colors=new[]{"Default","#FFFFFF","#8ECAFF"};var expected=new[]{Color.FromRgb(241,245,250),Colors.White,Color.FromRgb(142,202,255)};
   foreach(bool rounded in new[]{false,true})for(int i=0;i<colors.Length;i++)foreach(double scale in new[]{1.0,1.5,2.0}){
    card.Apply(new AppSettings {Mode=BackdropMode.Off,CardColor=colors[i],RoundedCorners=rounded},monitor);
    surface.Measure(new Size(card.Width,card.Height));surface.Arrange(new Rect(0,0,card.Width,card.Height));surface.UpdateLayout();
    int width=(int)Math.Ceiling(card.Width*scale),height=(int)Math.Ceiling(card.Height*scale);
    var bitmap=new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(surface);
    int stride=width*4;var pixels=new byte[stride*height];bitmap.CopyPixels(pixels,stride,0);
    foreach(var point in new[]{new Point(width/2,0),new Point(width/2,height-1),new Point(0,height/2),new Point(width-1,height/2)}){
     int offset=(int)point.Y*stride+(int)point.X*4;
     Check(pixels[offset+3]==255&&pixels[offset]==expected[i].B&&pixels[offset+1]==expected[i].G&&pixels[offset+2]==expected[i].R,"opaque notification color reaches every straight edge without a transparent black gap: "+colors[i]+" rounded="+rounded+" scale="+scale);
    }
   }
   card.Apply(new AppSettings {Mode=BackdropMode.Off,HighContrast=true},monitor);
   Check(surface.BorderThickness.Left==2&&((SolidColorBrush)surface.BorderBrush).Color==Colors.White,"high contrast retains its intentional visible border");
  }finally{card.Close();}
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
