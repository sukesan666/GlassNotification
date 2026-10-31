using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace Glass {
public static partial class Tests {
 sealed class CompositionSlideFixture : ICompositionSlide {
  public CompositionSlideRequest Request;public bool Disposed;public int Updates;
  public readonly List<string> Events=new List<string>();
  public bool Visible=true;public bool IsVisible {get{return Visible&&!Disposed;}}
  public Rect CurrentBounds {get{return Request.To;}}
  public void Update(FrameworkElement content){Updates++;}
  public void RevealOwner(){Events.Add("reveal");}
  public void SynchronizeOwner(){Events.Add("presented");}
  public void Dispose(){Events.Add("dispose");Disposed=true;}
 }
 static void CompositionSlideTests(){
  var card=new NotificationWindow();var sessions=new List<CompositionSlideFixture>();
  card.CompositionSlideFactory=delegate(CompositionSlideRequest request){var session=new CompositionSlideFixture {Request=request};sessions.Add(session);return session;};
  try{
   card.Apply(new AppSettings {Mode=BackdropMode.Off,CardColor="#000000",TextColor="#FFFFFF"},NativeWindow.GetMonitors()[0]);card.ShowCard();
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   var timer=(DispatcherTimer)typeof(NotificationWindow).GetField("animation",flags).GetValue(card);
   Check(sessions.Count==1&&sessions[0].Request.Entering&&sessions[0].Request.Seconds==.66,"entry submits one timed animation to the compositor");
   Check(sessions[0].Request.CardColor=="#000000","GPU slide receives selected black card color instead of creating a default white material");
   Check(timer==null,"GPU completion is driven by compositor notification without an independent duration timer");
   Pump(100);
   Check(NativeWindow.GetBounds(new WindowInteropHelper(card).Handle)==card.TargetPixels&&!sessions[0].Disposed,"real HWND remains at the target while compositor animation is in progress");
   Pump(650);Check(!sessions[0].Disposed,"elapsed UI time alone never destroys a still-active compositor host");
   card.CompositionAnimationCompleted(sessions[0].Request.Token);
   Check(!sessions[0].Disposed&&sessions[0].Events.Contains("reveal"),"entry handoff reveals real card while retaining the last proxy frame");
   Pump(60);Check(sessions[0].Disposed&&card.IsVisible,"compositor completion restores visible real card and releases proxy");
   Check(sessions[0].Events.IndexOf("reveal")<sessions[0].Events.IndexOf("presented")&&sessions[0].Events.IndexOf("presented")<sessions[0].Events.IndexOf("dispose"),"real card is presented before proxy resources are destroyed");
   bool finished=false;card.FadeOut(delegate {finished=true;});
   Check(sessions.Count==2&&!sessions[1].Request.Entering&&sessions[1].Request.Seconds==.33,"exit submits one compositor animation with requested duration");
   card.CompositionAnimationCompleted(sessions[0].Request.Token);
   Check(!sessions[1].Disposed,"a delayed completion from an older slide cannot terminate the current animation");
   card.CompositionAnimationCompleted(sessions[1].Request.Token);
   Check(finished&&sessions[1].Disposed&&!card.IsVisible,"exit hides the card before releasing the composition host");
   card.ShowCard();var interrupted=sessions[sessions.Count-1];card.HideCard();
   Check(interrupted.Disposed&&!card.IsVisible,"hiding during a compositor animation cancels and releases its host");
   card.CompositionSlideFactory=delegate{return null;};card.ShowCard();
   timer=(DispatcherTimer)typeof(NotificationWindow).GetField("animation",flags).GetValue(card);
   Check(timer.Interval.TotalMilliseconds<10,"unavailable GPU animation retains existing CPU fallback");
   card.HideCard();CompositionSlideFixture invisible=null;
   card.CompositionSlideFactory=delegate(CompositionSlideRequest request){invisible=new CompositionSlideFixture {Request=request,Visible=false};return invisible;};
   card.ShowCard();timer=(DispatcherTimer)typeof(NotificationWindow).GetField("animation",flags).GetValue(card);
   Check(invisible.Disposed&&timer.Interval.TotalMilliseconds<10,"an API-successful but cloaked GPU host is released and replaced with a visible CPU slide");
   card.HideCard();CompositionSlideFixture laterHidden=null;
   card.CompositionSlideFactory=delegate(CompositionSlideRequest request){laterHidden=new CompositionSlideFixture {Request=request};return laterHidden;};
   card.ShowCard();laterHidden.Visible=false;Pump(80);
   timer=(DispatcherTimer)typeof(NotificationWindow).GetField("animation",flags).GetValue(card);
   Check(laterHidden.Disposed&&timer.Interval.TotalMilliseconds<10,"host visibility is checked again after presentation to avoid an invisible animation followed by an instant card");
  }finally{card.Close();}
 }
}
}
