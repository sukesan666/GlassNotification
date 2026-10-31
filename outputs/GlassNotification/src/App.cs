using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
namespace Glass {
public static class App {
 [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);
 const int AnyProcess=-1;
 // One instance per executable path, so separate builds and the Chrome-linked executable can still run side by side.
 internal static string InstanceName(string executablePath) {
  using(var sha=SHA1.Create()) {
   byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(executablePath).ToLowerInvariant()));
   return "GlassNotification."+BitConverter.ToString(hash,0,8).Replace("-","");
  }
 }
 [STAThread] public static int Main(string[] args) {
  try {
   string name=InstanceName(System.Reflection.Assembly.GetEntryAssembly().Location);
   bool first;
   using(var showRequest=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\"+name+".Show"))
   using(var instance=new Mutex(true,"Local\\"+name,out first)) {
    if(!first) {AllowSetForegroundWindow(AnyProcess);showRequest.Set();return 0;}
    return Run(showRequest);
   }
  }catch(Exception e){MessageBox.Show("起動できませんでした。\n"+e.Message,"Glass Notification",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
 }
 static int Run(EventWaitHandle showRequest) {
  var runtime=PackageRuntime.Current;runtime.Prepare();
  var application=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
  application.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
   MessageBox.Show("試作品を終了します。\n"+e.Exception.Message,"Glass Notification",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;application.Shutdown(1);
  };
  var window=new ControlWindow(runtime.SettingsPath);
  var wait=ThreadPool.RegisterWaitForSingleObject(showRequest,delegate {window.Dispatcher.BeginInvoke(new Action(window.ShowFromSecondLaunch));},null,Timeout.Infinite,false);
  try{application.Run(window);}finally{wait.Unregister(null);}
  return 0;
 }
}
}
