using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace Glass {
internal sealed class AppRuntimeState {
 public bool DoNotDisturb {get;set;}
 public bool MinimizedToTray {get;set;}
 public AppRuntimeState Copy(){return new AppRuntimeState {DoNotDisturb=DoNotDisturb,MinimizedToTray=MinimizedToTray};}
}
internal interface IAppRuntimeStateStore {
 bool IsAvailable {get;}
 string ErrorMessage {get;}
 AppRuntimeState Load();
 void Save(AppRuntimeState state);
}
internal sealed class AppRuntimeStateStore : IAppRuntimeStateStore {
 readonly string path;AppRuntimeState state=new AppRuntimeState();
 public bool IsAvailable {get;private set;}
 public string ErrorMessage {get;private set;}
 public AppRuntimeStateStore(string filePath){
  if(string.IsNullOrWhiteSpace(filePath))throw new ArgumentException("起動状態の保存先が必要です。","filePath");
  path=Path.GetFullPath(filePath);LoadFile();
 }
 void LoadFile(){
  if(!File.Exists(path)){IsAvailable=true;return;}
  try{
   var loaded=new JavaScriptSerializer {MaxJsonLength=4096}.Deserialize<AppRuntimeState>(File.ReadAllText(path,Encoding.UTF8));
   if(loaded==null)throw new InvalidDataException("起動状態の形式が不正です。");
   state=loaded;IsAvailable=true;
  }catch(Exception e){
   if(!(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is InvalidOperationException||e is InvalidDataException))throw;
   ErrorMessage="前回の起動状態を読み込めません。既存データを保持しました。"+e.Message;
  }
 }
 public AppRuntimeState Load(){return state.Copy();}
 public void Save(AppRuntimeState value){
  if(!IsAvailable)throw new InvalidOperationException(ErrorMessage??"起動状態を保存できません。");
  if(value==null)throw new ArgumentNullException("value");
  var next=value.Copy();string directory=Path.GetDirectoryName(path);Directory.CreateDirectory(directory);
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   byte[] bytes=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(next));
   using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
   if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
   state=next;
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
}
