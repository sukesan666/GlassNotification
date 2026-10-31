using System;
namespace Glass {
public static partial class Tests {
 static partial void NotificationHistoryTests(){
  var history=new NotificationHistory();var first=new SystemNotification {Id=1};history.Add(first);
  Check(history.Items.Count==1&&ReferenceEquals(history.Items[0],first),"history stores received notifications for revisit");
  for(uint id=2;id<=NotificationHistory.Capacity+1;id++)history.Add(new SystemNotification {Id=id});
  Check(history.Items.Count==NotificationHistory.Capacity&&history.Items[0].Id==NotificationHistory.Capacity+1&&history.Items[NotificationHistory.Capacity-1].Id==2,"history keeps only the latest fifty notifications in newest-first order");
  history.Clear();Check(history.Items.Count==0,"history clear removes retained notifications");
 }
}
}
