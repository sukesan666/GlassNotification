using System;
using System.IO;
namespace Glass {
public static partial class Tests {
 sealed class FailingSenderFilterStore : INotificationSenderFilterStore {
  public bool IsAvailable {get{return true;}}public string ErrorMessage {get{return null;}}
  public bool IsAllowed(string senderId){return true;}public string[] RuleIds(){return new string[0];}
  public void SetAllowed(string senderId,bool allowed){throw new IOException("injected save failure");}
 }
 static partial void NotificationSenderFilterTests(string folder){
  string path=Path.Combine(folder,"notification-filters.json");
  var filters=new NotificationSenderFilterStore(path);
  Check(filters.IsAvailable&&filters.IsAllowed("Sender.A")&&filters.IsAllowed(null),"new senders default to allowed");
  filters.SetAllowed("Sender.A",false);filters.SetAllowed("Sender.B",true);
  var reloaded=new NotificationSenderFilterStore(path);
  Check(reloaded.IsAvailable&&!reloaded.IsAllowed("Sender.A")&&reloaded.IsAllowed("Sender.B")&&reloaded.IsAllowed("Sender.C"),"blocked and explicitly allowed sender rules persist and reload");
  Check(string.Join(",",reloaded.RuleIds())=="Sender.A,Sender.B","saved sender rules are listed so they can be changed after a restart");
  File.WriteAllText(path,"not json");
  var invalid=new NotificationSenderFilterStore(path);
  Check(!invalid.IsAvailable&&invalid.ErrorMessage!=null&&invalid.IsAllowed("Sender.A"),"invalid filter settings are reported and not silently applied");
  bool rejected=false;try{invalid.SetAllowed("Sender.B",false);}catch(InvalidOperationException){rejected=true;}
  Check(rejected&&File.ReadAllText(path)=="not json","unreadable filter settings are preserved and cannot be overwritten");
 }
}
}
