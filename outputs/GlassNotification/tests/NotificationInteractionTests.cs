using System;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
namespace Glass {
public static partial class Tests {
 static void NotificationInteractionTests(){
  var card=new NotificationWindow();
  try{
   var monitor=NativeWindow.GetMonitors()[0];var settings=new AppSettings {Mode=BackdropMode.Acrylic};
   card.Apply(settings,monitor);card.ShowCard();Pump(750);
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var backdrop=(WindowBackdropService)typeof(NotificationWindow).GetField("backdrop",flags).GetValue(card);
   var nativeField=typeof(WindowBackdropService).GetField("native",flags);
   var original=nativeField.GetValue(backdrop) as IDisposable;if(original!=null)original.Dispose();
   var device=new ContentDevice();nativeField.SetValue(backdrop,device);backdrop.Refresh();Pump(30);
   var refreshField=typeof(NotificationWindow).GetField("contentRefresh",BindingFlags.Instance|BindingFlags.NonPublic);
   int uploads=device.PresentCalls;
   settings.Width=400;card.Apply(settings,monitor);card.ShowCard();
   Check(device.PresentCalls==uploads,"preview changes do not capture and upload foreground pixels inside the input event");
   var pending=refreshField.GetValue(card) as DispatcherOperation;
   Check(pending!=null&&pending.Status==DispatcherOperationStatus.Pending,"changing a visible preview defers foreground capture until after input handling");
   for(int i=0;i<10;i++){settings.Width=410+i*10;card.Apply(settings,monitor);card.ShowCard();}
   Check(ReferenceEquals(pending,refreshField.GetValue(card)),"rapid settings changes share one queued foreground capture");
   Pump(100);
   Check(pending.Status==DispatcherOperationStatus.Completed&&card.Width==500,"queued preview catches up with the final slider value");
   Check(device.PresentCalls==uploads+1&&device.Width==500,"a burst of slider changes uploads only the final foreground size");
   settings.Width=480;card.Apply(settings,monitor);card.HideCard();Pump(50);
   Check(!card.IsVisible,"queued drawing does not reopen a hidden notification");
  }finally{card.Close();}
 }
}
}
