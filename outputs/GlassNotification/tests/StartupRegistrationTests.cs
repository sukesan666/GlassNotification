using System;
using System.IO;
namespace Glass {
public static partial class Tests {
 sealed class StartupCommandLog : IStartupCommandStore {
  public string Name,Command;public int Sets,Removes;
  public void Set(string name,string command){Name=name;Command=command;Sets++;}
  public void Remove(string name){Name=name;Removes++;}
 }
 sealed class StartupRegistrationLog : IStartupRegistration {
  public bool Enabled;public int Enables,Disables;
  public void SetEnabled(bool enabled){Enabled=enabled;if(enabled)Enables++;else Disables++;}
 }
 static partial void StartupRegistrationTests(){
  var store=new StartupCommandLog();var startup=new WindowsStartupRegistration(store,@"C:\Users\Test User\GlassNotification.exe");
  startup.SetEnabled(true);Check(store.Name=="GlassNotification"&&store.Command==@"""C:\Users\Test User\GlassNotification.exe"""&&store.Sets==1,"Windows startup writes a quoted current-user Run command");
  startup.SetEnabled(false);Check(store.Name=="GlassNotification"&&store.Removes==1&&store.Sets==1,"Windows startup disable removes only the app's Run value");
  const string appId="GlassNotification.OSLink_fixture!App";
  var installed=new PackageRuntime(@"C:\Program Files\WindowsApps\GlassNotification.OSLink_1.3.0.0_x64__fixture",@"C:\Program Files\WindowsApps\GlassNotification.OSLink_1.3.0.0_x64__fixture",@"C:\Fixture\LocalAppData",appId);
  var upgraded=new PackageRuntime(@"C:\Program Files\WindowsApps\GlassNotification.OSLink_1.4.0.0_x64__fixture",@"C:\Program Files\WindowsApps\GlassNotification.OSLink_1.4.0.0_x64__fixture",@"C:\Fixture\LocalAppData",appId);
  var packageStore=new StartupCommandLog();new WindowsStartupRegistration(packageStore,Path.Combine(installed.BaseDirectory,"GlassNotification.Linked.exe"),installed).SetEnabled(true);
  string installedCommand=packageStore.Command;
  Check(installedCommand.EndsWith(@"\explorer.exe"" ""shell:AppsFolder\GlassNotification.OSLink_fixture!App""",StringComparison.OrdinalIgnoreCase)&&installedCommand.IndexOf("WindowsApps",StringComparison.OrdinalIgnoreCase)<0,"installed startup activates the stable AppsFolder identity instead of a versioned executable");
  new WindowsStartupRegistration(packageStore,Path.Combine(upgraded.BaseDirectory,"GlassNotification.Linked.exe"),upgraded).SetEnabled(true);
  Check(packageStore.Command==installedCommand,"package upgrades preserve the exact startup activation command");
  new WindowsStartupRegistration(packageStore,Path.Combine(upgraded.BaseDirectory,"GlassNotification.Linked.exe"),upgraded).SetEnabled(false);
  Check(packageStore.Removes==1&&packageStore.Sets==2,"installed startup disable removes the app value without activating it");
  var sparse=new PackageRuntime(@"C:\Portable",installed.BaseDirectory,@"C:\Fixture\LocalAppData",appId);
  new WindowsStartupRegistration(packageStore,@"C:\Portable\GlassNotification.Linked.exe",sparse).SetEnabled(true);
  Check(packageStore.Command==@"""C:\Portable\GlassNotification.Linked.exe""","sparse external startup keeps the current executable command despite having package identity");
  bool missingIdentity=false;
  try {new WindowsStartupRegistration(packageStore,@"C:\Program Files\WindowsApps\GlassNotification.Linked.exe",new PackageRuntime(installed.BaseDirectory,installed.BaseDirectory,@"C:\Fixture\LocalAppData",null)).SetEnabled(true);}catch(InvalidOperationException){missingIdentity=true;}
  Check(missingIdentity,"installed startup without an activation identity fails instead of persisting a versioned package path");
 }
}
}
