using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Glass {
public static partial class Tests {
 [System.Runtime.InteropServices.DllImport("GlassBackdrop.dll",EntryPoint="ReadBackdropAppearance")]
 static extern int ReadColorTestAppearance(IntPtr context,out float tint,out float luminosity,out uint color,out uint fallback);
 static void NotificationColorTests(){
  var window=new NotificationWindow();var monitor=NativeWindow.GetMonitors()[0];
  var settings=new AppSettings {Mode=BackdropMode.Off,CardColor="#8ECAFF",TextColor="#FFFFFF"};
  window.Apply(settings,monitor);var border=(Border)window.Content;
  Check(((SolidColorBrush)border.Background).Color==Color.FromRgb(142,202,255),"selected card color applies to opaque notification");
  var text=(StackPanel)((Grid)border.Child).Children[1];
  foreach(TextBlock block in text.Children)Check(((SolidColorBrush)block.Foreground).Color==Colors.White,"chosen text color applies to every notification text block");
  window.ShowCard();Pump(260);
  foreach(BackdropMode mode in new[]{BackdropMode.Acrylic,BackdropMode.Mica,BackdropMode.Off}){
   settings.Mode=mode;window.Apply(settings,monitor);Pump(40);
   var color=((SolidColorBrush)border.Background).Color;
   if(mode==BackdropMode.Acrylic&&window.BackdropEnabled){
    Check(color.A==0,"native Acrylic is not obscured by a WPF background tint");
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var service=typeof(NotificationWindow).GetField("backdrop",flags).GetValue(window);
    var native=typeof(WindowBackdropService).GetField("native",flags).GetValue(service);
    var context=(IntPtr)typeof(SdkBackdropNative).GetField("context",flags).GetValue(native);
    foreach(var strength in new[]{BackdropStrength.Weak,BackdropStrength.Standard,BackdropStrength.Strong,BackdropStrength.Weak}){
     settings.BackdropStrength=strength;window.Apply(settings,monitor);Pump(40);
     float tint,luminosity;uint actual,fallback;
     int hr=ReadColorTestAppearance(context,out tint,out luminosity,out actual,out fallback);
     int index=(int)strength;
     Check(hr==0&&Math.Abs(tint-new[]{.20f,.45f,.70f}[index])<.001f&&Math.Abs(luminosity-new[]{.15f,.40f,.65f}[index])<.001f,"native Acrylic reads back the requested tint and luminosity for "+strength);
     Check(actual==0xFF8ECAFFu&&fallback==actual,"strength changes preserve the selected native material and fallback color");
    }
   }else Check(color.R==142&&color.G==202&&color.B==255&&color.A==(window.BackdropEnabled?100:255),"custom tint preserves Mica transparency or opaque fallback: "+mode);
  }
  settings.CardColor=settings.TextColor="Default";window.Apply(settings,monitor);
  Check(((SolidColorBrush)border.Background).Color==Color.FromRgb(241,245,250),"standard card color restores original opaque appearance");
  Check(((SolidColorBrush)((TextBlock)text.Children[0]).Foreground).Color==Color.FromRgb(102,111,124),"standard text restores original secondary-label shade");
  window.Close();
 }
}
}
