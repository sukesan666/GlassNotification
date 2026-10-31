using System;
using System.Collections.Generic;
namespace Glass {
public static partial class Tests {
 static void NotificationActivationRouteTests(){
  Guid first=Guid.NewGuid(),second=Guid.NewGuid(),extra=Guid.NewGuid();
  int reads=0,scans=0;DateTime time=new DateTime(2026,10,30,0,0,0,DateTimeKind.Utc);
  var routes=new ChromeActivationRoutes(
   delegate(string sender){reads++;return new[]{sender=="A"?first:second};},
   delegate{scans++;return new[]{first,extra};},delegate{return time;});
  using(var candidates=routes.Candidates("A").GetEnumerator()){
   Check(candidates.MoveNext()&&candidates.Current==first&&scans==0,"registered notification route is tried before any full registry scan");
  }
  using(var candidates=routes.Candidates("A").GetEnumerator()){
   Check(candidates.MoveNext()&&candidates.Current==first&&reads==1&&scans==0,"successful preferred route reuses its cache without scanning fallback registrations");
  }
  var all=new List<Guid>(routes.Candidates("A"));
  Check(all.Count==2&&all[0]==first&&all[1]==extra&&scans==1,"failed preferred route lazily discovers distinct fallback activators");
  all=new List<Guid>(routes.Candidates("A"));
  Check(reads==1&&scans==1,"fallback discovery is cached across clicks");
  routes.Remember("A",extra);
  using(var candidates=routes.Candidates("A").GetEnumerator()){
   Check(candidates.MoveNext()&&candidates.Current==extra&&reads==1&&scans==1,"a successful fallback is tried first on the next click without repeating discovery");
  }
  using(var candidates=routes.Candidates("B").GetEnumerator()){
   Check(candidates.MoveNext()&&candidates.Current==second&&reads==2,"route cache keeps different notification senders separate");
  }
  time=time.AddMinutes(5);
  all=new List<Guid>(routes.Candidates("A"));
  Check(reads==3&&scans==2,"route caches expire so updated Chrome registrations can be discovered");
 }
}
}
