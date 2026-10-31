using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
namespace Glass {
public static partial class Tests {
 delegate bool ThreadWindowVisitor(IntPtr hwnd,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
 [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,ThreadWindowVisitor visitor,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int count);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd,uint command);
 static void RecordNotificationState(Process child,string live,HashSet<string> recorded) {
  child.Refresh();IntPtr main=child.MainWindowHandle;if(main==IntPtr.Zero)return;
  uint mainProcess;uint thread=GetWindowThreadProcessId(main,out mainProcess);
  if(mainProcess!=(uint)child.Id)return;
  // Read ONLY the child application created by this fixture. No activation or style changes.
  EnumThreadWindows(thread,delegate(IntPtr hwnd,IntPtr unused) {
   uint process;GetWindowThreadProcessId(hwnd,out process);
   if(process!=(uint)child.Id||!IsWindowVisible(hwnd))return true;
   var title=new StringBuilder(128);GetWindowText(hwnd,title,title.Capacity);
   if(title.ToString()!="Glass Notification Card")return true;
   string warning;var settings=SettingsStore.Load(Path.Combine(live,"settings.json"),out warning);
   int type,corner;int typeResult=DwmGetWindowAttribute(hwnd,38,out type,4),cornerResult=DwmGetWindowAttribute(hwnd,33,out corner,4);
   int style=GetWindowLong(hwnd,-20);var bounds=NativeWindow.GetBounds(hwnd);
   bool foregroundIsMain=NativeWindow.GetForegroundWindow()==main;
   string key=settings.Backdrop+"/"+settings.RoundedCorners+"/"+settings.FontFamilyName+"/"+bounds.Width+"/"+bounds.Height+"/"+style+"/"+foregroundIsMain;
   if(!recorded.Add(key))return true;
   IntPtr region=CreateRectRgn(0,0,0,0);int regionKind;try{regionKind=GetWindowRgn(hwnd,region);}finally{DeleteObject(region);}
   var snapshot=new {Utc=DateTime.UtcNow.ToString("o"),RequestedMode=settings.Backdrop,settings.RoundedCorners,settings.FontFamilyName,BackdropType=type,BackdropReadHResult=typeResult,CornerPreference=corner,CornerReadHResult=cornerResult,CustomRegionKind=regionKind,NoActivate=(style&0x08000000)!=0,Topmost=(style&8)!=0,Layered=(style&0x80000)!=0,OwnerIsMain=GetWindow(hwnd,4)==main,ForegroundIsMain=foregroundIsMain,X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height};
   File.AppendAllText(Path.Combine(live,"notification-state.jsonl"),new JavaScriptSerializer().Serialize(snapshot)+Environment.NewLine);
   return true;
  },IntPtr.Zero);
 }
}
}
