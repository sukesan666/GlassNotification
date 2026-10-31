using System;
using System.Linq;
using System.Collections.Generic;
namespace Glass {
public sealed class SystemNotification {
 public uint Id {get;set;}public string SenderId {get;set;}public string App {get;set;}public string Title {get;set;}public string Body {get;set;}public DateTimeOffset Created {get;set;}
 public System.Windows.Media.Imaging.BitmapSource AppIcon {get;set;}
 // Images carried by the notification itself: the small logo replaces the app icon, the content image (hero/inline) sits on the right.
 public System.Windows.Media.Imaging.BitmapSource LogoImage {get;set;}public bool LogoCircle {get;set;}
 public System.Windows.Media.Imaging.BitmapSource ContentImage {get;set;}
 public string LaunchArguments {get;set;}
}
public sealed class NotificationFeed {
 readonly int capacity;readonly Queue<SystemNotification> pending=new Queue<SystemNotification>();HashSet<uint> known=new HashSet<uint>();bool baselined;
 public NotificationFeed(int maximum){if(maximum<1)throw new ArgumentOutOfRangeException("maximum");capacity=maximum;}
 public void Reset(){pending.Clear();known.Clear();baselined=false;}
 public void DiscardPending(){pending.Clear();}
 public void FilterPending(Func<SystemNotification,bool> shouldKeep){
  if(shouldKeep==null)throw new ArgumentNullException("shouldKeep");
  var kept=pending.Where(shouldKeep).ToArray();pending.Clear();foreach(var notification in kept)pending.Enqueue(notification);
 }
 public void Observe(IEnumerable<SystemNotification> snapshot){Observe(snapshot,delegate{return true;});}
 public void Observe(IEnumerable<SystemNotification> snapshot,Func<SystemNotification,bool> shouldForward){Observe(snapshot,shouldForward,null);}
 internal IEnumerable<SystemNotification> Pending {get{return pending;}}
 internal void Observe(IEnumerable<SystemNotification> snapshot,Func<SystemNotification,bool> shouldForward,Action<SystemNotification> arrived){
  if(snapshot==null)throw new ArgumentNullException("snapshot");if(shouldForward==null)throw new ArgumentNullException("shouldForward");
  var items=snapshot.GroupBy(n=>n.Id).Select(g=>g.First()).ToArray();
  var byId=items.ToDictionary(n=>n.Id);
  var current=new HashSet<uint>(byId.Keys);
  var waiting=pending.Where(n=>current.Contains(n.Id)&&shouldForward(n)).ToArray();
  pending.Clear();
  foreach(var n in waiting){
   SystemNotification latest;
   if(byId.TryGetValue(n.Id,out latest)&&n.AppIcon==null&&latest.AppIcon!=null)n.AppIcon=latest.AppIcon;
   pending.Enqueue(n);
  }
  if(baselined)foreach(var n in items.Where(n=>!known.Contains(n.Id)&&shouldForward(n)).OrderBy(n=>n.Created).ThenBy(n=>n.Id)){if(pending.Count>=capacity)pending.Dequeue();pending.Enqueue(n);}
  if(baselined&&arrived!=null)foreach(var n in pending)if(!known.Contains(n.Id))arrived(n);
  known=current;baselined=true;
 }
 public SystemNotification Take(){return pending.Count==0?null:pending.Dequeue();}
}
}
