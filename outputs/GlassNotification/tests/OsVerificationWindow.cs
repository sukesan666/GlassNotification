using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
namespace Glass {
// Test-only desktop background and aggregate audio meter; no notification payload logging.
public sealed class OsVerificationWindow : Window {
 [DllImport("GlassNotificationAudio.dll")]static extern int AudioProbeStart(out IntPtr result);
 [DllImport("GlassNotificationAudio.dll")]static extern int AudioProbePoll(IntPtr probe,out double peak,out ulong samples,out double systemPeak,out int systemSessions);
 [DllImport("GlassNotificationAudio.dll")]static extern void AudioProbeEnd(IntPtr probe);
 IntPtr probe;DateTime end;readonly TextBlock report;readonly DispatcherTimer timer;string phase;readonly string folder;
 Process iconChild;readonly DispatcherTimer childStatus;
 public OsVerificationWindow(string output){folder=output;Title="Glass — OS通知の実機検証";WindowState=WindowState.Maximized;Background=new SolidColorBrush(Color.FromRgb(45,76,110));
  var grid=new Grid();for(int i=0;i<12;i++){grid.ColumnDefinitions.Add(new ColumnDefinition());var b=new Border {Background=new SolidColorBrush(i%2==0?Color.FromRgb(33,93,180):Color.FromRgb(235,104,60))};Grid.SetColumn(b,i);grid.Children.Add(b);}
  var controls=new StackPanel {Background=Brushes.White,VerticalAlignment=VerticalAlignment.Bottom};Grid.SetColumnSpan(controls,12);grid.Children.Add(controls);
  report=new TextBlock {Text="OS通知を送信する前に計測を開始してください。音声データは保存せず、出力音の最大値だけを記録します。",Margin=new Thickness(18)};controls.Children.Add(report);
  var row=new StackPanel {Orientation=Orientation.Horizontal};controls.Children.Add(row);foreach(string name in new[]{"before","suppressed","restored"}){var button=new Button {Content=name+"：10秒間の出力音を計測",Padding=new Thickness(15,10,15,10)};string current=name;button.Click+=delegate {Begin(current);};row.Children.Add(button);}
  var launch=new Button {Content="検証用の通知アプリを起動",Padding=new Thickness(15,10,15,10)};row.Children.Add(launch);
  childStatus=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};childStatus.Tick+=delegate {if(iconChild!=null&&iconChild.HasExited){childStatus.Stop();Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"icon-runtime-exit.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {ExitCode=iconChild.ExitCode}));report.Text="通知アプリの終了コード："+iconChild.ExitCode;}};
  launch.Click+=delegate {if(iconChild!=null&&!iconChild.HasExited)return;string path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"../../outputs/GlassNotification/GlassNotification.Linked.exe"));var start=new ProcessStartInfo(path){WorkingDirectory=Path.GetDirectoryName(path),UseShellExecute=false};iconChild=Process.Start(start);report.Text="検証用の通知アプリが起動しました。";childStatus.Start();};
  timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};timer.Tick+=delegate {Poll();};Content=grid;Closed+=delegate {End();childStatus.Stop();if(iconChild!=null){if(!iconChild.HasExited)iconChild.CloseMainWindow();iconChild.Dispose();}};
 }
 void Begin(string name){End();try{Marshal.ThrowExceptionForHR(AudioProbeStart(out probe));phase=name;end=DateTime.UtcNow.AddSeconds(10);report.Text=name+" を計測中。いま連動版からWindows通知を送信してください。";timer.Start();}catch(Exception e){report.Text="音を計測できません："+e.Message;}}
 void Poll(){try{double peak,systemPeak;ulong samples;int systemSessions;Marshal.ThrowExceptionForHR(AudioProbePoll(probe,out peak,out samples,out systemPeak,out systemSessions));report.Text=phase+" 計測中：システム音 "+systemPeak.ToString("0.000000")+" / 出力全体 "+peak.ToString("0.000000");if(DateTime.UtcNow>=end){Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"audio-"+phase+".json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {Phase=phase,Peak=peak,SystemSoundsPeak=systemPeak,SystemSoundSessions=systemSessions,Frames=samples,Seconds=10,Source="Default render endpoint WASAPI loopback float32 and SystemSounds session peak",Microphone=false,AudioSaved=false}));report.Text=phase+" 完了：システム音 "+systemPeak.ToString("0.000000")+" / 出力全体 "+peak.ToString("0.000000");End();}}catch(Exception e){report.Text="計測を停止："+e.Message;End();}}
 void End(){if(timer!=null)timer.Stop();if(probe!=IntPtr.Zero){AudioProbeEnd(probe);probe=IntPtr.Zero;}}
 [STAThread]public static void Main(string[] args){new Application().Run(new OsVerificationWindow(args.Length==0?AppDomain.CurrentDomain.BaseDirectory:args[0]));}
}
}
