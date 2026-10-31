using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace Glass {
internal sealed class PackageRuntime {
 const int InsufficientBuffer=122;
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int GetCurrentPackagePath(ref uint length,StringBuilder path);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int GetCurrentApplicationUserModelId(ref uint length,StringBuilder applicationUserModelId);
 static class CurrentContext {internal static readonly PackageRuntime Value=CreateCurrent();}
 public static PackageRuntime Current {get{return CurrentContext.Value;}}
 public string BaseDirectory {get;private set;}
 public bool IsInstalledPackage {get;private set;}
 public string SettingsPath {get;private set;}
 public string ChimePath {get;private set;}
 public string ApplicationUserModelId {get;private set;}
 public PackageRuntime(string baseDirectory,string packagePath,string localApplicationData,string applicationUserModelId) {
  BaseDirectory=Path.GetFullPath(baseDirectory);IsInstalledPackage=IsInsidePackage(BaseDirectory,packagePath);ApplicationUserModelId=applicationUserModelId;
  // Sparse identity has a package path too, but its executable still runs from the external portable directory.
  string settingsDirectory=IsInstalledPackage?Path.Combine(localApplicationData,"GlassNotification.OSLink"):BaseDirectory;
  SettingsPath=Path.Combine(settingsDirectory,"settings.json");ChimePath=Path.Combine(BaseDirectory,"assets","chime.wav");
 }
 public void Prepare() {
  if(IsInstalledPackage)Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
  else if(!File.Exists(ChimePath))SoundService.CreateChime(ChimePath);
 }
 static bool IsInsidePackage(string baseDirectory,string packagePath) {
  if(string.IsNullOrWhiteSpace(packagePath))return false;
  string directory=Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  string package=Path.GetFullPath(packagePath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  return directory.Equals(package,StringComparison.OrdinalIgnoreCase)||directory.StartsWith(package+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 }
 static PackageRuntime CreateCurrent() {
  return new PackageRuntime(AppDomain.CurrentDomain.BaseDirectory,ReadPackagePath(),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),ReadApplicationUserModelId());
 }
 static string ReadPackagePath() {
  try {uint length=0;if(GetCurrentPackagePath(ref length,null)!=InsufficientBuffer||length==0)return null;var value=new StringBuilder((int)length);return GetCurrentPackagePath(ref length,value)==0?value.ToString():null;}
  catch(EntryPointNotFoundException){return null;}catch(DllNotFoundException){return null;}
 }
 static string ReadApplicationUserModelId() {
  try {uint length=0;if(GetCurrentApplicationUserModelId(ref length,null)!=InsufficientBuffer||length==0)return null;var value=new StringBuilder((int)length);return GetCurrentApplicationUserModelId(ref length,value)==0?value.ToString():null;}
  catch(EntryPointNotFoundException){return null;}catch(DllNotFoundException){return null;}
 }
}
}
