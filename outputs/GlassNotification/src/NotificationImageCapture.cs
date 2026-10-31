using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Imaging;
namespace Glass {
// Capture temporary Chrome files when a new notification arrives, before it waits for a card.
// Only the worker touches the resolver; the UI receives frozen bitmaps under the same lock.
internal sealed class NotificationImageCapture : IDisposable {
 const int MaximumPending=32;
 sealed class Capture {
  public uint Id;public long Sequence;public DateTime Deadline,NextAttempt;public bool Done;
  public BitmapSource Logo,Content;public bool Circle;public string LaunchArguments;
 }
 readonly object gate=new object();readonly NotificationImageResolver resolver;
 readonly Dictionary<uint,Capture> captures=new Dictionary<uint,Capture>();readonly Queue<Capture> work=new Queue<Capture>();
 bool running,disposed;long sequence;
 public NotificationImageCapture(NotificationImageResolver imageResolver){if(imageResolver==null)throw new ArgumentNullException("imageResolver");resolver=imageResolver;}
 public void Begin(IEnumerable<SystemNotification> arriving,IEnumerable<SystemNotification> pending,SystemNotification waiting){
  // The feed may have dropped old queued cards to make room. Release those before enforcing the capture bound.
  Retain(pending,waiting);foreach(var notification in arriving)Begin(notification);
 }
 public void Begin(SystemNotification notification){
  if(notification==null)return;
  lock(gate){
   if(disposed||captures.ContainsKey(notification.Id))return;
   if(captures.Count>=MaximumPending){Capture oldest=null;foreach(var entry in captures.Values)if(oldest==null||entry.Sequence<oldest.Sequence)oldest=entry;captures.Remove(oldest.Id);Compact();}
   var capture=new Capture {Id=notification.Id,Sequence=++sequence,Deadline=DateTime.UtcNow.AddSeconds(2.5)};
   captures.Add(capture.Id,capture);work.Enqueue(capture);
   if(!running){running=true;ThreadPool.QueueUserWorkItem(delegate {Run();});}
  }
 }
 public void Retain(IEnumerable<SystemNotification> pending,SystemNotification waiting){
  var keep=new HashSet<uint>();foreach(var notification in pending)keep.Add(notification.Id);if(waiting!=null)keep.Add(waiting.Id);
  lock(gate){var remove=new List<uint>();foreach(uint id in captures.Keys)if(!keep.Contains(id))remove.Add(id);foreach(uint id in remove)captures.Remove(id);Compact();}
 }
 public bool TryApply(SystemNotification notification){
  if(notification==null)return true;
  lock(gate){
   Capture capture;if(!captures.TryGetValue(notification.Id,out capture))return true;
   if(capture.Logo!=null){notification.LogoImage=capture.Logo;notification.LogoCircle=capture.Circle;}
   if(capture.Content!=null)notification.ContentImage=capture.Content;
   if(!string.IsNullOrEmpty(capture.LaunchArguments))notification.LaunchArguments=capture.LaunchArguments;
   return capture.Done;
  }
 }
 bool IsCurrent(Capture capture){Capture current;return captures.TryGetValue(capture.Id,out current)&&object.ReferenceEquals(current,capture);}
 void Compact(){int count=work.Count;for(int i=0;i<count;i++){var capture=work.Dequeue();if(IsCurrent(capture))work.Enqueue(capture);}}
 void Run(){
  while(true){
   Capture capture=null;
   lock(gate){
    if(disposed||work.Count==0){running=false;return;}
    int count=work.Count;for(int i=0;i<count;i++){var candidate=work.Dequeue();if(!IsCurrent(candidate))continue;if(capture==null&&candidate.NextAttempt<=DateTime.UtcNow)capture=candidate;else work.Enqueue(candidate);}
   }
   if(capture==null){Thread.Sleep(35);continue;}
   var result=new SystemNotification {Id=capture.Id};bool done;
   try{done=resolver.TryAttach(result);}catch(Exception){done=true;}
   lock(gate){
    if(!IsCurrent(capture))continue;
    if(result.LogoImage!=null){capture.Logo=result.LogoImage;capture.Circle=result.LogoCircle;}
    if(result.ContentImage!=null)capture.Content=result.ContentImage;
    if(!string.IsNullOrEmpty(result.LaunchArguments))capture.LaunchArguments=result.LaunchArguments;
    capture.Done=done||DateTime.UtcNow>=capture.Deadline;
    if(!capture.Done){capture.NextAttempt=DateTime.UtcNow.AddMilliseconds(60);work.Enqueue(capture);}
   }
  }
 }
 public void Dispose(){lock(gate){disposed=true;captures.Clear();work.Clear();}}
}
}
