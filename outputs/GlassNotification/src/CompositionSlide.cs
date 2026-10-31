using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Glass {
internal sealed class CompositionSlideRequest {
 public IntPtr Owner;public FrameworkElement Content;public Rect From,To;
 public double Scale,Seconds,Radius;public bool Entering,Acrylic;public int Strength;
 public string CardColor=NotificationColors.Default;
 public long Token;
}
internal interface ICompositionSlide : IDisposable {
 Rect CurrentBounds {get;}
 bool IsVisible {get;}
 void Update(FrameworkElement content);
 void RevealOwner();
 void SynchronizeOwner();
}
// Upload the measured card once; the Windows compositor owns all intermediate frames.
internal sealed class NativeCompositionSlide : ICompositionSlide {
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
 [DllImport("GlassBackdrop.dll")] static extern int BeginCompositionSlideV3(IntPtr owner,long token,int fromX,int toX,int y,int width,int height,int durationMs,int entering,int acrylic,int strength,int radius,int customColor,byte red,byte green,byte blue,int stride,byte[] pixels,out IntPtr result);
 [DllImport("GlassBackdrop.dll")] static extern int PaintSlideContent(IntPtr slide,int width,int height,int stride,byte[] pixels);
 [DllImport("GlassBackdrop.dll")] static extern int ReadSlideBounds(IntPtr slide,out RECT bounds);
 [DllImport("GlassBackdrop.dll")] static extern void EndCompositionSlide(IntPtr slide);
 [DllImport("GlassBackdrop.dll")] static extern int SlideVisibility(IntPtr slide);
 [DllImport("GlassBackdrop.dll")] static extern int RevealSlideOwner(IntPtr slide);
 [DllImport("GlassBackdrop.dll")] static extern int WaitForSlideOwner(IntPtr slide);
 IntPtr handle;readonly CompositionSlideRequest request;
 NativeCompositionSlide(CompositionSlideRequest request){this.request=request;}
 static byte[] Capture(FrameworkElement content,int width,int height,double scale){
  var bitmap=new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32);
  bitmap.Render(content);var pixels=new byte[checked(width*height*4)];bitmap.CopyPixels(pixels,width*4,0);return pixels;
 }
 internal static ICompositionSlide TryStart(CompositionSlideRequest request){
  var slide=new NativeCompositionSlide(request);
  try{
   int width=(int)Math.Round(request.To.Width),height=(int)Math.Round(request.To.Height);
   var pixels=Capture(request.Content,width,height,request.Scale);
   string selected=NotificationColors.Normalize(request.CardColor);var color=NotificationColors.Color(selected,Colors.White);
   int hr=BeginCompositionSlideV3(request.Owner,request.Token,(int)Math.Round(request.From.X),(int)Math.Round(request.To.X),(int)Math.Round(request.To.Y),width,height,(int)Math.Round(request.Seconds*1000),request.Entering?1:0,request.Acrylic?1:0,request.Strength,(int)System.Math.Round(request.Radius*request.Scale),selected==NotificationColors.Default?0:1,color.R,color.G,color.B,width*4,pixels,out slide.handle);
   if(hr>=0&&slide.handle!=IntPtr.Zero&&slide.IsVisible)return slide;
  }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}catch(ExternalException){}catch(InvalidOperationException){}
  slide.Dispose();return null;
 }
 public Rect CurrentBounds {
  get{RECT bounds;if(handle!=IntPtr.Zero&&ReadSlideBounds(handle,out bounds)>=0)return new Rect(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top);return request.To;}
 }
 public bool IsVisible {get{return handle!=IntPtr.Zero&&SlideVisibility(handle)>=0;}}
 public void Update(FrameworkElement content){
  if(handle==IntPtr.Zero)return;
  int width=(int)Math.Round(request.To.Width),height=(int)Math.Round(request.To.Height);
  int hr=PaintSlideContent(handle,width,height,width*4,Capture(content,width,height,request.Scale));
  if(hr<0)Marshal.ThrowExceptionForHR(hr);
 }
 public void Dispose(){if(handle==IntPtr.Zero)return;var old=handle;handle=IntPtr.Zero;EndCompositionSlide(old);}
 public void RevealOwner(){int hr=RevealSlideOwner(handle);if(hr<0)Marshal.ThrowExceptionForHR(hr);}
 public void SynchronizeOwner(){int hr=WaitForSlideOwner(handle);if(hr<0)Marshal.ThrowExceptionForHR(hr);}
}
}
