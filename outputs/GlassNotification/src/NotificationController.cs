using System;
using System.Collections.Generic;
using System.Windows.Threading;
namespace Glass {
public interface IScheduler {IDisposable Schedule(TimeSpan delay,Action callback);}
public sealed class DispatcherScheduler : IScheduler {
 sealed class Ticket : IDisposable {public DispatcherTimer Timer;public void Dispose(){Timer.Stop();}}
 public IDisposable Schedule(TimeSpan delay,Action callback) {
  var timer=new DispatcherTimer {Interval=delay};timer.Tick+=delegate {timer.Stop();callback();};timer.Start();return new Ticket {Timer=timer};
 }
}
public sealed class NotificationController : IDisposable {
 public const int MaximumVisibleCards=3;
 sealed class CardSlot {
  public INotificationView View;
  public SystemNotification Notification;
  public bool IsSample;
  public bool IsPreview;
  public IDisposable Deadline;
  public long Generation;
  public void CancelDeadline(){
   Generation++;
   if(Deadline!=null){Deadline.Dispose();Deadline=null;}
  }
 }
 readonly CardSlot[] slots;
 readonly List<CardSlot> activeSlots=new List<CardSlot>();
 readonly ISoundService sound;readonly IScheduler scheduler;
 readonly Func<AppSettings,AppSettings> apply;readonly Action<AppSettings> save;readonly Action<string> report;
 bool disposed;
 public bool IsPaused {get;private set;}
 public AppSettings Settings {get;private set;}
 public int ActiveCardCount {get{return activeSlots.Count;}}
 public bool HasAvailableSlot {
  get{
   if(disposed||IsPaused)return false;
   if(activeSlots.Count<slots.Length)return true;
   return HasOnlyPreviewCards();
  }
 }
 public NotificationController(INotificationView card,ISoundService audio,IScheduler timer,Func<AppSettings,AppSettings> normalize,Action<AppSettings> persist,Action<string> reportError)
  :this(new[]{card},audio,timer,normalize,persist,reportError){}
 public NotificationController(IList<INotificationView> cards,ISoundService audio,IScheduler timer,Func<AppSettings,AppSettings> normalize,Action<AppSettings> persist,Action<string> reportError) {
  if(cards==null||cards.Count==0)throw new ArgumentNullException("cards");
  int count=Math.Min(MaximumVisibleCards,cards.Count);
  slots=new CardSlot[count];
  for(int i=0;i<count;i++)slots[i]=new CardSlot {View=cards[i]};
  sound=audio;scheduler=timer;apply=normalize;save=persist;report=reportError;Settings=new AppSettings();
 }
 public void Initialize(AppSettings value) {if(!disposed)Settings=apply(value.Copy());}
 bool HasOnlyPreviewCards(){
  if(activeSlots.Count==0)return false;
  for(int i=0;i<activeSlots.Count;i++)if(!activeSlots[i].IsSample&&!activeSlots[i].IsPreview)return false;
  return true;
 }
 void ClearSlotsExcept(CardSlot keep){
  for(int i=0;i<slots.Length;i++){
   var slot=slots[i];
   if(ReferenceEquals(slot,keep))continue;
   slot.CancelDeadline();
   bool wasActive=activeSlots.Remove(slot);
   slot.Notification=null;slot.IsSample=false;slot.IsPreview=false;
   var stackable=slot.View as IStackableNotificationView;
   if(stackable!=null)stackable.SetStackOffset(0);
   if(wasActive)slot.View.HideCard();
  }
 }
 CardSlot AcquireSlot(bool isPreview){
  if(!isPreview&&HasOnlyPreviewCards()){
   ClearSlotsExcept(slots[0]);
   activeSlots.Clear();
   return slots[0];
  }
  if(activeSlots.Count==1&&activeSlots[0].IsSample){
   activeSlots.Clear();
   return slots[0];
  }
  for(int i=0;i<slots.Length;i++)if(!activeSlots.Contains(slots[i]))return slots[i];
  var oldest=activeSlots[0];
  oldest.CancelDeadline();
  activeSlots.RemoveAt(0);
  return oldest;
 }
 void UpdateStackLayout(){
  double offset=0;
  for(int i=0;i<activeSlots.Count;i++){
   var slot=activeSlots[i];
   var stackable=slot.View as IStackableNotificationView;
   if(stackable!=null){
    stackable.SetStackOffset(offset);
    double height=stackable.CardHeightDip>0?stackable.CardHeightDip:(Settings==null?88:Settings.Height);
    offset+=height+Layout.StackSpacing;
   }
  }
  for(int i=0;i<slots.Length;i++){
   if(!activeSlots.Contains(slots[i])){
    var stackable=slots[i].View as IStackableNotificationView;
    if(stackable!=null)stackable.SetStackOffset(0);
   }
  }
 }
 public void ShowSample(bool playSound) {
  if(disposed||IsPaused)return;
  ClearSlotsExcept(slots[0]);
  activeSlots.Clear();
  ShowOnSlot(slots[0],null,true,true,playSound);
 }
 public void ShowNotification(SystemNotification value){ShowNotification(value,true);}
 public void ShowNotification(SystemNotification value,bool playSound){
  if(disposed||IsPaused||value==null)return;
  var slot=AcquireSlot(false);
  ShowOnSlot(slot,value,false,false,playSound);
 }
 internal void ShowPreviewNotification(SystemNotification value,bool playSound){
  if(disposed||IsPaused||value==null)return;
  var slot=AcquireSlot(true);
  ShowOnSlot(slot,value,false,true,playSound);
 }
 void ShowOnSlot(CardSlot slot,SystemNotification value,bool isSample,bool isPreview,bool playSound) {
  if(disposed||IsPaused)return;
  try {
   slot.CancelDeadline();
   slot.Notification=value;
   slot.IsSample=isSample;
   slot.IsPreview=isPreview;
   var content=slot.View as INotificationContentView;
   if(content!=null)content.SetNotification(value);
   if(!activeSlots.Contains(slot))activeSlots.Add(slot);
   Settings=apply(Settings.Copy());
   UpdateStackLayout();
   slot.View.ShowCard();
   if(playSound)sound.Play(Settings,report);
   StartSlotDeadline(slot,Settings.DisplaySeconds+NotificationWindow.EntryDurationSeconds);
  }catch(InvalidOperationException e){
   activeSlots.Remove(slot);
   UpdateStackLayout();
   report(e.Message);
  }
 }
 public void UpdateSettings(AppSettings value) {
  if(disposed)return;
  try {
   bool changedSound=Settings.SoundKind!=value.SoundKind||Settings.SoundPath!=value.SoundPath;
   var resolved=apply(value.Copy());if(changedSound)sound.Stop();Settings=resolved;save(Settings.Copy());
   if(!IsPaused){
    if(activeSlots.Count==0){
     var slot=slots[0];
     slot.IsSample=true;slot.IsPreview=true;
     activeSlots.Add(slot);
    }
    UpdateStackLayout();
    for(int i=0;i<activeSlots.Count;i++){
     activeSlots[i].View.ShowCard();
     StartSlotDeadline(activeSlots[i],Settings.DisplaySeconds);
    }
   }
  }catch(InvalidOperationException e){report(e.Message);}
 }
 void StartSlotDeadline(CardSlot slot,double seconds) {
  slot.CancelDeadline();
  long current=slot.Generation;
  slot.Deadline=scheduler.Schedule(TimeSpan.FromSeconds(seconds),delegate {
   if(!disposed&&current==slot.Generation) {
    slot.CancelDeadline();
    long fadeGen=slot.Generation;
    slot.View.FadeOut(delegate {
     if(!disposed&&fadeGen==slot.Generation){
      activeSlots.Remove(slot);
      slot.Notification=null;slot.IsSample=false;slot.IsPreview=false;
      UpdateStackLayout();
     }
    });
   }
  });
 }
 public void PreviewSound(){if(!disposed&&!IsPaused&&Settings.SoundKind!=SoundKind.None)sound.Play(Settings,report);}
 public void DismissCard(INotificationView target){
  if(disposed||target==null)return;
  for(int i=0;i<slots.Length;i++){
   var slot=slots[i];
   if(ReferenceEquals(slot.View,target)){
    slot.CancelDeadline();
    sound.Stop();
    long fadeGen=slot.Generation;
    slot.View.FadeOut(delegate {
     if(!disposed&&fadeGen==slot.Generation){
      activeSlots.Remove(slot);
      slot.Notification=null;slot.IsSample=false;slot.IsPreview=false;
      UpdateStackLayout();
     }
    });
    return;
   }
  }
 }
 public void DismissCurrentCard(){
  if(disposed)return;
  if(activeSlots.Count>0){
   DismissCard(activeSlots[0].View);
   return;
  }
  DismissCard(slots[0].View);
 }
 public void SetPaused(bool value) {
  if(disposed||IsPaused==value)return;
  IsPaused=value;
  if(value) {
   for(int i=0;i<slots.Length;i++){
    slots[i].CancelDeadline();
    slots[i].Notification=null;slots[i].IsSample=false;slots[i].IsPreview=false;
    var stackable=slots[i].View as IStackableNotificationView;
    if(stackable!=null)stackable.SetStackOffset(0);
    slots[i].View.HideCard();
   }
   activeSlots.Clear();
   sound.Stop();
  }
 }
 public void Reset(){if(disposed)return;sound.Stop();UpdateSettings(new AppSettings());}
 public void Dispose(){
  if(disposed)return;disposed=true;
  for(int i=0;i<slots.Length;i++){
   slots[i].CancelDeadline();
   slots[i].View.HideCard();
  }
  activeSlots.Clear();
  sound.Dispose();
 }
}
}
