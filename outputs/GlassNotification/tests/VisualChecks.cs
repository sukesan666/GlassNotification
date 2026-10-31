using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;
namespace Glass {
public static partial class Tests {
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
 // Pattern fixture only. The actual delivered EXE runs with separate settings.
 static partial void Visual() {
  string live=Path.GetFullPath(Path.Combine(Root,"../../work/live"));Directory.CreateDirectory(Path.Combine(live,"assets"));
  File.Copy(Path.Combine(Root,"GlassNotification.exe"),Path.Combine(live,"GlassNotification.exe"),true);
  File.Copy(Path.Combine(Root,"GlassNotification.exe.config"),Path.Combine(live,"GlassNotification.exe.config"),true);
  File.Copy(Path.Combine(Root,"assets/chime.wav"),Path.Combine(live,"assets/chime.wav"),true);
  foreach(string file in Directory.GetFiles(Root,"*.dll"))File.Copy(file,Path.Combine(live,Path.GetFileName(file)),true);
  foreach(string file in Directory.GetFiles(Root,"*.pri"))File.Copy(file,Path.Combine(live,Path.GetFileName(file)),true);
  if(!File.Exists(Path.Combine(live,"settings.json")))File.WriteAllText(Path.Combine(live,"settings.json"),"{\"Width\":480,\"Height\":120,\"Margin\":56,\"SoundKind\":0,\"Backdrop\":\"Acrylic\"}");
  var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
  var host=new Window {Title="Glass — comparison pattern (test fixture)",WindowState=WindowState.Maximized,Background=Brushes.White};
  var grid=new Grid();var columns=new Border[12];int phase=0;
  for(int i=0;i<12;i++)grid.ColumnDefinitions.Add(new ColumnDefinition());
  Action paint=delegate {for(int i=0;i<columns.Length;i++)if(columns[i]!=null)columns[i].Background=new SolidColorBrush((i+phase)%2==0?Color.FromRgb(33,93,180):Color.FromRgb(235,104,60));};
  for(int i=0;i<12;i++) {
   var b=new Border();columns[i]=b;Grid.SetColumn(b,i);var panel=new StackPanel();
   for(int j=0;j<18;j++)panel.Children.Add(new TextBlock {Text="GLASS ▪ ▪ ▪",FontSize=20,Foreground=Brushes.White,Margin=new Thickness(0,12,0,12)});
   b.Child=panel;grid.Children.Add(b);
  }paint();
  var controls=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom,Background=Brushes.White};Grid.SetColumnSpan(controls,12);grid.Children.Add(controls);
  var shift=new Button {Content="背景の模様を切替",Padding=new Thickness(18,10,18,10)};
  var move=new Button {Content="背景の動きを開始／停止",Padding=new Thickness(18,10,18,10)};
  var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(700)};timer.Tick+=delegate {phase++;paint();};
  shift.Click+=delegate {phase++;paint();};move.Click+=delegate {if(timer.IsEnabled)timer.Stop();else timer.Start();};
  controls.Children.Add(shift);controls.Children.Add(move);controls.Children.Add(new TextBlock {Text="上の設定画面は配布用EXEです。個人設定は変更しません。",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(15,0,0,0)});
  host.Content=grid;Process child=null;
  var recorded=new HashSet<string>();var diagnosticTime=Stopwatch.StartNew();
  var diagnostic=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(800)};
  File.WriteAllText(Path.Combine(live,"notification-state.jsonl"),"");
  diagnostic.Tick+=delegate {if(child==null||child.HasExited||diagnosticTime.Elapsed.TotalMinutes>5){diagnostic.Stop();return;}RecordNotificationState(child,live,recorded);};
  var measure=new Button {Content="待機30秒を計測",Padding=new Thickness(18,10,18,10)};controls.Children.Add(measure);
  var measurement=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};int samples=0,handlesBefore=0;double cpuBefore=0;string mode="";Stopwatch elapsed=null;
  measure.Click+=delegate {
   if(child==null||child.HasExited||measurement.IsEnabled)return;
   string warning;mode=SettingsStore.Load(Path.Combine(live,"settings.json"),out warning).Backdrop;
   child.Refresh();cpuBefore=child.TotalProcessorTime.TotalMilliseconds;handlesBefore=child.HandleCount;
   samples=0;elapsed=Stopwatch.StartNew();measure.Content="計測中…操作せずお待ちください";measurement.Start();
  };
  measurement.Tick+=delegate {
   if(child==null||child.HasExited){measurement.Stop();return;}
   if(++samples<30)return;measurement.Stop();child.Refresh();
   var result=new {Mode=mode,Seconds=elapsed.Elapsed.TotalSeconds,CpuMs=child.TotalProcessorTime.TotalMilliseconds-cpuBefore,LogicalProcessors=Environment.ProcessorCount,HandlesBefore=handlesBefore,HandlesAfter=child.HandleCount,PrivateBytes=child.PrivateMemorySize64,Gpu="未計測"};
   File.WriteAllText(Path.Combine(live,"perf-"+mode+".json"),new JavaScriptSerializer().Serialize(result));measure.Content=mode+" 計測完了（30秒）";
  };
  host.Loaded+=delegate {child=Process.Start(new ProcessStartInfo(Path.Combine(live,"GlassNotification.exe")) {WorkingDirectory=live});diagnostic.Start();};
  host.Closed+=delegate {timer.Stop();measurement.Stop();diagnostic.Stop();if(child!=null){if(!child.HasExited)child.CloseMainWindow();child.Dispose();}};
  app.Run(host);
 }
}
}
