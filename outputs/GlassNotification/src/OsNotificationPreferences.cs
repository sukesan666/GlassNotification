using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace Glass {
public interface INotificationPreferenceStore {
 string[] ListSenders();int? Read(string key,string name);void Write(string key,string name,int? value);
}
public sealed class WindowsNotificationPreferenceStore : INotificationPreferenceStore {
 const string Root="Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings";
 public string[] ListSenders(){using(var root=Registry.CurrentUser.OpenSubKey(Root)){return root==null?new string[0]:root.GetSubKeyNames();}}
 public int? Read(string key,string name){using(var entry=Registry.CurrentUser.OpenSubKey(Root+(key.Length==0?"":"\\"+key))){
  if(entry==null||!entry.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase))return null;
  if(entry.GetValueKind(name)!=RegistryValueKind.DWord)throw new InvalidDataException("通知設定の値形式が対応していません："+key+" / "+name+" / "+entry.GetValueKind(name));
  return (int)entry.GetValue(name);
 }}
 public void Write(string key,string name,int? value){
  OsNotificationPreferences.Validate(key,name);
  if(!value.HasValue){using(var entry=Registry.CurrentUser.OpenSubKey(Root+(key.Length==0?"":"\\"+key),true)){if(entry!=null)entry.DeleteValue(name,false);}return;}
  using(var entry=Registry.CurrentUser.CreateSubKey(Root+(key.Length==0?"":"\\"+key))){entry.SetValue(name,value.Value,RegistryValueKind.DWord);entry.Flush();}
 }
}
public sealed class OsPreferenceChange {public string Key {get;set;}public string Name {get;set;}public bool Exists {get;set;}public int Original {get;set;}}
public sealed class OsPreferenceJournal {public int Version {get;set;}public List<OsPreferenceChange> Changes {get;set;}public OsPreferenceJournal(){Version=1;Changes=new List<OsPreferenceChange>();}}
public sealed class OsNotificationPreferences {
 readonly INotificationPreferenceStore store;readonly string path;OsPreferenceJournal journal;
 public bool Recoverable {get{return File.Exists(path);}}
 public static void Validate(string key,string name){if(key==null||name==null||key.Length>1024||key.IndexOfAny(new[]{'\\','\0'})>=0||!(key.Length==0?name=="NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND":name=="ShowBanner"))throw new InvalidDataException("通知設定の復元データに対応外の項目があります："+key+" / "+name);}
 public OsNotificationPreferences(INotificationPreferenceStore backend,string journalPath){store=backend;path=journalPath;}
 void Load(){if(journal!=null)return;if(!File.Exists(path)){journal=new OsPreferenceJournal();return;}
  try{journal=new JavaScriptSerializer().Deserialize<OsPreferenceJournal>(File.ReadAllText(path));if(journal==null||journal.Version!=1||journal.Changes==null||journal.Changes.Count>20000)throw new InvalidDataException();foreach(var c in journal.Changes){if(c==null)throw new InvalidDataException();Validate(c.Key,c.Name);}if(journal.Changes.Select(c=>c.Key+"|"+c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=journal.Changes.Count)throw new InvalidDataException();}
  catch(Exception e){journal=null;if(e is IOException||e is UnauthorizedAccessException)throw;throw new InvalidDataException("通知設定の復元データを読み込めません。上書きせず停止しました。",e);}
 }
 void Save(){string directory=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(directory);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){var bytes=System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(journal));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
  finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 void Suppress(string key,string name){Validate(key,name);var existing=journal.Changes.FirstOrDefault(c=>string.Equals(c.Key,key,StringComparison.OrdinalIgnoreCase)&&c.Name==name);if(existing!=null)return;int? original=store.Read(key,name);var change=new OsPreferenceChange {Key=key,Name=name,Exists=original.HasValue,Original=original.GetValueOrDefault()};journal.Changes.Add(change);try{Save();}catch{journal.Changes.Remove(change);throw;}store.Write(key,name,0);}
 public void Apply(bool banners,bool sounds){Load();try{
  foreach(var c in journal.Changes.ToArray())if(c.Key.Length==0?!sounds:!banners)RestoreOne(c);
  if(banners)foreach(string sender in store.ListSenders())Suppress(sender,"ShowBanner");
  if(sounds)Suppress("","NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND");
 }catch(Exception applyError){try{Restore();}catch(Exception restoreError){throw new IOException("OS設定の変更を中止しましたが、復元が一部未完了です。「OSの通知設定を元に戻す」で再試行してください。",new AggregateException(applyError,restoreError));}throw;}}
 void RestoreOne(OsPreferenceChange change){if(store.Read(change.Key,change.Name)==0)store.Write(change.Key,change.Name,change.Exists?(int?)change.Original:null);journal.Changes.Remove(change);try{Save();}catch{journal.Changes.Add(change);throw;}}
 public void Restore(){Load();Exception error=null;foreach(var c in journal.Changes.ToArray())try{RestoreOne(c);}catch(Exception e){error=e;}if(error!=null)throw new IOException("一部のOS通知設定を戻せませんでした。復元データを保持しています。",error);if(File.Exists(path))File.Delete(path);}
}
}
