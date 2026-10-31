using System;
using System.Runtime.InteropServices;
namespace Glass {
public interface IBackdropContentNative : IDisposable {
 bool HasContentTarget {get;}
 int DeviceStatus {get;}
 int Present(int width,int height,int stride,byte[] pixels);
 void SetOpacity(double opacity);
}
public interface IBackdropColorNative {
 int SetColor(string value);
}
// Keeps the existing WPF window, layout, input, and .NET Framework runtime.
// Only Acrylic's background and measured foreground are composed by the SDK.
public sealed class SdkBackdropNative : IBackdropNative,IBackdropStrengthNative,IBackdropContentNative,IBackdropColorNative {
 readonly DwmBackdropNative dwm=new DwmBackdropNative();IntPtr context;BackdropStrength strength=BackdropStrength.Standard;
 string cardColor=NotificationColors.Default;
 [DllImport("GlassBackdrop.dll")] static extern int CreateBackdrop(IntPtr hwnd,int strength,out IntPtr result);
 [DllImport("GlassBackdrop.dll")] static extern int SetBackdropStrength(IntPtr value,int strength);
 [DllImport("GlassBackdrop.dll")] static extern int SetBackdropColor(IntPtr value,int custom,byte red,byte green,byte blue);
 [DllImport("GlassBackdrop.dll")] static extern void CloseBackdrop(IntPtr value);
 [DllImport("GlassBackdrop.dll")] static extern int PaintContent(IntPtr value,int width,int height,int stride,byte[] pixels);
 [DllImport("GlassBackdrop.dll")] static extern void ContentOpacity(IntPtr value,float opacity);
 [DllImport("GlassBackdrop.dll",EntryPoint="DeviceStatus")] static extern int NativeDeviceStatus(IntPtr value);
 public bool HasContentTarget {get{return context!=IntPtr.Zero;}}
 public int DeviceStatus {get{return context==IntPtr.Zero?0:NativeDeviceStatus(context);}}
 public string UnavailableReason(){return dwm.UnavailableReason();}
 public int ExtendFrame(IntPtr hwnd,bool enabled){return dwm.ExtendFrame(hwnd,enabled);}
 public int SetType(IntPtr hwnd,int type){
  if(type!=3){Dispose();return dwm.SetType(hwnd,type);}
  if(context!=IntPtr.Zero)return 0;
  // Clear the previous DWM material before connecting the SDK target.
  int hr=dwm.SetType(hwnd,1);if(hr<0)return hr;
  hr=CreateBackdrop(hwnd,(int)strength,out context);
  if(hr<0){
   Dispose();
   return dwm.SetType(hwnd,3);
  }
  return SetColor(cardColor);
 }
 public int SetColor(string value){
  cardColor=NotificationColors.Normalize(value);if(context==IntPtr.Zero)return 0;
  var color=NotificationColors.Color(cardColor,System.Windows.Media.Colors.White);
  return SetBackdropColor(context,cardColor==NotificationColors.Default?0:1,color.R,color.G,color.B);
 }
 public int SetStrength(BackdropStrength value){
  strength=Enum.IsDefined(typeof(BackdropStrength),value)?value:BackdropStrength.Standard;
  return context==IntPtr.Zero?0:SetBackdropStrength(context,(int)strength);
 }
 public int Present(int width,int height,int stride,byte[] pixels){return PaintContent(context,width,height,stride,pixels);}
 public void SetOpacity(double opacity){if(context!=IntPtr.Zero)ContentOpacity(context,(float)Math.Max(0,Math.Min(1,opacity)));}
 public void Dispose(){if(context==IntPtr.Zero)return;var old=context;context=IntPtr.Zero;CloseBackdrop(old);}
}
}
