using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace Glass {
public static partial class Tests {
 static void BackdropSettingsTests(string path) {
  // Catches loss of mode on save, unsafe unknown modes, and migration overwrites.
  string warning;var serializer=new JavaScriptSerializer();
  string json="{\"Width\":480,\"Margin\":56,\"GlassEffectEnabled\":false,\"Backdrop\":\"Mica\",\"SoundKind\":1}";
  File.WriteAllText(path,json);var settings=SettingsStore.Load(path,out warning);
  Check(File.ReadAllText(path)==json,"loading settings never migrates the file on disk");
  SettingsStore.Save(path,settings,out warning);
  var saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(saved.ContainsKey("Backdrop")&&Convert.ToString(saved["Backdrop"])=="Mica","explicit mode wins over legacy bool and survives save");
  Check(settings.Width==480&&settings.Margin==56&&settings.SoundKind==SoundKind.Standard,"mode migration preserves unrelated preferences");
  File.WriteAllText(path,"{\"GlassEffectEnabled\":false}");settings=SettingsStore.Load(path,out warning);SettingsStore.Save(path,settings,out warning);
  saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));Check(Convert.ToString(saved["Backdrop"])=="Off","legacy false migrates to Off");
  File.WriteAllText(path,"{\"GlassEffectEnabled\":true}");settings=SettingsStore.Load(path,out warning);SettingsStore.Save(path,settings,out warning);
  saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));Check(Convert.ToString(saved["Backdrop"])=="Acrylic","legacy true migrates to Acrylic");
  File.WriteAllText(path,"{\"Backdrop\":\"future-material\",\"Width\":500}");settings=SettingsStore.Load(path,out warning);
  Check(!settings.GlassEffectEnabled&&settings.Width==500&&warning!=null,"unknown mode falls back to Off without losing settings");
  json="{\"FontFamilyName\":\"Consolas\",\"RoundedCorners\":false,\"Width\":480,\"FontSize\":18,\"Backdrop\":\"Off\"}";
  File.WriteAllText(path,json);settings=SettingsStore.Load(path,out warning);Check(File.ReadAllText(path)==json,"appearance load preserves user file bytes");
  SettingsStore.Save(path,settings,out warning);saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(saved.ContainsKey("FontFamilyName")&&Convert.ToString(saved["FontFamilyName"])=="Consolas","installed notification font persists independently of size");
  Check(saved.ContainsKey("RoundedCorners")&&!Convert.ToBoolean(saved["RoundedCorners"])&&settings.FontSize==18,"square corners persist with font size");
  File.WriteAllText(path,"{\"FontFamilyName\":\"file:///missing.ttf#Injected\",\"RoundedCorners\":\"bad\",\"Width\":480}");
  settings=SettingsStore.Load(path,out warning);bool recoveredWarning=warning!=null;SettingsStore.Save(path,settings,out warning);saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(settings.Width==480&&Convert.ToString(saved["FontFamilyName"])!="file:///missing.ttf#Injected"&&recoveredWarning,"invalid font/corners recover without losing existing preferences");
  Check(Convert.ToBoolean(saved["RoundedCorners"]),"invalid corners use rounded default");
  json="{\"CardColor\":\"#8ecaff\",\"TextColor\":\"#112233\",\"Width\":480}";
  File.WriteAllText(path,json);settings=SettingsStore.Load(path,out warning);
  Check(File.ReadAllText(path)==json,"loading notification colors does not rewrite settings");
  SettingsStore.Save(path,settings,out warning);saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(saved.ContainsKey("CardColor")&&Convert.ToString(saved["CardColor"])=="#8ECAFF","card color is normalized and persisted");
  Check(saved.ContainsKey("TextColor")&&Convert.ToString(saved["TextColor"])=="#112233","notification text color persists independently");
  File.WriteAllText(path,"{\"CardColor\":\"bad\",\"TextColor\":{},\"Width\":500}");settings=SettingsStore.Load(path,out warning);
  SettingsStore.Save(path,settings,out warning);saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(settings.Width==500&&Convert.ToString(saved["CardColor"])=="Default"&&Convert.ToString(saved["TextColor"])=="Default","invalid colors safely restore defaults without losing other settings");
  File.WriteAllText(path,"{\"DisplaySeconds\":12,\"Width\":480}");settings=SettingsStore.Load(path,out warning);SettingsStore.Save(path,settings,out warning);
  saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(saved.ContainsKey("DisplaySeconds")&&Convert.ToDouble(saved["DisplaySeconds"])==12,"display duration survives save and reload");
  File.WriteAllText(path,"{\"Backdrop\":\"Acrylic\",\"BackdropStrength\":2,\"Width\":480}");settings=SettingsStore.Load(path,out warning);
  Check(settings.BackdropStrength==BackdropStrength.Strong&&settings.Width==480,"strong acrylic strength loads independently of other card settings");
  SettingsStore.Save(path,settings,out warning);settings=SettingsStore.Load(path,out warning);
  Check(settings.BackdropStrength==BackdropStrength.Strong,"acrylic strength survives save and reload");
  File.WriteAllText(path,"{\"BackdropStrength\":99,\"Width\":500}");settings=SettingsStore.Load(path,out warning);
  Check(settings.BackdropStrength==BackdropStrength.Standard&&settings.Width==500,"invalid acrylic strength defaults to standard without losing other settings");
  File.WriteAllText(path,"{\"UiScalePercent\":\"bad\",\"Width\":500}");settings=SettingsStore.Load(path,out warning);
  Check(settings.UiScalePercent==100&&settings.Width==500&&warning!=null,"invalid app scale defaults safely without losing other settings");
  File.WriteAllText(path,"{\"DisplaySeconds\":\"bad\",\"Width\":500}");settings=SettingsStore.Load(path,out warning);SettingsStore.Save(path,settings,out warning);
  saved=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
  Check(settings.Width==500&&Convert.ToDouble(saved["DisplaySeconds"])==4,"invalid duration restores four seconds without losing other preferences");
 }
}
}
