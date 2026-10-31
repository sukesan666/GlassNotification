using System;
using System.IO;
namespace Glass {
public static partial class Tests {
 sealed class MemoryRuntimeStateStore : IAppRuntimeStateStore {
  AppRuntimeState state=new AppRuntimeState();
  public bool IsAvailable {get{return true;}}
  public string ErrorMessage {get{return null;}}
  public AppRuntimeState Load(){return state.Copy();}
  public void Save(AppRuntimeState value){state=value.Copy();}
 }
 static partial void AppRuntimeStateTests(string folder){
  string path=Path.Combine(folder,"runtime-state.json");
  var store=new AppRuntimeStateStore(path);Check(store.IsAvailable&&!store.Load().DoNotDisturb&&!store.Load().MinimizedToTray,"missing previous state starts normally");
  store.Save(new AppRuntimeState {DoNotDisturb=true,MinimizedToTray=true});
  var restored=new AppRuntimeStateStore(path).Load();Check(restored.DoNotDisturb&&restored.MinimizedToTray,"DND and tray state persist across store reload");
  File.WriteAllText(path,"not json");var invalid=new AppRuntimeStateStore(path);
  Check(!invalid.IsAvailable&&invalid.ErrorMessage!=null&&!invalid.Load().DoNotDisturb,"invalid state is reported and safe defaults are used");
  bool rejected=false;try{invalid.Save(new AppRuntimeState());}catch(InvalidOperationException){rejected=true;}
  Check(rejected&&File.ReadAllText(path)=="not json","invalid state file is not overwritten");
 }
}
}
