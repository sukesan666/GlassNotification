using System;
using System.IO;
using System.Collections.Generic;
namespace Glass {
public static partial class Tests {
 sealed class PreferenceMemory : INotificationPreferenceStore {
  public Dictionary<string,int> Values=new Dictionary<string,int>();public string[] Senders=new[]{"Sender.A","Sender.B"};public string Fail;public bool FailRestore;
  public string[] ListSenders(){return Senders;}
  public int? Read(string key,string name){int value;return Values.TryGetValue(key+"|"+name,out value)?(int?)value:null;}
  public void Write(string key,string name,int? value){if(Fail==key||(FailRestore&&key=="Sender.A"&&value==1))throw new IOException("injected write failure");if(value.HasValue)Values[key+"|"+name]=value.Value;else Values.Remove(key+"|"+name);}
 }
 static void OsNotificationTests(string folder){
  RegisteredAppActivationTests(folder);
  var store=new PreferenceMemory();store.Values["Sender.A|ShowBanner"]=1;store.Values["|NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"]=1;
  string path=Path.Combine(folder,"os-recovery.json");var prefs=new OsNotificationPreferences(store,path);
  prefs.Apply(true,true);Check(store.Read("Sender.A","ShowBanner")==0&&store.Read("Sender.B","ShowBanner")==0&&store.Read("","NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND")==0,"OS banner/sound suppression keeps reception untouched");
  Check(File.Exists(path)&&prefs.Recoverable,"durable recovery journal exists before OS changes");
  prefs.Apply(true,true);new OsNotificationPreferences(store,path).Restore();Check(store.Read("Sender.A","ShowBanner")==1&&store.Read("Sender.B","ShowBanner")==null&&store.Read("","NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND")==1,"restart recovery restores original values and original absence");
  prefs=new OsNotificationPreferences(store,path);prefs.Apply(true,false);store.Write("Sender.A","ShowBanner",7);prefs.Restore();Check(store.Read("Sender.A","ShowBanner")==7,"restore preserves subsequent external user changes");
  prefs=new OsNotificationPreferences(store,path);store.Fail="Sender.B";bool failed=false;try{prefs.Apply(true,true);}catch(IOException){failed=true;}store.Fail=null;prefs.Restore();Check(failed&&store.Read("Sender.A","ShowBanner")==7&&store.Read("","NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND")==1,"partial mutation failure is reversible");
  prefs=new OsNotificationPreferences(store,folder);failed=false;try{prefs.Apply(true,true);}catch(IOException){failed=true;}Check(failed&&store.Read("Sender.A","ShowBanner")==7,"journal persistence failure prevents mutations");
  File.WriteAllText(path,"{\"Version\":1,\"Changes\":[{\"Key\":\"evil\\\\nested\",\"Name\":\"Enabled\",\"Exists\":false,\"Original\":0}]}");
  failed=false;try{new OsNotificationPreferences(store,path).Restore();}catch(InvalidDataException){failed=true;}Check(failed,"recovery rejects paths and values outside notification preferences");File.Delete(path);
  store.Senders=new[]{"Microsoft.Edge!chrome-extension://example/"};prefs=new OsNotificationPreferences(store,path);prefs.Apply(true,false);Check(store.Read(store.Senders[0],"ShowBanner")==0,"browser extension notification IDs with forward slashes are valid registry sender names");prefs.Restore();Check(store.Read(store.Senders[0],"ShowBanner")==null,"browser extension sender original absence is restored");
  store.Senders=new[]{"Sender.A","Sender.B"};prefs=new OsNotificationPreferences(store,path);prefs.Apply(true,true);prefs.Apply(false,true);
  Check(store.Read("Sender.A","ShowBanner")==7&&store.Read("Sender.B","ShowBanner")==null&&store.Read("","NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND")==0&&prefs.Recoverable,"turning banner suppression off restores banners while sound suppression stays active and recoverable");
  prefs.Apply(false,false);Check(store.Read("","NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND")==1&&store.Read("Sender.A","ShowBanner")==7&&store.Read("Sender.B","ShowBanner")==null,"turning both suppressions off restores every original value");prefs.Restore();Check(!prefs.Recoverable,"restore removes the recovery journal once everything is back");
  store.Senders=new[]{"Sender.A","Sender.B"};store.Write("Sender.A","ShowBanner",1);store.Fail="Sender.B";store.FailRestore=true;prefs=new OsNotificationPreferences(store,path);string failure="";try{prefs.Apply(true,false);}catch(IOException e){failure=e.Message;}Check(failure.Contains("復元")&&prefs.Recoverable,"failed rollback is explicitly reported and keeps recovery data");store.Fail=null;store.FailRestore=false;prefs.Restore();Check(store.Read("Sender.A","ShowBanner")==1,"retry restores settings after rollback failure is resolved");
 }
 static void RegisteredAppActivationTests(string folder){
  using(var source=new SystemNotificationSource()){source.Reset();source.Reset();}
  string appId="GlassNotification.OSLink_example!App";
  string firstDirectory=Path.Combine(folder,"portable-original"),movedDirectory=Path.Combine(folder,"portable moved elsewhere");
  var registered=OsNotificationPanel.LinkedAppStartInfo(appId,firstDirectory);
  Check(registered.FileName=="explorer.exe"&&registered.Arguments=="shell:AppsFolder\\"+appId&&registered.UseShellExecute,"registered application launches through its installed AppsFolder identity");
  var moved=OsNotificationPanel.LinkedAppStartInfo(appId,movedDirectory);
  Check(moved.FileName==registered.FileName&&moved.Arguments==registered.Arguments,"moving the portable folder does not change registered application activation");
  foreach(string absent in new[]{null,"","   "}){
   var fallback=OsNotificationPanel.LinkedAppStartInfo(absent,movedDirectory);
   Check(fallback.FileName==Path.Combine(movedDirectory,"GlassNotification.Linked.exe")&&string.IsNullOrEmpty(fallback.Arguments)&&fallback.UseShellExecute,"unregistered legacy identity falls back to the linked executable: "+absent);
  }
 }
}
}
