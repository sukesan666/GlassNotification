using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
namespace Glass {
public sealed class OsNotificationPanel : StackPanel,IDisposable {
 readonly Window owner;readonly NotificationController controller;readonly TextBlock status;readonly Button listen,register,test,restore;readonly CheckBox banners,sounds;
 readonly DispatcherTimer timer;readonly NotificationFeed feed=new NotificationFeed(30);readonly OsNotificationPreferences preferences;
 readonly Func<bool> registrar;
 readonly INotificationSenderFilterStore senderFilters;readonly TextBlock senderFilterStatus;readonly StackPanel senderFilterRows;readonly Dictionary<string,CheckBox> senderChecks=new Dictionary<string,CheckBox>(StringComparer.Ordinal);readonly Dictionary<string,string> senderNames=new Dictionary<string,string>(StringComparer.Ordinal);
 internal event Action<SystemNotification> NotificationArrived;
 SystemNotification imageWaiting;DateTime imageDeadline;
 SystemNotification TakeWithImages(){
  if(imageWaiting==null){imageWaiting=feed.Take();imageDeadline=DateTime.UtcNow.AddSeconds(2.5);}
  if(imageWaiting==null)return null;
  if(!source.TryAttachImages(imageWaiting)&&DateTime.UtcNow<imageDeadline)return null;
  var result=imageWaiting;imageWaiting=null;return result;
 }
 SystemNotificationSource source;FileStream ownership;readonly string ownershipPath;bool active,connected,disposed,registering,syncing,paused,pollingSuspended;DateTime nextCard,nextPreferences;
 internal bool IsPollingSuspended {get{return pollingSuspended;}}
 public OsNotificationPanel(Window window,NotificationController target):this(window,target,SystemNotificationSource.Register,null){}
 public OsNotificationPanel(Window window,NotificationController target,Func<bool> registerAction):this(window,target,registerAction,null){}
 internal OsNotificationPanel(Window window,NotificationController target,Func<bool> registerAction,INotificationSenderFilterStore filters){owner=window;controller=target;registrar=registerAction;
  Margin=new Thickness(0,16,0,12);Children.Add(new TextBlock {Text="Windows・アプリ通知の連動設定",FontWeight=FontWeights.SemiBold});
  register=AddButton("Windows通知の連動版を開く","OsRegister");listen=AddButton("通知の連動を開始","OsListen");
  banners=AddCheck("Windows標準の通知バナーを非表示にする","OsBanners");sounds=AddCheck("Windows標準の通知音を鳴らさない","OsSounds");
  restore=AddButton("Windowsの通知設定を元に戻す","OsRestore");test=new Button {Visibility=Visibility.Collapsed};owner.RegisterName("OsTest",test);
  status=new TextBlock {Text="連動は停止中です。初回はWindowsの「通知へのアクセス」許可が必要です。",TextWrapping=TextWrapping.Wrap,FontSize=11,Margin=new Thickness(0,6,0,0)};Children.Add(status);
  Children.Add(new TextBlock {Text="Windowsのアクションセンター（通知履歴）には通知を残します。通知バナーの抑制を設定すると、Glassカードとの二重表示を防ぎます。",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,8,0,0)});
  string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GlassNotification.OSLink");
  senderFilters=filters??new NotificationSenderFilterStore(Path.Combine(folder,"notification-filters.json"));
  senderFilterStatus=new TextBlock {Text=senderFilters.ErrorMessage??"新しい通知元は許可されます。受信開始後に一覧へ表示します。",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.Gray,Margin=new Thickness(0,8,0,4)};
  Children.Add(new TextBlock {Text="通知元フィルター",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,0)});Children.Add(senderFilterStatus);
  senderFilterRows=new StackPanel();var senderScroll=new ScrollViewer {Content=senderFilterRows,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Children.Add(senderScroll);
  foreach(string savedId in senderFilters.RuleIds())EnsureSenderRow(savedId,savedId);
  if(senderFilters.IsAvailable&&senderChecks.Count>0)SetSenderStatus("通知元ごとに表示を許可またはブロックできます。");
  string path=Path.Combine(folder,"os-notifications-recovery.json");preferences=new OsNotificationPreferences(new WindowsNotificationPreferenceStore(),path);ownershipPath=Path.Combine(folder,"preferences.lock");
  timer=new DispatcherTimer(DispatcherPriority.Background) {Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=delegate {Tick();};
  listen.Click+=delegate {if(active)Stop("通知の連動を停止しました。");else Start();};
  register.Click+=delegate {Register();};
  banners.Checked+=PreferencesChanged;banners.Unchecked+=PreferencesChanged;sounds.Checked+=PreferencesChanged;sounds.Unchecked+=PreferencesChanged;
  restore.Click+=delegate {try{if(!Acquire())return;preferences.Restore();SetChecks(false,false);SetStatus("Windowsの通知設定を元に戻しました。");}catch(Exception e){Error("通知設定を戻せませんでした",e);}};
  test.Click+=delegate {try{SystemNotificationSource.SendTest();SetStatus("確認用のテスト通知を送信しました。Glassカードが届くことを確認してください。");}catch(Exception e){Error("確認用通知を送れませんでした",e);}};
  Loaded+=delegate {try{if(preferences.Recoverable&&Acquire()){preferences.Restore();SetStatus("前回の一時設定を元に戻しました。連動は停止中です。");}}catch(Exception e){Error("前回のChrome設定を戻せませんでした",e);}};
 }
 Button AddButton(string label,string name){var button=new Button {Content=label,Margin=new Thickness(0,6,0,0),Padding=new Thickness(9,6,9,6),HorizontalContentAlignment=HorizontalAlignment.Left};owner.RegisterName(name,button);Children.Add(button);return button;}
 CheckBox AddCheck(string label,string name){var check=new CheckBox {Content=label,Margin=new Thickness(0,8,0,0)};owner.RegisterName(name,check);Children.Add(check);return check;}
 string CurrentLanguage {get{var controlWindow=owner as ControlWindow;return controlWindow==null?UiLocalization.Language:controlWindow.CurrentSettings.Language;}}
 void SetStatus(string value){status.Text=UiLocalization.Translate(value,CurrentLanguage);}
 void SetSenderStatus(string value){senderFilterStatus.Text=UiLocalization.Translate(value,CurrentLanguage);}
 internal void ApplyLanguage(){UiLocalization.Apply(this,CurrentLanguage);UpdateControls();}
 bool Acquire(){if(ownership!=null)return true;try{Directory.CreateDirectory(Path.GetDirectoryName(ownershipPath));ownership=new FileStream(ownershipPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);return true;}catch(IOException){SetStatus("通知連動の処理中です。少し待ってから再試行してください。");return false;}catch(Exception e){Error("通知の連動を開始できませんでした",e);return false;}}
 void Start(){if(disposed||!Acquire())return;try{
   NotificationResourceCache.EnsureDefaultWatcher();
   if(preferences.Recoverable){preferences.Restore();SetStatus("通知アクセスを確認しています。");}
   source=new SystemNotificationSource();int access=source.Access(true);active=true;connected=false;feed.Reset();imageWaiting=null;nextCard=DateTime.MinValue;
   if(!pollingSuspended||registering)timer.Start();
   SetStatus(access==-1?"Windowsの「設定 > システム > 通知」で通知へのアクセスを許可してください。":"通知へのアクセスを確認しています。");UpdateControls();
  }catch(Exception e){if(source!=null){source.Dispose();source=null;}Error("通知の連動を開始できませんでした",e);UpdateControls();}}
 internal void StartIntegration(){if(disposed||active||registering)return;Start();}
 internal void SetPollingSuspended(bool value){
  if(disposed||pollingSuspended==value)return;
  pollingSuspended=value;
  if(pollingSuspended){
   if(!registering)timer.Stop();
  }else{
   if(active||registering)timer.Start();
  }
 }
 void Tick(){if(disposed)return;try{
   if(registering){if(registrar()){registering=false;if(pollingSuspended||!active)timer.Stop();SetStatus("登録しました。連動版を開きます。");LaunchLinkedApp();UpdateControls();}if(pollingSuspended||!active)return;}
   if(pollingSuspended){timer.Stop();return;}
   if(!active||source==null)return;int access=source.Access(false);if(access==-1)return;if(access!=1){Stop("通知アクセスが許可されていないため停止しました。Chromeの「通知アクセス」設定を確認してください。");return;}
   var snapshot=source.Read();if(snapshot!=null){
    if(imageWaiting!=null){bool present=false;foreach(var item in snapshot){if(item.Id==imageWaiting.Id){present=true;if(imageWaiting.AppIcon==null&&item.AppIcon!=null)imageWaiting.AppIcon=item.AppIcon;}}if(!present||!senderFilters.IsAllowed(imageWaiting.SenderId))imageWaiting=null;}
    UpdateSenderList(snapshot);var arrivingImages=new List<SystemNotification>();
    feed.Observe(snapshot,delegate(SystemNotification n){return senderFilters.IsAllowed(n.SenderId);},arrivingImages.Add);
    foreach(var pending in feed.Pending)source.TryAttachImages(pending);
    source.CaptureImages(arrivingImages,feed.Pending,imageWaiting);
    if(!connected){
     connected=true;
     SetStatus("アプリ・システム通知の受信を開始しました。");
     UpdateControls();
     try{
      SetChecks(true,true);
      if(Acquire())preferences.Apply(true,true);
     }catch(Exception ex){
      Error("標準通知の抑制設定を反映できませんでした",ex);
     }
    }
   }
   if(paused){SystemNotification pausedNotification;while((pausedNotification=TakeWithImages())!=null)if(NotificationArrived!=null)NotificationArrived(pausedNotification);}
   else{
    int shownInTick=0;
    while(controller==null||controller.HasAvailableSlot){
     var n=TakeWithImages();if(n==null)break;
     if(senderFilters.IsAllowed(n.SenderId)){
      if(NotificationArrived!=null)NotificationArrived(n);
      if(controller!=null)controller.ShowNotification(n,shownInTick==0);
      shownInTick++;
      SetStatus("届いた通知をカードに表示しました。");
     }
    }
   }
   if(connected&&DateTime.UtcNow>=nextPreferences){nextPreferences=DateTime.UtcNow.AddSeconds(5);if(banners.IsChecked==true||sounds.IsChecked==true)preferences.Apply(banners.IsChecked==true,sounds.IsChecked==true);}
  }catch(Exception e){bool failedRegistration=registering;registering=false;Stop("連動中に問題が発生したため停止しました。");Error(failedRegistration?"Chromeに登録できませんでした":"Chrome連動中に問題が発生しました",e);}}
 void PreferencesChanged(object sender,RoutedEventArgs args){if(syncing||disposed)return;if(!connected){SetChecks(false,false);return;}try{if(!Acquire())return;preferences.Apply(banners.IsChecked==true,sounds.IsChecked==true);SetStatus("標準通知の抑制設定を反映しました。");}catch(Exception e){Error("標準通知の抑制設定を反映できませんでした",e);}}
 void SetChecks(bool banner,bool sound){syncing=true;try{banners.IsChecked=banner;sounds.IsChecked=sound;}finally{syncing=false;}}
 void UpdateSenderList(IEnumerable<SystemNotification> notifications){
  int previousCount=senderChecks.Count;
  foreach(var notification in notifications){
   string id=notification.SenderId;if(string.IsNullOrWhiteSpace(id))continue;
   EnsureSenderRow(id,string.IsNullOrWhiteSpace(notification.App)?id:notification.App);
  }
  if(senderFilters.IsAvailable&&senderChecks.Count!=previousCount)SetSenderStatus(senderChecks.Count==0?"受信を開始すると、見つかった通知元がここに表示されます。":"通知元ごとに表示を許可またはブロックできます。");
 }
 void EnsureSenderRow(string id,string app){
  CheckBox check;
  if(!senderChecks.TryGetValue(id,out check)){
   check=new CheckBox {Tag=id,Margin=new Thickness(0,3,0,3),Foreground=Brushes.DimGray,IsEnabled=senderFilters.IsAvailable};
   var controlWindow=owner as ControlWindow;if(controlWindow!=null)controlWindow.ApplyTheme(check);
   check.Checked+=SenderFilterChanged;check.Unchecked+=SenderFilterChanged;senderChecks.Add(id,check);senderFilterRows.Children.Add(check);
  }
  senderNames[id]=app;check.Content=UiLocalization.Translate(app+" — 通知を表示",CurrentLanguage);check.ToolTip=id;
  bool allowed=senderFilters.IsAllowed(id);if(check.IsChecked!=allowed){syncing=true;try{check.IsChecked=allowed;}finally{syncing=false;}}
 }
 internal void ObserveSenders(IEnumerable<SystemNotification> notifications){UpdateSenderList(notifications);}
 void SenderFilterChanged(object sender,RoutedEventArgs args){
  if(syncing||disposed)return;var check=(CheckBox)sender;string id=(string)check.Tag;bool allowed=check.IsChecked==true;
  try{senderFilters.SetAllowed(id,allowed);feed.FilterPending(delegate(SystemNotification n){return senderFilters.IsAllowed(n.SenderId);});if(imageWaiting!=null&&!senderFilters.IsAllowed(imageWaiting.SenderId))imageWaiting=null;if(source!=null)source.RetainImages(feed.Pending,imageWaiting);SetSenderStatus((allowed?"許可":"ブロック")+"を保存しました： "+senderNames[id]);}
  catch(Exception e){syncing=true;try{check.IsChecked=senderFilters.IsAllowed(id);}finally{syncing=false;}SetSenderStatus("通知元フィルターを保存できませんでした。"+e.Message);}
 }
 public void SetPaused(bool value){if(disposed||paused==value)return;paused=value;if(paused){feed.DiscardPending();imageWaiting=null;if(source!=null)source.RetainImages(feed.Pending,null);}}
 void Stop(string message){active=false;connected=false;timer.Stop();if(source!=null){source.Dispose();source=null;}feed.Reset();imageWaiting=null;try{if(ownership!=null)preferences.Restore();SetChecks(false,false);SetStatus(message);}catch(Exception e){Error("Chrome通知設定の停止処理に失敗しました",e);}finally{UpdateControls();}}
 void UpdateControls(){banners.IsEnabled=sounds.IsEnabled=connected&&!registering;test.IsEnabled=connected;listen.IsEnabled=!registering;listen.Content=UiLocalization.Translate(active?"通知の連動を停止":"通知の連動を開始",CurrentLanguage);register.IsEnabled=!active&&!registering;restore.IsEnabled=!active;}
 internal const string LinkedExecutableName="GlassNotification.Linked.exe";
 internal static ProcessStartInfo LinkedAppStartInfo(string registeredAppId,string baseDirectory){
  return string.IsNullOrWhiteSpace(registeredAppId)?new ProcessStartInfo(Path.Combine(baseDirectory,LinkedExecutableName)){UseShellExecute=true}:new ProcessStartInfo("explorer.exe","shell:AppsFolder\\"+registeredAppId){UseShellExecute=true};
 }
 static void LaunchLinkedApp(){Process.Start(LinkedAppStartInfo(SystemNotificationSource.RegisteredAppId(),AppDomain.CurrentDomain.BaseDirectory));}
 // Registration finishes asynchronously: a false result means "still running", so keep polling from Tick.
 void Register(){if(active||registering||disposed)return;try{registering=true;SetStatus("Chromeに連動版を登録しています…");UpdateControls();if(registrar()){registering=false;SetStatus("登録しました。連動版を開きます。");LaunchLinkedApp();UpdateControls();}else timer.Start();}catch(Exception e){registering=false;timer.Stop();SetStatus("Chrome連動版の登録に失敗しました。");Error("Chrome連動版の登録に失敗しました",e);UpdateControls();}}
 void Error(string message,Exception e){SetStatus(UiLocalization.Translate(message,CurrentLanguage)+"。"+(e is InvalidOperationException||e is InvalidDataException||e is IOException?e.Message:e is System.Runtime.InteropServices.COMException?e.Message:UiLocalization.Translate("Chrome通知連動時に問題が発生しました。",CurrentLanguage)));}
 public void Dispose(){if(disposed)return;Stop("通知の連動を終了しました。");disposed=true;if(preferences.Recoverable&&ownership!=null)MessageBox.Show(UiLocalization.Translate("Chromeの通知設定を完全に元に戻すには、通知アクセスの許可を手動で解除してください。",CurrentLanguage),UiLocalization.Translate("Glass Notification",CurrentLanguage),MessageBoxButton.OK,MessageBoxImage.Information);if(ownership!=null){ownership.Dispose();ownership=null;}}
 }
}
