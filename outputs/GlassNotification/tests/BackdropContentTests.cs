using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Glass {
public static partial class Tests {
 static void BackdropContentTests(){
  // The composition target must receive the measured notification foreground.
  // Native acceptance alone previously left a blurred card with no text.
  var method=typeof(WindowBackdropService).GetMethod("PresentContent");
  Check(method!=null,"backdrop must present notification foreground above the SDK material");
  var root=new Border {Background=Brushes.White,Padding=new Thickness(10),Child=new TextBlock {Text="文字 Ag 国",FontSize=20}};
  var window=new Window {Width=280,Height=140,Content=root,ShowInTaskbar=false};
  var device=new ContentDevice();var service=new WindowBackdropService(window,root,Border.BackgroundProperty,device);
  service.SetMode(BackdropMode.Acrylic);window.Show();Pump(40);root.Opacity=.25;
  Check(((SolidColorBrush)root.Background).Color.A==0,"SDK Acrylic owns the background; WPF must not add a fixed tint over it");
  method.Invoke(service,new object[]{root});
  Check(device.Pixels[3]==0,"uploaded foreground keeps empty corner pixels transparent instead of painting Acrylic twice");
  service.SetCardColor("#000000");
  Check(device.CardColor=="#000000","selected black reaches native Acrylic material and its fallback color, not only the foreground bitmap");
  service.SetCardColor(NotificationColors.Default);
  Check(device.CardColor==NotificationColors.Default,"restoring default color resets native Acrylic overrides");
  service.SetMode(BackdropMode.Mica);
  Check(((SolidColorBrush)root.Background).Color.A>0,"Mica retains its WPF tint without an SDK foreground target");
  service.SetMode(BackdropMode.Acrylic);
  Check(((SolidColorBrush)root.Background).Color.A==0,"returning from Mica removes the foreground tint again");
  foreach(var strength in new[]{BackdropStrength.Weak,BackdropStrength.Standard,BackdropStrength.Strong,BackdropStrength.Weak}){
   service.SetStrength(strength);service.SetCardColor("#FFFFFF");service.Refresh();method.Invoke(service,new object[]{root});
   Check(device.Strength==strength&&device.CardColor=="#FFFFFF","strength and color survive refresh and repeated changes together");
   Check(((SolidColorBrush)root.Background).Color.A==0&&device.Pixels[3]==0,"all strengths keep the uploaded background transparent, including white");
  }
  service.SetCardColor(NotificationColors.Default);
  Check(device.Pixels!=null&&device.Width>0&&device.Height>0,"measured foreground pixels reach native composition");
  bool hasInk=false;for(int i=0;i<device.Pixels.Length;i+=4)if(device.Pixels[i+3]>200&&device.Pixels[i]<100){hasInk=true;break;}
  Check(hasInk,"foreground pixels contain the notification text, not only transparent background");
  Check(root.Opacity==.25,"capturing foreground preserves current animation opacity");
  int changed=0;service.Changed+=delegate{changed++;};
  device.DeviceStatus=-1;
  var deviceCheck=typeof(WindowBackdropService).GetMethod("CheckGraphicsDevice");
  Check(deviceCheck!=null,"SDK device removal must have a UI-thread fallback path");
  deviceCheck.Invoke(service,null);
  Check(service.AppliedMode==BackdropMode.Off&&changed>0,"GPU loss restores opaque content and updates UI status");
  device.DeviceStatus=0;service.Refresh();changed=0;
  device.Fail=true;method.Invoke(service,new object[]{root});
  Check(service.AppliedMode==BackdropMode.Off&&ReferenceEquals(root.Background,Brushes.White),"foreground upload failure restores readable opaque WPF content");
  Check(changed>0,"foreground failure notifies settings UI of fallback");
  window.Close();Check(device.Disposed,"closing releases native composition resources");
 }
 sealed class ContentDevice : IBackdropNative,IBackdropContentNative,IBackdropColorNative,IBackdropStrengthNative {
  public BackdropStrength Strength;public int SetStrength(BackdropStrength value){Strength=value;return 0;}
  public string CardColor;public int SetColor(string value){CardColor=value;return 0;}
  public byte[] Pixels;public int Width,Height;public bool Fail,Disposed;
  public string UnavailableReason(){return null;}
  public int SetType(IntPtr hwnd,int type){return 0;}
  public int ExtendFrame(IntPtr hwnd,bool enabled){return 0;}
  public bool HasContentTarget {get{return true;}}
  public int DeviceStatus {get;set;}
  // Connected to the optional content interface in the implementation step.
  public int Present(int width,int height,int stride,byte[] pixels){Width=width;Height=height;Pixels=pixels;return Fail?-1:0;}
  public void SetOpacity(double opacity){}
  public void Dispose(){Disposed=true;}
 }
}
}
