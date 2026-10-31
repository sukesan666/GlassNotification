using System;
using System.IO;
namespace Glass {
public static partial class Tests {
 static partial void PackageRuntimeTests(string folder) {
  string root=Path.Combine(folder,"pkg"),localData=Path.Combine(root,"LocalAppData");
  string package=Path.Combine(root,"1.3.0.0"),portable=Path.Combine(root,"portable");
  Directory.CreateDirectory(package);Directory.CreateDirectory(portable);
  string packageSettings=Path.Combine(package,"settings.json");
  const string untouched="{\"Width\":590,\"Language\":\"English\"}";File.WriteAllText(packageSettings,untouched);
  var installed=new PackageRuntime(package,package,localData,"GlassNotification.OSLink_fixture!App");
  Check(installed.IsInstalledPackage&&installed.SettingsPath==Path.Combine(localData,"GlassNotification.OSLink","settings.json"),"installed package chooses version-independent writable user settings");
  installed.Prepare();
  string warning;var initial=SettingsStore.Load(installed.SettingsPath,out warning);
  Check(initial.Width==360&&initial.Language=="Japanese"&&!File.Exists(installed.SettingsPath),"installed package starts with defaults without copying adjacent package settings");
  Check(SettingsStore.Save(installed.SettingsPath,new AppSettings {Width=450,Language="English"},out warning),"installed settings can be saved in the selected user directory: "+warning);
  var restored=SettingsStore.Load(installed.SettingsPath,out warning);
  Check(restored.Width==450&&restored.Language=="English","installed settings persist and reload from the writable user path");
  Check(File.ReadAllText(packageSettings)==untouched&&!Directory.Exists(Path.Combine(package,"assets")),"installed preparation neither changes package settings nor generates assets in the immutable package");
  var upgraded=new PackageRuntime(Path.Combine(root,"1.4.0.0"),Path.Combine(root,"1.4.0.0"),localData,"GlassNotification.OSLink_fixture!App");
  Check(upgraded.SettingsPath==installed.SettingsPath&&SettingsStore.Load(upgraded.SettingsPath,out warning).Width==450,"a package upgrade reuses the prior version's saved settings");
  var unpackaged=new PackageRuntime(portable,null,localData,null);
  string portableSettings=Path.Combine(portable,"settings.json");File.WriteAllText(portableSettings,untouched);
  unpackaged.Prepare();
  Check(!unpackaged.IsInstalledPackage&&unpackaged.SettingsPath==portableSettings&&File.ReadAllText(portableSettings)==untouched,"portable preparation retains adjacent settings byte for byte");
  Check(SoundService.Validate(unpackaged.ChimePath).IsValid,"portable preparation can still create a missing standard chime beside the executable");
  var sparse=new PackageRuntime(portable,package,localData,"GlassNotification.OSLink_fixture!App");
  Check(!sparse.IsInstalledPackage&&sparse.SettingsPath==portableSettings,"sparse identity with an external executable retains portable settings location");
  var sibling=new PackageRuntime(package+"-external",package,localData,"GlassNotification.OSLink_fixture!App");
  Check(!sibling.IsInstalledPackage,"a shared directory prefix is not mistaken for package containment");
  var nested=new PackageRuntime(Path.Combine(package,"bin")+Path.DirectorySeparatorChar,package+Path.DirectorySeparatorChar,localData,"GlassNotification.OSLink_fixture!App");
  Check(nested.IsInstalledPackage,"executables inside a package subdirectory are detected with trailing path separators");
 }
}
}
