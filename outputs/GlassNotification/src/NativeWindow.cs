using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
namespace Glass {
public sealed class MonitorInfo {
 public string Id,Label; public Rect WorkAreaPx,ScreenBoundsPx; public double DpiScale; public bool IsPrimary;
 public Size AvailableDip {get{return new Size(WorkAreaPx.Width/DpiScale,WorkAreaPx.Height/DpiScale);}}
 public Rect ToPixels(Rect dip) {return new Rect(Math.Round(WorkAreaPx.X+dip.X*DpiScale),Math.Round(WorkAreaPx.Y+dip.Y*DpiScale),Math.Round(dip.Width*DpiScale),Math.Round(dip.Height*DpiScale));}
 public override string ToString() {return Label;}
}
public static class NativeWindow {
 [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MONITORINFO {public int Size; public RECT Monitor,Work; public int Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;}
 delegate bool MonitorProc(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr rect,MonitorProc proc,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
 [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint color,byte alpha,uint flags);
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
 public static MonitorInfo[] GetMonitors() {
  var list=new List<MonitorInfo>();
  EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr m,IntPtr dc,IntPtr rc,IntPtr data) {
   var info=new MONITORINFO {Size=Marshal.SizeOf(typeof(MONITORINFO))}; GetMonitorInfo(m,ref info);
   uint x=96,y=96; try {GetDpiForMonitor(m,0,out x,out y);} catch(EntryPointNotFoundException) {} catch(DllNotFoundException) {}
   list.Add(new MonitorInfo {Id=info.Device,Label=(list.Count+1)+"  "+info.Device+((info.Flags&1)!=0?"（メイン）":""),IsPrimary=(info.Flags&1)!=0,DpiScale=Math.Max(1,x)/96.0,ScreenBoundsPx=new Rect(info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top),WorkAreaPx=new Rect(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top)}); return true;
  },IntPtr.Zero);
  list.Sort(delegate(MonitorInfo a,MonitorInfo b){return b.IsPrimary.CompareTo(a.IsPrimary);}); return list.ToArray();
 }
 public static Rect GetBounds(IntPtr handle) {RECT r; GetWindowRect(handle,out r); return new Rect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
 public static void NonActivating(IntPtr handle) {SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000000|0x80);}
 public static void Place(IntPtr handle,Rect px) {
  SetWindowPos(handle,new IntPtr(-1),(int)px.X,(int)px.Y,(int)px.Width,(int)px.Height,0x10|0x40);
 }
  public static void SlideMove(IntPtr handle,int x,int y) {
   SetWindowPos(handle,IntPtr.Zero,x,y,0,0,0x0001|0x0004|0x0010|0x0200|0x0400);
  }
 public static bool ConfigureCorners(IntPtr handle,bool rounded) {
  // A custom HRGN prevents DWM rounding and does not clip the system material.
  // Use a rectangular HWND; DWM clips both its backdrop and client consistently.
  SetWindowRgn(handle,IntPtr.Zero,true);int preference=rounded?2:1;
  try{
   // DWMWA_COLOR_NONE suppresses Windows' automatic light border while keeping rounded corners.
   int noBorder=unchecked((int)0xFFFFFFFE);int borderResult=DwmSetWindowAttribute(handle,34,ref noBorder,4);
   int cornerResult=DwmSetWindowAttribute(handle,33,ref preference,4);
   return borderResult>=0&&cornerResult>=0;
  }
  catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}
 }
 public static void Alpha(IntPtr handle,double alpha) {
  int style=GetWindowLong(handle,-20);
  if(alpha>=.999) {if((style&0x80000)!=0)SetWindowLong(handle,-20,style&~0x80000); return;}
  if((style&0x80000)==0)SetWindowLong(handle,-20,style|0x80000); SetLayeredWindowAttributes(handle,0,(byte)Math.Max(0,Math.Min(255,alpha*255)),2);
 }
}
}
