using System;
using System.Collections.ObjectModel;
namespace Glass {
internal sealed class NotificationHistory {
 public const int Capacity=50;
 public ObservableCollection<SystemNotification> Items {get;private set;}
 public NotificationHistory(){Items=new ObservableCollection<SystemNotification>();}
 public void Add(SystemNotification notification){
  if(notification==null)throw new ArgumentNullException("notification");
  Items.Insert(0,notification);
  while(Items.Count>Capacity)Items.RemoveAt(Items.Count-1);
 }
 public void Clear(){Items.Clear();}
}
}
