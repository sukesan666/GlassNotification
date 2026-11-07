using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace Glass {
internal interface INotificationSenderFilterStore {
 bool IsAvailable {get;}
 string ErrorMessage {get;}
 bool IsAllowed(string senderId);
 string[] RuleIds();
 void SetAllowed(string senderId,bool allowed);
}
internal sealed class NotificationSenderFilterStore : INotificationSenderFilterStore {
 const int MaximumSenders=4096;
 readonly string path;
 readonly Dictionary<string,bool> rules=new Dictionary<string,bool>(StringComparer.Ordinal);
 public bool IsAvailable {get;private set;}
 public string ErrorMessage {get;private set;}
 public NotificationSenderFilterStore(string filePath) {
  if(string.IsNullOrWhiteSpace(filePath))throw new ArgumentException("通知元フィルターの保存先が必要です。","filePath");
  path=Path.GetFullPath(filePath);Load();
 }
 void Load() {
  if(!File.Exists(path)){IsAvailable=true;return;}
  try {
   var loaded=new JavaScriptSerializer {MaxJsonLength=1024*1024}.Deserialize<Dictionary<string,bool>>(File.ReadAllText(path,Encoding.UTF8));
   if(loaded==null||loaded.Count>MaximumSenders)throw new InvalidDataException("通知元フィルターの形式または件数が不正です。");
   foreach(var rule in loaded) {
    if(string.IsNullOrWhiteSpace(rule.Key)||rule.Key.Length>1024)throw new InvalidDataException("通知元IDが不正です。");
    rules.Add(rule.Key,rule.Value);
   }
   IsAvailable=true;
  }catch(Exception e) {
   if(!(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException||e is InvalidDataException))throw;
   ErrorMessage="通知元フィルターを読み込めません。既存の設定を保持し、フィルター適用を停止しました。"+e.Message;
  }
 }
 public bool IsAllowed(string senderId) {
  if(string.IsNullOrEmpty(senderId)||!IsAvailable)return true;
  bool allowed;return !rules.TryGetValue(senderId,out allowed)||allowed;
 }
 public string[] RuleIds(){return rules.Keys.OrderBy(id=>id,StringComparer.Ordinal).ToArray();}
 public void SetAllowed(string senderId,bool allowed) {
  if(!IsAvailable)throw new InvalidOperationException(ErrorMessage??"通知元フィルターを利用できません。");
  if(string.IsNullOrWhiteSpace(senderId)||senderId.Length>1024)throw new ArgumentException("通知元IDが不正です。","senderId");
  bool hadRule;bool previousAllowed=rules.TryGetValue(senderId,out hadRule);
  rules[senderId]=allowed;
  try{Save();}
  catch{if(hadRule)rules[senderId]=previousAllowed;else rules.Remove(senderId);throw;}
 }
 void Save() {
  if(rules.Count>MaximumSenders)throw new InvalidDataException("通知元フィルターが上限件数を超えました。");
  string directory=Path.GetDirectoryName(path);Directory.CreateDirectory(directory);
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   byte[] bytes=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(rules));
   using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
   for(int retry=0;;retry++){
    try{if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);break;}
    catch(IOException){if(retry>=10)throw;System.Threading.Thread.Sleep(25);}
   }
  }finally{if(File.Exists(temporary))try{File.Delete(temporary);}catch{}}
 }
}
}
