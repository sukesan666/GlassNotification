using System;
using System.IO;
using Microsoft.Win32;
namespace Glass {
internal interface IStartupCommandStore {
 void Set(string name,string command);
 void Remove(string name);
}
internal interface IStartupRegistration {
 void SetEnabled(bool enabled);
}
internal sealed class RegistryStartupCommandStore : IStartupCommandStore {
 const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
 public void Set(string name,string command) {
  using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) {
   if(key==null)throw new IOException("Windowsの自動起動設定を開けませんでした。");
   key.SetValue(name,command,RegistryValueKind.String);key.Flush();
  }
 }
 public void Remove(string name) {
  using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true))if(key!=null)key.DeleteValue(name,false);
 }
}
internal sealed class WindowsStartupRegistration : IStartupRegistration {
 const string ValueName="GlassNotification";
 readonly IStartupCommandStore store;readonly string executable;readonly PackageRuntime runtime;
 public WindowsStartupRegistration(IStartupCommandStore commandStore,string executablePath):this(commandStore,executablePath,null) {}
 public WindowsStartupRegistration(IStartupCommandStore commandStore,string executablePath,PackageRuntime packageRuntime) {
  if(commandStore==null)throw new ArgumentNullException("commandStore");
  if(string.IsNullOrWhiteSpace(executablePath))throw new ArgumentException("実行ファイルのパスが必要です。","executablePath");
  store=commandStore;executable=Path.GetFullPath(executablePath);runtime=packageRuntime;
 }
 public void SetEnabled(bool enabled) {
  if(!enabled){store.Remove(ValueName);return;}
  if(runtime==null||!runtime.IsInstalledPackage){store.Set(ValueName,"\""+executable+"\"");return;}
  if(string.IsNullOrWhiteSpace(runtime.ApplicationUserModelId))throw new InvalidOperationException("インストールされたアプリの起動IDを取得できませんでした。");
  string explorer=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
  store.Set(ValueName,"\""+explorer+"\" \"shell:AppsFolder\\"+runtime.ApplicationUserModelId+"\"");
 }
}
}
