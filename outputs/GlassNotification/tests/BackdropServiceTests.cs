using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace Glass {
public static partial class Tests {
 sealed class BackdropDevice : IBackdropNative,IBackdropStrengthNative {
  public string Blocked;public int Result,FrameResult,Calls,LastType;public BackdropStrength LastStrength;
  public string UnavailableReason(){return Blocked;}
  public int SetType(IntPtr hwnd,int value){Calls++;LastType=value;return value==1?0:Result;}
  public int ExtendFrame(IntPtr hwnd,bool enabled){return enabled?FrameResult:0;}
  public int SetStrength(BackdropStrength value){LastStrength=value;return 0;}
 }
 static void BackdropServiceTests() {
  var rootBrush=new SolidColorBrush(Colors.Beige);var windowBrush=new SolidColorBrush(Colors.AliceBlue);
  var root=new ScrollViewer {Background=rootBrush};var window=new Window {Width=280,Height=160,Background=windowBrush,Content=root,ShowInTaskbar=false};
  var device=new BackdropDevice();var service=new WindowBackdropService(window,root,Control.BackgroundProperty,device);
  service.SetMode(BackdropMode.Acrylic);Check(device.Calls==0,"do not create or use HWND before SourceInitialized");
  window.Show();Pump(70);
  Check(service.AppliedMode==BackdropMode.Acrylic&&device.LastType==3,"Acrylic selects Desktop Acrylic on the window target");
  service.SetStrength(BackdropStrength.Strong);Check(device.LastStrength==BackdropStrength.Strong&&service.Strength==BackdropStrength.Strong,"Acrylic strength is forwarded to the native backdrop");
  int unchangedCalls=device.Calls;service.SetStrength(BackdropStrength.Strong);
  Check(device.Calls==unchangedCalls,"unchanged strength does not reconfigure the native backdrop during unrelated settings changes");
  Check(((SolidColorBrush)root.Background).Color.A<255&&window.Background==Brushes.Transparent,"root tint is configured translucent; this fake-native test does not prove visible material");
  Check(window.Opacity==1&&!window.AllowsTransparency,"keep foreground rendering and window alpha intact");
  for(int i=0;i<10;i++){service.SetMode(BackdropMode.Mica);service.SetMode(BackdropMode.Off);}
  Check(ReferenceEquals(root.Background,rootBrush)&&ReferenceEquals(window.Background,windowBrush),"Off restores original brush instances after repeated switching");
  Check(HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).CompositionTarget.BackgroundColor!=Colors.Transparent,"Off restores original HwndTarget color");
  device.Result=unchecked((int)0x80070057);service.SetMode(BackdropMode.Acrylic);
  Check(service.RequestedMode==BackdropMode.Acrylic&&service.AppliedMode==BackdropMode.Off&&ReferenceEquals(root.Background,rootBrush),"failed DWM call separates request from fallback and restores root");
  device.Result=0;device.FrameResult=-1;service.SetMode(BackdropMode.Mica);
  Check(service.AppliedMode==BackdropMode.Off&&ReferenceEquals(window.Background,windowBrush),"frame extension failure restores opaque background");
  device.FrameResult=0;device.Blocked="透明効果OFF";service.SetMode(BackdropMode.Acrylic);
  Check(service.AppliedMode==BackdropMode.Off&&service.Status.Contains("透明効果OFF"),"OS policy fallback explains unavailable material");
  device.Blocked=null;service.Refresh();Check(service.AppliedMode==BackdropMode.Acrylic,"policy recovery can reapply requested mode");
  service.QueueRefresh();window.Close();int after=device.Calls;Pump(70);
  Check(device.Calls==after,"closing cancels delayed backdrop work");service.Dispose();
 }
}
}
