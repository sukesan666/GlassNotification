using System;
namespace Glass {
public static partial class Tests {
 static SystemNotification Notice(uint id){return new SystemNotification {Id=id,App="テスト",Title="タイトル"+id,Body="本文",Created=DateTimeOffset.UtcNow};}
 static void NotificationFeedTests(){
  var feed=new NotificationFeed(3);feed.Observe(new[]{Notice(1),Notice(2)});Check(feed.Take()==null,"listener baselines existing OS notifications without replay");
  feed.Observe(new[]{Notice(1),Notice(2),Notice(3)});Check(feed.Take().Id==3&&feed.Take()==null,"new Windows notification is forwarded once");
  feed.Observe(new[]{Notice(3)});Check(feed.Take()==null,"repeat snapshots and removals do not create cards");
  feed.Observe(new[]{Notice(4),Notice(5),Notice(6),Notice(7)});Check(feed.Take().Id==5&&feed.Take().Id==6&&feed.Take().Id==7&&feed.Take()==null,"flood queue stays bounded and preserves arrival order");
  feed.Reset();feed.Observe(new[]{Notice(9)});Check(feed.Take()==null,"reconnection baselines again without stale pending cards");
  feed.Observe(new[]{Notice(9),Notice(10)});feed.Observe(new[]{Notice(9)});Check(feed.Take()==null,"dismissed OS notifications are removed from the waiting card queue");
  feed.Observe(new[]{Notice(9),Notice(11),Notice(11)});Check(feed.Take().Id==11&&feed.Take()==null,"duplicate IDs within a snapshot cannot enqueue duplicate cards");
  feed.Observe(new[]{Notice(9),Notice(12)});feed.DiscardPending();Check(feed.Take()==null,"paused cards are discarded without resetting the active notification baseline");
  feed.Observe(new[]{Notice(9),Notice(12),Notice(13)});Check(feed.Take().Id==13&&feed.Take()==null,"notifications arriving after resume are still forwarded");
  feed.Reset();feed.Observe(new[]{Notice(20)});feed.Observe(new[]{Notice(20),Notice(21),Notice(22)},delegate(SystemNotification n){return n.Id!=21;});
  Check(feed.Take().Id==22&&feed.Take()==null,"sender filtering blocks only notifications from the selected sender");
  feed.Observe(new[]{Notice(20),Notice(21),Notice(22)},delegate(SystemNotification n){return n.Id!=21;});
  Check(feed.Take()==null,"blocked sender notifications are not replayed from subsequent snapshots");
  feed.Reset();var baseline=Notice(30);baseline.SenderId="baseline";feed.Observe(new[]{baseline});
  var allowedPending=Notice(31);allowedPending.SenderId="Sender.Allowed";var blockedPending=Notice(32);blockedPending.SenderId="Sender.Blocked";
  feed.Observe(new[]{baseline,allowedPending,blockedPending});feed.FilterPending(delegate(SystemNotification n){return n.SenderId!="Sender.Blocked";});
  Check(feed.Take().Id==31&&feed.Take()==null,"changing a sender to blocked removes only that sender's queued cards");
 }
}
}
