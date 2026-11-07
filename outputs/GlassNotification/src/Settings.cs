using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Web.Script.Serialization;
namespace Glass {
public enum Corner { TopRight, TopLeft, BottomRight, BottomLeft }
public enum SoundKind { None, Standard, File }
public enum BackdropMode { Off, Acrylic, Mica }
public enum BackdropStrength { Weak, Standard, Strong }
public sealed class AppSettings {
 public double Width {get;set;} public double Height {get;set;} public double FontSize {get;set;}
 public string MonitorId {get;set;} public Corner Corner {get;set;} public double Margin {get;set;}
 public SoundKind SoundKind {get;set;} public string SoundPath {get;set;}
 public bool StartWithWindows {get;set;} public bool StartMinimizedToTray {get;set;}
 // Retained for old callers/files. The explicit mode is authoritative on load.
 public bool GlassEffectEnabled {get{return Mode!=BackdropMode.Off;}set{Mode=value?BackdropMode.Acrylic:BackdropMode.Off;}}
 public string Backdrop {get;set;}
 public string FontFamilyName {get;set;}
 public bool RoundedCorners {get;set;}
 public string CardColor {get;set;} public string TextColor {get;set;}
 public string TextOutlineColor {get;set;}
 public double DisplaySeconds {get;set;}
 public double ImageSize {get;set;}
 public string ImagePosition {get;set;}
 public bool HighContrast {get;set;}
 public BackdropStrength BackdropStrength {get;set;}
 public double UiScalePercent {get;set;}
 public string ThemeMode {get;set;}
 public string Language {get;set;}
 public int SettingsVersion {get;set;}
 [ScriptIgnore] public BackdropMode Mode {
  get {BackdropMode mode;return Enum.TryParse(Backdrop,true,out mode)&&Enum.IsDefined(typeof(BackdropMode),mode)?mode:BackdropMode.Off;}
  set {Backdrop=Enum.IsDefined(typeof(BackdropMode),value)?value.ToString():"Off";}
 }
 public AppSettings() { Width=360; Height=88; FontSize=13; Margin=20; MonitorId=""; SoundPath=""; GlassEffectEnabled=true;FontFamilyName=NotificationFonts.Default;RoundedCorners=true;CardColor=TextColor=TextOutlineColor=NotificationColors.Default;DisplaySeconds=4;ImageSize=56;ImagePosition="Right";StartWithWindows=false;StartMinimizedToTray=false;HighContrast=false;BackdropStrength=BackdropStrength.Standard;UiScalePercent=100;ThemeMode="System";Language="Japanese"; }
 public AppSettings Copy() { return (AppSettings)MemberwiseClone(); }
 public static double Clamp(double v,double min,double max,double step) {
  if(double.IsNaN(v)||double.IsInfinity(v)) return min;
  return Math.Max(min,Math.Min(max,min+Math.Round((v-min)/step)*step));
 }
 public AppSettings Normalize() {
  var s=Copy();s.Mode=s.Mode;s.SettingsVersion=10;s.FontFamilyName=NotificationFonts.Resolve(s.FontFamilyName);s.CardColor=NotificationColors.Normalize(s.CardColor);s.TextColor=NotificationColors.Normalize(s.TextColor);s.TextOutlineColor=NotificationColors.Normalize(s.TextOutlineColor);if(!Enum.IsDefined(typeof(BackdropStrength),s.BackdropStrength))s.BackdropStrength=BackdropStrength.Standard;s.UiScalePercent=Clamp(s.UiScalePercent,100,150,10);s.ThemeMode=NormalizeChoice(s.ThemeMode,"System","System","Light","Dark");s.Language=NormalizeChoice(s.Language,"Japanese","Japanese","English");s.Width=Clamp(s.Width,280,600,10); s.Height=Clamp(s.Height,88,240,4);
  s.DisplaySeconds=double.IsNaN(s.DisplaySeconds)||double.IsInfinity(s.DisplaySeconds)?4:Clamp(s.DisplaySeconds,1,60,1);
  s.ImageSize=double.IsNaN(s.ImageSize)||double.IsInfinity(s.ImageSize)?56:Clamp(s.ImageSize,24,128,4);
  s.ImagePosition=NormalizeChoice(s.ImagePosition,"Right","Right","Left");
  s.FontSize=Clamp(s.FontSize,11,24,1); s.Margin=Clamp(s.Margin,0,120,4);
  if(!Enum.IsDefined(typeof(Corner),s.Corner)) s.Corner=Corner.TopRight;
  if(!Enum.IsDefined(typeof(SoundKind),s.SoundKind)) s.SoundKind=SoundKind.None;
  s.MonitorId=s.MonitorId??""; s.SoundPath=s.SoundPath??"";
  if(!string.IsNullOrEmpty(s.SoundPath)) {
   try {if(!Path.IsPathRooted(s.SoundPath))s.SoundPath="";else s.SoundPath=Path.GetFullPath(s.SoundPath);}
   catch(ArgumentException){s.SoundPath="";}catch(NotSupportedException){s.SoundPath="";}catch(PathTooLongException){s.SoundPath="";}
  }
  return s;
 }
 static string NormalizeChoice(string value,string fallback,params string[] choices){foreach(string choice in choices)if(string.Equals(value,choice,StringComparison.OrdinalIgnoreCase))return choice;return fallback;}
}
public static class SettingsStore {
 public static AppSettings Load(string path,out string warning) {
  warning=null; if(!File.Exists(path)) return new AppSettings();
  try {
   var serializer=new JavaScriptSerializer();
   var fields=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));
   if(fields==null) throw new FormatException();
   var defaults=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(new AppSettings()));
   foreach(var key in fields.Keys) if(defaults.ContainsKey(key)) defaults[key]=fields[key];
   if(!fields.ContainsKey("Backdrop")) defaults["Backdrop"]=fields.ContainsKey("GlassEffectEnabled")&&!Convert.ToBoolean(fields["GlassEffectEnabled"])?"Off":"Acrylic";
   BackdropMode mode;
   if(!Enum.TryParse(Convert.ToString(defaults["Backdrop"]),true,out mode)||!Enum.IsDefined(typeof(BackdropMode),mode)) {
    defaults["Backdrop"]="Off";warning="背景の設定が不明なため、OFF（不透明）で起動しました。";
   }
   defaults.Remove("GlassEffectEnabled");
   string requestedFont=Convert.ToString(defaults["FontFamilyName"]);string resolvedFont=NotificationFonts.Resolve(requestedFont);
   defaults["FontFamilyName"]=resolvedFont;
   if(!string.Equals(requestedFont,resolvedFont,StringComparison.OrdinalIgnoreCase))warning="通知のフォントが見つからないため、既定のフォントに戻しました。";
   bool rounded;
   if(!bool.TryParse(Convert.ToString(defaults["RoundedCorners"]),out rounded)){rounded=true;warning="角丸の設定が不正なため、オンで起動しました。";}
   defaults["RoundedCorners"]=rounded;
   foreach(string field in new[]{"CardColor","TextColor","TextOutlineColor"}){
    string requested=defaults[field] as string;string resolved=NotificationColors.Normalize(requested);
    if(resolved==NotificationColors.Default&&!string.Equals(requested,resolved,StringComparison.OrdinalIgnoreCase))warning="通知の色の設定が不正なため、標準の色に戻しました。";
    defaults[field]=resolved;
   }
   double duration;
   if(!double.TryParse(Convert.ToString(defaults["DisplaySeconds"],CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out duration)||double.IsNaN(duration)||double.IsInfinity(duration)){
    duration=4;warning="表示時間の設定が不正なため、4秒に戻しました。";
   }
   defaults["DisplaySeconds"]=duration;
   double imageSize;
   if(!double.TryParse(Convert.ToString(defaults["ImageSize"],CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out imageSize)||double.IsNaN(imageSize)||double.IsInfinity(imageSize)){
    imageSize=56;warning="画像サイズの設定が不正なため、56に戻しました。";
   }
   defaults["ImageSize"]=imageSize;
   double uiScale;
   if(!double.TryParse(Convert.ToString(defaults["UiScalePercent"],CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out uiScale)||double.IsNaN(uiScale)||double.IsInfinity(uiScale)){
    uiScale=100;warning="アプリの拡大率が不正なため、100%に戻しました。";
   }
   defaults["UiScalePercent"]=uiScale;
   var s=serializer.Deserialize<AppSettings>(serializer.Serialize(defaults)).Normalize();
   if(s.SoundKind==SoundKind.File&&string.IsNullOrEmpty(s.SoundPath))warning="音声ファイルの設定が不正です。WAV を選び直してください。";
   return s;
  } catch(Exception e) {
   if(!(e is ArgumentException || e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException || e is FormatException)) throw;
   warning="設定を読み込めなかったため、初期値で起動しました。"; return new AppSettings();
  }
 }
 public static bool Save(string path,AppSettings settings,out string warning) {
  warning=null; string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   File.WriteAllText(temp,new JavaScriptSerializer().Serialize(settings.Normalize()),new UTF8Encoding(false));
   for(int retry=0;;retry++){
    try {
     if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
     break;
    } catch(IOException) {
     if(retry>=10) throw;
     System.Threading.Thread.Sleep(25);
    }
   }
   return true;
  } catch(Exception e) {
   if(!(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)) throw;
   warning="設定を保存できません。この起動中は変更を使えます。 "+e.Message; return false;
  } finally { try { if(File.Exists(temp)) File.Delete(temp); } catch(IOException) {} catch(UnauthorizedAccessException) {} }
 }
}
}
