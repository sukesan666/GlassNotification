using System;
using System.Collections.Generic;
namespace Glass {
public static partial class Tests {
 sealed class ScheduleLog : IScheduler {
  public sealed class Job : IDisposable {public Action Callback;public TimeSpan Delay;public bool Cancelled;public void Dispose(){Cancelled=true;}}
  public List<Job> Jobs=new List<Job>();
  public IDisposable Schedule(TimeSpan delay,Action callback){var job=new Job {Delay=delay,Callback=callback};Jobs.Add(job);return job;}
 }
 sealed class ViewLog : INotificationView,IStaticNotificationView,INotificationContentView,IStackableNotificationView {
  public int Shows,StaticShows,Hides,Fades;public SystemNotification Notification;public double HeightDip=88;
  public double CardHeightDip {get{return HeightDip;}}
  public double StackOffsetDip {get;private set;}
  public void SetStackOffset(double offsetDip){StackOffsetDip=offsetDip;}
  public void SetNotification(SystemNotification value){Notification=value;}
  public void ShowCard(){Shows++;}public void ShowStaticCard(){StaticShows++;}public void HideCard(){Hides++;}public void FadeOut(Action completed){Fades++;completed();}
 }
 sealed class SoundLog : ISoundService {public int Plays,Stops,Disposals;public void Play(AppSettings s,Action<string> report){Plays++;}public void Stop(){Stops++;}public void Dispose(){Disposals++;}}
 static partial void ControllerTests() {
  var timer=new ScheduleLog();var view=new ViewLog();var sound=new SoundLog();int saves=0;
  var controller=new NotificationController(view,sound,timer,delegate(AppSettings s){return s;},delegate(AppSettings s){saves++;},delegate{});
  controller.ShowSample(false);Check(view.Shows==1&&sound.Plays==0,"startup silent");
  var old=timer.Jobs[0];controller.ShowSample(true);Check(sound.Plays==1&&old.Cancelled,"manual sample and cancels old deadline");
  old.Callback();Check(view.Fades==0,"stale callback ignored");
  controller.UpdateSettings(new AppSettings {Width=480});Check(saves==1&&sound.Plays==1,"settings save and remain silent");
  var latest=timer.Jobs[timer.Jobs.Count-1];Check(latest.Delay.TotalSeconds==4,"adjustment visible for four seconds");
  latest.Callback();Check(view.Fades==1,"latest deadline fades card");
  controller.UpdateSettings(new AppSettings {SoundKind=SoundKind.Standard});Check(sound.Stops==1,"sound selection stops audio");
  controller.PreviewSound();Check(sound.Plays==2,"preview selected sound");
  controller.Reset();Check(controller.Settings.Width==360&&controller.Settings.SoundKind==SoundKind.None,"reset all settings");
  controller.PreviewSound();Check(sound.Plays==2,"none cannot preview");
  var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
  controller.Initialize(serializer.Deserialize<AppSettings>("{\"DisplaySeconds\":12}"));controller.ShowSample(false);
  var longDisplay=timer.Jobs[timer.Jobs.Count-1];
  Check(Math.Abs(longDisplay.Delay.TotalSeconds-12.66)<.001,"sample uses chosen display duration plus entry animation");
  controller.UpdateSettings(serializer.Deserialize<AppSettings>("{\"DisplaySeconds\":2}"));
  var shortDisplay=timer.Jobs[timer.Jobs.Count-1];int beforeOldCallback=view.Fades;longDisplay.Callback();
  Check(longDisplay.Cancelled&&shortDisplay.Delay.TotalSeconds==2&&view.Fades==beforeOldCallback,"changing duration cancels old deadline and uses the new value");
  var combinedTimer=new ScheduleLog();var combinedView=new ViewLog();var combinedSound=new SoundLog();
  var combinedController=new NotificationController(combinedView,combinedSound,combinedTimer,delegate(AppSettings s){return s;},delegate(AppSettings s){},delegate{});
  combinedController.Initialize(new AppSettings {SoundKind=SoundKind.Standard,DisplaySeconds=8});
  var testNotification=new SystemNotification {App="テスト通知",Title="文字の確認",Body="カード本文を表示します。"};
  combinedController.ShowNotification(testNotification,true);
  Check(ReferenceEquals(combinedView.Notification,testNotification)&&combinedView.Shows==1&&combinedSound.Plays==1&&Math.Abs(combinedTimer.Jobs[0].Delay.TotalSeconds-8.66)<.001,"combined preview displays representative text, plays selected sound and uses configured duration");
  combinedController.Dispose();
  var pending=timer.Jobs[timer.Jobs.Count-1];controller.Dispose();int shown=view.Shows;pending.Callback();controller.ShowSample(true);
  Check(pending.Cancelled&&sound.Disposals==1&&view.Shows==shown&&view.Hides==1,"shutdown cancels all owned work");
  timer=new ScheduleLog();view=new ViewLog();sound=new SoundLog();
  controller=new NotificationController(view,sound,timer,delegate(AppSettings s){return s;},delegate(AppSettings s){},delegate{});
  controller.Initialize(new AppSettings {SoundKind=SoundKind.Standard});controller.ShowSample(true);
  controller.SetPaused(true);int pausedShows=view.Shows;controller.ShowSample(true);controller.ShowNotification(new SystemNotification());controller.PreviewSound();
  controller.UpdateSettings(new AppSettings {SoundKind=SoundKind.Standard});
  Check(controller.IsPaused&&view.Shows==pausedShows&&sound.Plays==1&&view.Hides==1,"pause hides cards and blocks samples, received notifications, previews and settings refreshes");
  controller.SetPaused(false);controller.ShowSample(false);
  Check(!controller.IsPaused&&view.Shows==pausedShows+1,"resume allows later cards without replaying paused cards");
  var stackTimer=new ScheduleLog();var v0=new ViewLog();var v1=new ViewLog();var v2=new ViewLog();var stackSound=new SoundLog();
  var stackController=new NotificationController(new INotificationView[]{v0,v1,v2},stackSound,stackTimer,delegate(AppSettings s){return s;},delegate(AppSettings s){},delegate{});
  var n1=new SystemNotification {Id=1,Title="1件目"};var n2=new SystemNotification {Id=2,Title="2件目"};var n3=new SystemNotification {Id=3,Title="3件目"};var n4=new SystemNotification {Id=4,Title="4件目"};
  stackController.ShowNotification(n1,false);stackController.ShowNotification(n2,false);stackController.ShowNotification(n3,false);
  Check(stackController.ActiveCardCount==3&&!stackController.HasAvailableSlot&&ReferenceEquals(v0.Notification,n1)&&ReferenceEquals(v1.Notification,n2)&&ReferenceEquals(v2.Notification,n3),"up to three overlapping notifications display simultaneously");
  Check(v0.StackOffsetDip==0&&v1.StackOffsetDip==98&&v2.StackOffsetDip==196,"stacked cards receive cumulative height plus spacing offsets");
  stackTimer.Jobs[0].Callback();
  Check(stackController.ActiveCardCount==2&&stackController.HasAvailableSlot&&v1.StackOffsetDip==0&&v2.StackOffsetDip==98,"when the first card fades out, remaining stacked cards shift toward the corner");
  stackController.ShowNotification(n4,false);
  Check(stackController.ActiveCardCount==3&&ReferenceEquals(v0.Notification,n4)&&v1.StackOffsetDip==0&&v2.StackOffsetDip==98&&v0.StackOffsetDip==196,"freed card slot is reused at the end of the stack for the next arriving notification");
  // Two-mode tests: Settings (stationary card) and Active
  int animatedShowsBefore=v0.Shows;int staticShowsBefore=v0.StaticShows;int jobsBefore=stackTimer.Jobs.Count;
  stackController.SetMode(OperationMode.Settings);
  Check(stackController.Mode==OperationMode.Settings&&stackController.ActiveCardCount==1&&v0.StaticShows==staticShowsBefore+1&&v0.Shows==animatedShowsBefore&&stackTimer.Jobs.Count==jobsBefore,"settings mode displays a stationary card without slide animation or countdown timer");
  stackController.UpdateSettings(new AppSettings {Width=400});
  Check(v0.StaticShows==staticShowsBefore+2&&v0.Shows==animatedShowsBefore&&stackTimer.Jobs.Count==jobsBefore,"changing settings in settings mode updates the stationary card in place without scheduling fade-out");
  stackController.SetMode(OperationMode.Active);
  Check(stackController.Mode==OperationMode.Active&&stackController.ActiveCardCount==0&&stackController.HasAvailableSlot,"switching from settings mode to active mode clears the stationary preview card and enables normal notifications");
  stackController.Dispose();
  controller.Dispose();
 }
}
}
