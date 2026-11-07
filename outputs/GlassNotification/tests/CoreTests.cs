using System;
using System.IO;
using System.Windows;
namespace Glass {
public static partial class Tests {
 static int count;
 public static void Check(bool value, string message) { if(!value) throw new Exception(message); count++; }
 [STAThread] public static int Main(string[] args) {
  try { string mode=args.Length>0?args[0]:"All"; Root=args.Length>1?args[1]:AppDomain.CurrentDomain.BaseDirectory;
   if(mode=="Visual") {Visual();return 0;}
   if(mode!="UI") Core();
   if(mode!="Core") Ui();
   Console.WriteLine("PASS: "+count+" assertions ("+mode+")"); return 0;
  } catch(Exception e) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-failure.txt"),e.ToString()); Console.WriteLine("FAIL: "+e); Console.Error.WriteLine(e); return 1; }
 }
 public static string Root;
 static void Core() {
  string folder=Path.GetFullPath(Path.Combine(Root,"../../work/GlassTests-"+Guid.NewGuid())); Directory.CreateDirectory(folder);
  try {
   string path=Path.Combine(folder,"settings.json"),warning; var s=SettingsStore.Load(path,out warning);
   Check(s.Width==360&&s.Height==88&&s.FontSize==13&&s.Margin==20,"defaults");
   Check(s.GlassEffectEnabled,"glass enabled by default");
   Check(s.DisplaySeconds==4,"legacy/default settings retain four-second duration");
   Check(!s.HighContrast,"high contrast defaults off for existing settings");
   Check(s.UiScalePercent==100&&new AppSettings {UiScalePercent=150}.Normalize().UiScalePercent==150&&new AppSettings {UiScalePercent=200}.Normalize().UiScalePercent==150&&new AppSettings {UiScalePercent=50}.Normalize().UiScalePercent==100,"app UI scale defaults to 100 percent and stays within 100 to 150 percent");
   Check(s.ThemeMode=="System"&&s.Language=="Japanese"&&new AppSettings {ThemeMode="bad",Language="bad"}.Normalize().ThemeMode=="System"&&new AppSettings {ThemeMode="bad",Language="bad"}.Normalize().Language=="Japanese","theme follows system and Japanese is the safe default");
   Check(UiLocalization.Translate("テスト通知元 — 通知を表示","English")=="テスト通知元 — Show notifications"&&UiLocalization.Translate("テスト通知元 — Show notifications","Japanese")=="テスト通知元 — 通知を表示","dynamic notification source labels translate without changing the source name");
   Check(new AppSettings {DisplaySeconds=1000}.Normalize().DisplaySeconds==60&&new AppSettings {DisplaySeconds=0}.Normalize().DisplaySeconds==1,"duration stays within supported bounds");
   Check(new AppSettings {DisplaySeconds=double.NaN}.Normalize().DisplaySeconds==4,"non-finite duration restores safe default");
   ImageSizeSettingsTests(path);
   ImagePositionSettingsTests(path);
   BackdropSettingsTests(path);
   TextOutlineSettingsTests(path);
   File.WriteAllText(path,"bad json"); s=SettingsStore.Load(path,out warning); Check(s.Width==360&&warning!=null,"corrupt settings");
   File.WriteAllText(path,"{\"Width\":480}"); s=SettingsStore.Load(path,out warning); Check(s.Width==480&&s.FontSize==13,"missing fields preserve defaults");
   Check(s.GlassEffectEnabled,"old settings without glass flag keep existing appearance");
   s.GlassEffectEnabled=false;Check(SettingsStore.Save(path,s,out warning),"save glass preference");s=SettingsStore.Load(path,out warning);Check(!s.GlassEffectEnabled&&s.Width==480,"glass off restored with other settings");
   s.HighContrast=true;Check(SettingsStore.Save(path,s,out warning),"save high contrast preference");s=SettingsStore.Load(path,out warning);Check(s.HighContrast,"high contrast preference restored from settings");
   s.ThemeMode="Dark";s.Language="English";Check(SettingsStore.Save(path,s,out warning),"save theme and language");s=SettingsStore.Load(path,out warning);Check(s.ThemeMode=="Dark"&&s.Language=="English","theme and language preferences restore from settings");
   s.Width=900; s.FontSize=-1; s.SoundKind=(SoundKind)99; s.Corner=(Corner)99;
   bool saved=SettingsStore.Save(path,s,out warning); Check(saved,"save: "+warning); s=SettingsStore.Load(path,out warning);
   Check(s.Width==600&&s.FontSize==11&&s.Corner==Corner.TopRight&&s.SoundKind==SoundKind.None,"normalize values");
   Check(!SettingsStore.Save(folder,s,out warning)&&warning!=null,"save failure nonfatal");
   File.WriteAllText(path,"{\"SoundKind\":2,\"SoundPath\":\"bad\\u0000path\"}");
   var brokenSound=SettingsStore.Load(path,out warning);Check(brokenSound.SoundPath==""&&warning!=null,"malformed sound path is recoverable");
   var d=new AppSettings(); var r=Layout.Resolve(d,new Size(1920,1080),88);
   Check(r.Bounds==new Rect(1540,20,360,88),"top right");
   d.Corner=Corner.BottomLeft; r=Layout.Resolve(d,new Size(1920,1080),88); Check(r.Bounds==new Rect(20,972,360,88),"bottom left");
   d.Corner=Corner.TopLeft; Check(Layout.Resolve(d,new Size(1920,1080),88).Bounds.Top==20,"top left");
   d.Corner=Corner.BottomRight; Check(Layout.Resolve(d,new Size(1920,1080),88).Bounds.Right==1900,"bottom right");
   d.Corner=Corner.TopRight; Check(Layout.Resolve(d,new Size(1920,1080),88,98).Bounds==new Rect(1540,118,360,88)&&Layout.Resolve(d,new Size(1920,1080),88,196).Bounds==new Rect(1540,216,360,88),"top right stacks downward");
   d.Corner=Corner.TopLeft; Check(Layout.Resolve(d,new Size(1920,1080),88,98).Bounds==new Rect(20,118,360,88)&&Layout.Resolve(d,new Size(1920,1080),88,196).Bounds==new Rect(20,216,360,88),"top left stacks downward");
   d.Corner=Corner.BottomRight; Check(Layout.Resolve(d,new Size(1920,1080),88,98).Bounds==new Rect(1540,874,360,88)&&Layout.Resolve(d,new Size(1920,1080),88,196).Bounds==new Rect(1540,776,360,88),"bottom right stacks upward");
   d.Corner=Corner.BottomLeft; Check(Layout.Resolve(d,new Size(1920,1080),88,98).Bounds==new Rect(20,874,360,88)&&Layout.Resolve(d,new Size(1920,1080),88,196).Bounds==new Rect(20,776,360,88),"bottom left stacks upward");
   d.Corner=Corner.BottomRight;
   Check(Layout.Resolve(d,new Size(1920,1080),119).Settings.Height==120,"minimum height rounded");
   d.Margin=120; d.Width=600; r=Layout.Resolve(d,new Size(500,300),88); Check(r.Bounds.Right<=500&&r.Settings.Margin==0&&r.Settings.Width==500,"fit margin before card");
   bool tooSmall=false; try { Layout.Resolve(d,new Size(200,50),88); } catch(InvalidOperationException) {tooSmall=true;} Check(tooSmall,"unusable area");
   SoundTests(folder);
   ControllerTests();
   PackageRuntimeTests(folder);
   StartupRegistrationTests();
   AppRuntimeStateTests(folder);
   NotificationSenderFilterTests(folder);
   NotificationImageTests(folder);
   NotificationHistoryTests();
   OsNotificationTests(folder);
   NotificationFeedTests();
  } finally { Directory.Delete(folder,true); }
 }
 static void ImageSizeSettingsTests(string path) {
  string warning;var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
  File.WriteAllText(path,"{\"Width\":480,\"ImageSize\":96}");
  var settings=SettingsStore.Load(path,out warning);
  Check(settings.ImageSize==96,"custom content image size survives settings load");
  Check(SettingsStore.Save(path,settings,out warning),"save custom content image size");
  var fields=serializer.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(path));
  Check(Convert.ToDouble(fields["ImageSize"])==96,"custom content image size is persisted");
  settings=SettingsStore.Load(path,out warning);
  Check(settings.ImageSize==96,"saved custom content image size reloads");
  foreach(string json in new[]{"{}","{\"Width\":480}"}) {
   File.WriteAllText(path,json);settings=SettingsStore.Load(path,out warning);
   Check(settings.ImageSize==56,"new and legacy settings keep the existing thumbnail size");
  }
  double[] requested={0,25,27,130,double.NaN,double.PositiveInfinity,double.NegativeInfinity};
  double[] expected={24,24,28,128,56,56,56};
  for(int i=0;i<requested.Length;i++) {
   settings=new AppSettings {ImageSize=requested[i]};
   Check(settings.Normalize().ImageSize==expected[i],"content image size normalizes bounds, step and non-finite values: "+requested[i]);
  }
  foreach(string invalid in new[]{"\"NaN\"","\"Infinity\"","\"invalid\"","null"}) {
   File.WriteAllText(path,"{\"Width\":480,\"ImageSize\":"+invalid+"}");settings=SettingsStore.Load(path,out warning);
   Check(settings.Width==480&&settings.ImageSize==56&&warning!=null,"invalid stored image size falls back without losing other settings: "+invalid);
  }
 }
 static void ImagePositionSettingsTests(string path) {
  string warning;var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
  File.WriteAllText(path,"{\"Width\":480,\"ImagePosition\":\"left\"}");
  var settings=SettingsStore.Load(path,out warning);
  Check(settings.ImagePosition=="Left"&&settings.Width==480,"image position loads and normalizes a case-insensitive left value without losing other settings");
  Check(SettingsStore.Save(path,settings,out warning),"save left image position");
  var fields=serializer.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(path));
  Check(Convert.ToString(fields["ImagePosition"])=="Left","left image position is persisted");
  settings=SettingsStore.Load(path,out warning);
  Check(settings.ImagePosition=="Left","saved left image position reloads");
  foreach(string requested in new[]{"Right","rIgHt","Left","lEfT","unknown",null}) {
   settings=new AppSettings {ImagePosition=requested};
   string expected=requested=="Left"||requested=="lEfT"?"Left":"Right";
   Check(settings.Normalize().ImagePosition==expected,"image position normalizes known and invalid choices: "+requested);
  }
  foreach(string json in new[]{"{}","{\"Width\":480}","{\"ImagePosition\":null}","{\"ImagePosition\":\"unknown\"}"}) {
   File.WriteAllText(path,json);settings=SettingsStore.Load(path,out warning);
   Check(settings.ImagePosition=="Right","new, legacy and invalid settings use right image position");
  }
 }
 static partial void SoundTests(string folder);
 static partial void ControllerTests();
 static partial void StartupRegistrationTests();
 static partial void PackageRuntimeTests(string folder);
 static partial void AppRuntimeStateTests(string folder);
 static partial void NotificationSenderFilterTests(string folder);
 static partial void NotificationHistoryTests();
 static partial void Ui();
 static partial void Visual();
}
}
