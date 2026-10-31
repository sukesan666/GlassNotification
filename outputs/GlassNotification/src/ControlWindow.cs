using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
namespace Glass {
public sealed class ControlWindow : Window {
 readonly NotificationWindow card;readonly NotificationWindow[] cards;readonly NotificationController controller;readonly string settingsPath;
 readonly StackPanel panel;readonly TextBlock status,fileName;readonly Slider cardWidth,cardHeight,cardFont,margin,displaySeconds,imageSize;
 readonly TextBlock widthValue,heightValue,fontValue,marginValue,displaySecondsValue,imageSizeValue;readonly ComboBox display,position,sound;readonly Button preview;
 readonly ComboBox backdropMode,backdropStrength,cardFontFamily;readonly CheckBox roundedCorners;readonly TextBlock backdropStatus;
 readonly ComboBox cardColor,textColor,uiScale,themeMode,language,imagePosition;readonly Button showSample;readonly CheckBox startWithWindows,startMinimizedToTray,highContrast;
 readonly OsNotificationPanel osNotifications;
 readonly IStartupRegistration startupRegistration;
 readonly IAppRuntimeStateStore runtimeStateStore;readonly AppRuntimeState runtimeState;
 readonly Forms.NotifyIcon trayIcon;readonly Forms.ContextMenuStrip trayMenu;
 readonly System.Drawing.Icon trayApplicationIcon;
 readonly Forms.ToolStripMenuItem trayOpen,trayPause,trayResume,trayExit;
 readonly NotificationHistory notificationHistory;readonly ListBox historyList;readonly Button openHistory,replayHistory,clearHistory;readonly TextBlock historyStatus;
 readonly INotificationActivator notificationActivator;
 MonitorInfo[] monitors;bool syncing,closed;long soundRequest;double appliedUiScale=100;Panel target;
 internal bool IsPaused {get;private set;}
 internal bool IsInTray {get;private set;}
 internal bool IsTrayIconVisible {get{return trayIcon.Visible;}}
 public AppSettings CurrentSettings {get{return controller.Settings.Copy();}}
 static bool IsChromeLinkedExecutable() {
  var entry=Assembly.GetEntryAssembly();
  if(entry==null)return false;
  string name=Path.GetFileName(entry.Location);
  return name.Equals(OsNotificationPanel.LinkedExecutableName,StringComparison.OrdinalIgnoreCase);
 }
 public ControlWindow(string path):this(path,new WindowsStartupRegistration(new RegistryStartupCommandStore(),System.Reflection.Assembly.GetEntryAssembly().Location,PackageRuntime.Current),null,new AppRuntimeStateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GlassNotification.OSLink","runtime-state.json")),null) {}
 internal ControlWindow(string path,IStartupRegistration startup):this(path,startup,null,null,null) {}
 internal ControlWindow(string path,IStartupRegistration startup,INotificationSenderFilterStore senderFilters):this(path,startup,senderFilters,null,null) {}
 internal ControlWindow(string path,IStartupRegistration startup,INotificationSenderFilterStore senderFilters,IAppRuntimeStateStore runtimeStore):this(path,startup,senderFilters,runtimeStore,null) {}
 internal ControlWindow(string path,IStartupRegistration startup,INotificationSenderFilterStore senderFilters,IAppRuntimeStateStore runtimeStore,INotificationActivator activator) {
  NameScope.SetNameScope(this,new NameScope());
  settingsPath=path;startupRegistration=startup;notificationActivator=activator??new ChromeNotificationActivator();
  runtimeStateStore=runtimeStore??new AppRuntimeStateStore(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),"runtime-state.json"));runtimeState=runtimeStateStore.Load();string warning;var initial=SettingsStore.Load(settingsPath,out warning);UiLocalization.Language=initial.Language;Title="Glass Notification — 通知のプレビュー";Width=860;Height=720;MinWidth=780;MinHeight=520;Icon=LoadApplicationIcon();
  WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brush("#F5F7FA");FontFamily=new FontFamily("Yu Gothic UI");FontSize=13;
  var scroll=new ScrollViewer {Background=Background,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false};VirtualizingPanel.SetScrollUnit(scroll,ScrollUnit.Pixel);
   panel=new StackPanel {Margin=new Thickness(28,20,28,20)};scroll.Content=panel;Content=scroll;
   showSample=Button("通知をまとめてテスト","ShowSample");showSample.Background=Brush("#54769F");showSample.Foreground=Brushes.White;showSample.BorderThickness=new Thickness(0);showSample.Padding=new Thickness(12,11,12,11);panel.Children.Add(showSample);
   panel.Children.Add(new TextBlock {Text="現在のカード表示・文字設定と、選択中の通知音を確認します。音なし設定では音は鳴りません。",TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brush("#637286"),Margin=new Thickness(0,5,0,0)});
   var columns=new Grid {Margin=new Thickness(0,10,0,0)};
   columns.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
   columns.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(24)});
   columns.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
   panel.Children.Add(columns);
   var leftCol=new StackPanel();Grid.SetColumn(leftCol,0);columns.Children.Add(leftCol);
   var rightCol=new StackPanel();Grid.SetColumn(rightCol,2);columns.Children.Add(rightCol);
   target=leftCol;Section("基本設定");
   themeMode=ComboRow("テーマ","ThemeMode");themeMode.ItemsSource=new[]{"システムに合わせる","ライト","ダーク"};
   uiScale=ComboRow("アプリの拡大率","UiScale");uiScale.ItemsSource=new[]{"100%","110%","120%","130%","140%","150%"};
   display=ComboRow("画面","CardDisplay");position=ComboRow("位置","CardPosition");position.ItemsSource=new[]{"右上","左上","右下","左下"};
   displaySeconds=SliderRow("表示時間（秒）",1,60,1,"DisplaySeconds",out displaySecondsValue);
   imageSize=SliderRow("通知画像の大きさ",24,128,4,"ImageSize",out imageSizeValue);
   imageSize.ToolTip="拡大枠の画像の大きさを調整します。";
   imagePosition=ComboRow("通知画像の位置","ImagePosition");imagePosition.ItemsSource=new[]{"右","左"};
   imagePosition.ToolTip="本文画像、送信者アイコン、アプリアイコンの順で表示します。";
   sound=ComboRow("音の種類","CardSound");sound.ItemsSource=new[]{"音なし","標準の音","ファイルを選ぶ"};
   var soundRow=new Grid {Margin=new Thickness(0,6,0,0)};soundRow.ColumnDefinitions.Add(new ColumnDefinition());soundRow.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
   var choose=Button("WAVを選択…","ChooseSound");preview=Button("試聴","PreviewSound");Grid.SetColumn(preview,1);choose.Margin=new Thickness(0,0,8,0);soundRow.Children.Add(choose);soundRow.Children.Add(preview);leftCol.Children.Add(soundRow);
   fileName=new TextBlock {TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Brush("#748194"),Margin=new Thickness(0,7,0,0)};leftCol.Children.Add(fileName);
   leftCol.Children.Add(new TextBlock {Text="PCM WAV（最大5秒・8/16bit・8〜48kHz）",FontSize=11,Foreground=Brush("#8994A3"),Margin=new Thickness(0,3,0,0)});
   var appearanceGroup=Group("外観の詳細（サイズ・フォント・すりガラス）","AppearanceDetails",leftCol);
   var chromeGroup=Group("アプリ・システム通知の連動とフィルター","ChromeIntegration",rightCol);
   var historyGroup=Group("通知履歴（起動中のみ・最大50件）","NotificationHistoryGroup",rightCol);
   var systemGroup=Group("システム・詳細設定（自動起動・言語など）","SystemSettings",rightCol);
  target=appearanceGroup;
  backdropMode=ComboRow("背景","BackdropMode");
  backdropMode.ItemsSource=new[]{"Acrylic：すりガラス（推奨）","Mica：壁紙になじむ背景","オフ：不透明"};
  backdropStrength=ComboRow("すりガラスの強さ","BackdropStrength");backdropStrength.ItemsSource=new[]{"弱","標準","強"};backdropStrength.ToolTip="強さは色の重なりを調整します。ぼかし半径はWindowsが管理します。";
  backdropStatus=new TextBlock {TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brush("#637286"),Margin=new Thickness(0,3,0,0)};target.Children.Add(backdropStatus);
  cardWidth=SliderRow("カードの幅",280,600,10,"CardWidth",out widthValue);
  cardHeight=SliderRow("カードの高さ",88,240,4,"CardHeight",out heightValue);
  cardFontFamily=ComboRow("通知のフォント","CardFontFamily");VirtualizingPanel.SetIsVirtualizing(cardFontFamily,true);VirtualizingPanel.SetVirtualizationMode(cardFontFamily,VirtualizationMode.Recycling);cardFontFamily.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));cardFontFamily.ItemsSource=NotificationFonts.Names;cardFontFamily.IsTextSearchEnabled=true;cardFontFamily.MaxDropDownHeight=260;
  cardFont=SliderRow("文字サイズ",11,24,1,"CardFont",out fontValue);
  roundedCorners=new CheckBox {Content="通知カードの角を丸くする",Margin=new Thickness(0,8,0,0), Foreground=Brush("#637286")};RegisterName("CardRoundedCorners",roundedCorners);target.Children.Add(roundedCorners);
  highContrast=new CheckBox {Content=new TextBlock {Text="高コントラスト表示",TextWrapping=TextWrapping.Wrap},Margin=new Thickness(0,5,0,0),Foreground=Brush("#637286")};RegisterName("HighContrast",highContrast);target.Children.Add(highContrast);
  cardColor=ColorRow("カードの色","CardColor",true);textColor=ColorRow("文字の色","TextColor",false);
  margin=SliderRow("端からの余白",0,120,4,"CardMargin",out marginValue);
  target=systemGroup;
  startWithWindows=new CheckBox {Content="Windowsへのサインイン時に起動する",Margin=new Thickness(0,5,0,0),Foreground=Brush("#637286")};RegisterName("StartWithWindows",startWithWindows);target.Children.Add(startWithWindows);
  startMinimizedToTray=new CheckBox {Content="通知領域に最小化して起動する",Margin=new Thickness(0,7,0,0),Foreground=Brush("#637286")};RegisterName("StartMinimizedToTray",startMinimizedToTray);target.Children.Add(startMinimizedToTray);
  language=ComboRow("言語","Language");language.ItemsSource=new[]{"日本語","English"};
  target=panel;
  status=new TextBlock {TextWrapping=TextWrapping.Wrap,Foreground=Brush("#748194"),FontSize=12,Margin=new Thickness(0,14,0,10)};panel.Children.Add(status);
  var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
  var reset=Button("初期値に戻す","ResetSettings");var exit=Button("終了","ExitApp");Grid.SetColumn(exit,1);reset.Margin=new Thickness(0,0,8,0);actions.Children.Add(reset);actions.Children.Add(exit);panel.Children.Add(actions);
  card=new NotificationWindow();
  cards=new[]{card,new NotificationWindow(),new NotificationWindow()};
  foreach(var c in cards){var current=c;current.DpiEnvironmentChanged+=DisplayChanged;current.BackdropChanged+=BackdropChanged;current.NotificationClicked+=delegate(SystemNotification n){CardClicked(current,n);};current.NotificationDismissRequested+=delegate {if(!closed)controller.DismissCard(current);};}
  var audio=new SoundService(new WaveOutput(),PackageRuntime.Current.ChimePath);
  controller=new NotificationController(cards,audio,new DispatcherScheduler(),ApplySettings,Persist,Report);
  notificationHistory=new NotificationHistory();
  osNotifications=new OsNotificationPanel(this,controller,SystemNotificationSource.Register,senderFilters);osNotifications.Margin=new Thickness(0,0,0,4);chromeGroup.Children.Add(osNotifications);
  var historyPanel=historyGroup;
  historyList=new ListBox {Name="NotificationHistory",ItemsSource=notificationHistory.Items,MaxHeight=190,MinHeight=60,SelectionMode=SelectionMode.Single,BorderBrush=Brush("#DDE3EB"),Background=Brushes.White};
  RegisterName("NotificationHistory",historyList);historyList.ItemTemplate=HistoryTemplate();historyList.SelectionChanged+=delegate {RefreshHistoryControls();};historyList.MouseDoubleClick+=delegate {OpenSelectedNotification();};historyPanel.Children.Add(historyList);
  var historyActions=new Grid {Margin=new Thickness(0,5,0,0)};historyActions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});historyActions.ColumnDefinitions.Add(new ColumnDefinition());historyActions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
  openHistory=Button("通知元アプリで開く","OpenHistory");openHistory.Padding=new Thickness(9,5,9,5);openHistory.Margin=new Thickness(0,0,6,0);openHistory.Click+=delegate {OpenSelectedNotification();};historyActions.Children.Add(openHistory);
  replayHistory=Button("選択した通知を再表示","ReplayHistory");replayHistory.Padding=new Thickness(9,5,9,5);Grid.SetColumn(replayHistory,1);replayHistory.Click+=delegate {ReplaySelectedNotification();};historyActions.Children.Add(replayHistory);
  clearHistory=Button("履歴を消去","ClearHistory");clearHistory.Padding=new Thickness(9,5,9,5);clearHistory.Margin=new Thickness(6,0,0,0);Grid.SetColumn(clearHistory,2);clearHistory.Click+=delegate {notificationHistory.Clear();historyStatus.Text=UiLocalization.Translate("履歴を消去しました。",CurrentLanguage);RefreshHistoryControls();};historyActions.Children.Add(clearHistory);historyPanel.Children.Add(historyActions);
  historyStatus=new TextBlock {FontSize=11,Foreground=Brush("#748194"),Margin=new Thickness(0,4,0,0)}; historyPanel.Children.Add(historyStatus);
  osNotifications.NotificationArrived+=RecordNotification;
  trayMenu=new Forms.ContextMenuStrip();
  trayOpen=new Forms.ToolStripMenuItem("開く");trayMenu.Items.Add(trayOpen);trayOpen.Click+=delegate {Dispatcher.BeginInvoke(new Action(RestoreFromTray));};
  trayPause=new Forms.ToolStripMenuItem("一時停止");trayMenu.Items.Add(trayPause);trayPause.Click+=delegate {Dispatcher.BeginInvoke(new Action(delegate {SetPaused(true);}));};
  trayResume=new Forms.ToolStripMenuItem("再開");trayMenu.Items.Add(trayResume);trayResume.Click+=delegate {Dispatcher.BeginInvoke(new Action(delegate {SetPaused(false);}));};
  trayMenu.Items.Add(new Forms.ToolStripSeparator());
  trayExit=new Forms.ToolStripMenuItem("終了");trayMenu.Items.Add(trayExit);trayExit.Click+=delegate {Dispatcher.BeginInvoke(new Action(Close));};
  trayApplicationIcon=new System.Drawing.Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","AppIcon.ico"));
  trayIcon=new Forms.NotifyIcon {Icon=trayApplicationIcon,Text="Glass Notification",ContextMenuStrip=trayMenu,Visible=true};
  trayIcon.DoubleClick+=delegate {Dispatcher.BeginInvoke(new Action(RestoreFromTray));};
  trayPause.Enabled=true;trayResume.Enabled=false;
  controller.Initialize(initial);string startupWarning=null;
  if(initial.StartWithWindows)try{startupRegistration.SetEnabled(true);}catch(Exception e){startupWarning="Windows起動時の登録を適用できませんでした。"+e.Message;}
  Sync();ApplyLanguage();ApplyTheme();UpdateTrayLanguage();if(warning!=null)Report(warning);if(startupWarning!=null)Report(startupWarning);
  cardWidth.ValueChanged+=delegate {Change(delegate(AppSettings s){s.Width=cardWidth.Value;});};
  backdropMode.SelectionChanged+=delegate {if(backdropMode.SelectedIndex>=0)Change(delegate(AppSettings s){s.Mode=backdropMode.SelectedIndex==0?BackdropMode.Acrylic:backdropMode.SelectedIndex==1?BackdropMode.Mica:BackdropMode.Off;});};
  backdropStrength.SelectionChanged+=delegate {if(backdropStrength.SelectedIndex>=0)Change(delegate(AppSettings s){s.BackdropStrength=(BackdropStrength)backdropStrength.SelectedIndex;});};
  cardHeight.ValueChanged+=delegate {Change(delegate(AppSettings s){s.Height=cardHeight.Value;});};
  cardFont.ValueChanged+=delegate {Change(delegate(AppSettings s){s.FontSize=cardFont.Value;});};
  cardFontFamily.SelectionChanged+=delegate {var family=cardFontFamily.SelectedItem as string;if(family!=null)Change(delegate(AppSettings s){s.FontFamilyName=family;});};
  roundedCorners.Checked+=delegate {Change(delegate(AppSettings s){s.RoundedCorners=true;});};
  roundedCorners.Unchecked+=delegate {Change(delegate(AppSettings s){s.RoundedCorners=false;});};
  displaySeconds.ValueChanged+=delegate {Change(delegate(AppSettings s){s.DisplaySeconds=displaySeconds.Value;});};
  imageSize.ValueChanged+=delegate {Change(delegate(AppSettings s){s.ImageSize=imageSize.Value;});};
  imagePosition.SelectionChanged+=delegate {if(imagePosition.SelectedIndex>=0)Change(delegate(AppSettings s){s.ImagePosition=imagePosition.SelectedIndex==1?"Left":"Right";});};
  highContrast.Checked+=delegate {Change(delegate(AppSettings s){s.HighContrast=true;});};
  highContrast.Unchecked+=delegate {Change(delegate(AppSettings s){s.HighContrast=false;});};
  cardColor.SelectionChanged+=delegate {var color=cardColor.SelectedItem as NotificationColorChoice;if(color!=null)Change(delegate(AppSettings s){s.CardColor=color.Value;});};
  textColor.SelectionChanged+=delegate {var color=textColor.SelectedItem as NotificationColorChoice;if(color!=null)Change(delegate(AppSettings s){s.TextColor=color.Value;});};
  margin.ValueChanged+=delegate {Change(delegate(AppSettings s){s.Margin=margin.Value;});};
  position.SelectionChanged+=delegate {if(position.SelectedIndex>=0)Change(delegate(AppSettings s){s.Corner=(Corner)position.SelectedIndex;});};
  display.SelectionChanged+=delegate {var m=display.SelectedItem as MonitorInfo;if(m!=null)Change(delegate(AppSettings s){s.MonitorId=m.Id;});};
  sound.SelectionChanged+=delegate {if(syncing)return;if(sound.SelectedIndex==2){ChooseSound();}else if(sound.SelectedIndex>=0){soundRequest++;Change(delegate(AppSettings s){s.SoundKind=(SoundKind)sound.SelectedIndex;});}};
  startWithWindows.Checked+=delegate {SetStartWithWindows(true);};startWithWindows.Unchecked+=delegate {SetStartWithWindows(false);};
  startMinimizedToTray.Checked+=delegate {Change(delegate(AppSettings s){s.StartMinimizedToTray=true;});};
  startMinimizedToTray.Unchecked+=delegate {Change(delegate(AppSettings s){s.StartMinimizedToTray=false;});};
  uiScale.SelectionChanged+=delegate {if(uiScale.SelectedIndex>=0)Change(delegate(AppSettings s){s.UiScalePercent=100+uiScale.SelectedIndex*10;});};
  themeMode.SelectionChanged+=delegate {if(themeMode.SelectedIndex>=0)Change(delegate(AppSettings s){s.ThemeMode=themeMode.SelectedIndex==0?"System":themeMode.SelectedIndex==1?"Light":"Dark";});};
  language.SelectionChanged+=delegate {if(language.SelectedIndex>=0)Change(delegate(AppSettings s){s.Language=language.SelectedIndex==0?"Japanese":"English";});};
  choose.Click+=delegate {ChooseSound();};preview.Click+=delegate {controller.PreviewSound();};
  showSample.Click+=delegate {ShowTestNotification();};
  reset.Click+=delegate {
   soundRequest++;
   try{if(controller.Settings.StartWithWindows)startupRegistration.SetEnabled(false);controller.Reset();Sync();ApplyLanguage();ApplyTheme();}
   catch(Exception e){Report("Windows起動登録を解除できなかったため、初期化しませんでした。"+e.Message);}
  };
  exit.Click+=delegate {Close();};
  Loaded+=delegate {
   controller.SetPaused(runtimeState.DoNotDisturb);IsPaused=runtimeState.DoNotDisturb;trayPause.Enabled=!IsPaused;trayResume.Enabled=IsPaused;
   if(IsChromeLinkedExecutable())osNotifications.StartIntegration();
   controller.ShowSample(false);Sync();CheckRestoredSound();
   if(controller.Settings.StartMinimizedToTray||runtimeState.MinimizedToTray)MinimizeToTray();
   if(runtimeStateStore.ErrorMessage!=null)Report(runtimeStateStore.ErrorMessage);
   else if(IsPaused)Report("休憩モード中です。通知の受信は継続し、カード表示と音を停止しています。");
  };
  StateChanged+=delegate {if(WindowState==WindowState.Minimized)MinimizeToTray();};
  Closed+=delegate {FlushPersist();closed=true;osNotifications.NotificationArrived-=RecordNotification;trayIcon.Visible=false;trayIcon.Dispose();trayApplicationIcon.Dispose();trayMenu.Dispose();osNotifications.Dispose();SystemEvents.DisplaySettingsChanged-=DisplayChanged;SystemEvents.UserPreferenceChanged-=UserPreferenceChanged;foreach(var c in cards){c.BackdropChanged-=BackdropChanged;c.DpiEnvironmentChanged-=DisplayChanged;}controller.Dispose();foreach(var c in cards)c.Close();};
  SourceInitialized+=delegate {ApplyTheme();};
  SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  SystemEvents.UserPreferenceChanged+=UserPreferenceChanged;
 }
 internal void MinimizeToTray(){
  if(closed||IsInTray)return;
  bool wasMinimized=runtimeState.MinimizedToTray;
  try{runtimeState.MinimizedToTray=true;runtimeStateStore.Save(runtimeState);}
  catch(Exception e){runtimeState.MinimizedToTray=wasMinimized;WindowState=WindowState.Normal;Report("前回状態を保存できないため、通知領域へ最小化できません。"+e.Message);return;}
  IsInTray=true;ShowInTaskbar=false;trayIcon.Visible=true;Hide();
 }
 internal void RestoreFromTray(){
  if(closed)return;
  bool wasMinimized=runtimeState.MinimizedToTray;
  try{runtimeState.MinimizedToTray=false;runtimeStateStore.Save(runtimeState);}
  catch(Exception e){runtimeState.MinimizedToTray=wasMinimized;Report("前回状態を保存できないため、ウィンドウを復元できません。"+e.Message);return;}
  IsInTray=false;trayIcon.Visible=true;ShowInTaskbar=true;WindowState=WindowState.Normal;Show();Activate();
 }
 internal void SetPaused(bool value){
  if(closed||IsPaused==value)return;
  bool wasPaused=runtimeState.DoNotDisturb;bool wasMinimized=runtimeState.MinimizedToTray;
  try{runtimeState.DoNotDisturb=value;runtimeState.MinimizedToTray=IsInTray;runtimeStateStore.Save(runtimeState);}
  catch(Exception e){runtimeState.DoNotDisturb=wasPaused;runtimeState.MinimizedToTray=wasMinimized;Report("前回状態を保存できないため、休憩モードを変更できません。"+e.Message);return;}
  IsPaused=value;controller.SetPaused(value);osNotifications.SetPaused(value);
  showSample.IsEnabled=!value;preview.IsEnabled=!value&&controller.Settings.SoundKind!=SoundKind.None;
  trayPause.Enabled=!value;trayResume.Enabled=value;
  RefreshHistoryControls();
  Report(value?"カード表示を一時停止しました。Windows通知の受信は継続します。":"カード表示を再開しました。");
 }
 internal void RecordNotification(SystemNotification notification){
  notificationHistory.Add(notification);historyStatus.Text=UiLocalization.Translate("この起動中の通知："+notificationHistory.Items.Count+" / "+NotificationHistory.Capacity+"件",CurrentLanguage);
  RefreshHistoryControls();
 }
 void CardClicked(NotificationWindow clickedCard,SystemNotification notification){
  if(closed)return;
  controller.DismissCard(clickedCard);
  if(notification!=null)ActivateNotification(notification);
 }
 void OpenSelectedNotification(){
  var notification=historyList.SelectedItem as SystemNotification;if(notification==null)return;
  ActivateNotification(notification);
 }
 async void ActivateNotification(SystemNotification notification){
  if(notification==null)return;
  ChromeNotificationActivator.PrepareForegroundActivation();
  bool opened=false;
  try{opened=await Task.Run(delegate{return notificationActivator.Activate(notification);});}catch{}
  if(closed)return;
  if(opened)Report("通知元アプリを開きました。");
  else Report("通知を開くためのアプリが見つかりませんでした。");
 }
 void ReplaySelectedNotification(){
  var notification=historyList.SelectedItem as SystemNotification;if(notification==null||IsPaused)return;
  controller.ShowNotification(notification,false);
 }
 void ShowTestNotification(){
  controller.ShowPreviewNotification(new SystemNotification {
   App="Glass Notification テスト",
   Title="表示・文字の確認",
   Body="この通知で文字サイズ・配色・表示時間と、設定した通知音を確認できます。",
   AppIcon=Icon as BitmapSource,
   Created=DateTimeOffset.Now
  },true);
 }
 void RefreshHistoryControls(){
  if(replayHistory==null)return;
  bool hasSelection=historyList.SelectedItem is SystemNotification;
  if(openHistory!=null)openHistory.IsEnabled=hasSelection;
  replayHistory.IsEnabled=!IsPaused&&hasSelection;
  clearHistory.IsEnabled=notificationHistory.Items.Count>0;
 }
 static DataTemplate HistoryTemplate(){
  var template=new DataTemplate(typeof(SystemNotification));var outer=new FrameworkElementFactory(typeof(StackPanel));outer.SetValue(StackPanel.MarginProperty,new Thickness(3));
  var app=new FrameworkElementFactory(typeof(TextBlock));app.SetBinding(TextBlock.TextProperty,new Binding("App"));app.SetValue(TextBlock.FontSizeProperty,11.0);app.SetValue(TextBlock.ForegroundProperty,Brush("#748194"));outer.AppendChild(app);
  var title=new FrameworkElementFactory(typeof(TextBlock));title.SetBinding(TextBlock.TextProperty,new Binding("Title"));title.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);title.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);outer.AppendChild(title);
  var body=new FrameworkElementFactory(typeof(TextBlock));body.SetBinding(TextBlock.TextProperty,new Binding("Body"));body.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);body.SetValue(TextBlock.MaxHeightProperty,34.0);body.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);outer.AppendChild(body);
  var time=new FrameworkElementFactory(typeof(TextBlock));var timeBinding=new Binding("Created") {StringFormat="{}{0:HH:mm:ss}"};time.SetBinding(TextBlock.TextProperty,timeBinding);time.SetValue(TextBlock.FontSizeProperty,10.0);time.SetValue(TextBlock.ForegroundProperty,Brush("#8994A3"));time.SetValue(TextBlock.HorizontalAlignmentProperty,HorizontalAlignment.Right);outer.AppendChild(time);
  template.VisualTree=outer;return template;
 }
 void SetStartWithWindows(bool enabled){
  if(syncing||closed)return;
  try{
   startupRegistration.SetEnabled(enabled);
   Change(delegate(AppSettings s){s.StartWithWindows=enabled;});
   Report(enabled?"Windowsへのサインイン時に起動します。":"Windows起動時の自動起動を解除しました。");
  }catch(Exception e){Sync();Report("Windows起動設定を変更できませんでした。"+e.Message);}
 }
 static SolidColorBrush Brush(string value){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));}
 static BitmapFrame LoadApplicationIcon(){
  string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","Square44x44Logo.png");
  var icon=BitmapFrame.Create(new Uri(path,UriKind.Absolute),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);icon.Freeze();return icon;
 }
 Button Button(string label,string name){var b=new Button {Content=label,Padding=new Thickness(13,7,13,7),Background=Brushes.White,BorderBrush=Brush("#DDE3EB"),Foreground=Brush("#3D4F65"),Cursor=System.Windows.Input.Cursors.Hand};if(name!=null)RegisterName(name,b);return b;}
 void Section(string label){target.Children.Add(new TextBlock {Text=label,FontWeight=FontWeights.SemiBold,Foreground=Brush("#3B4D63"),Margin=new Thickness(0,21,0,10)});}
  StackPanel Group(string header,string name,Panel container){
   var content=new StackPanel {Margin=new Thickness(0,2,0,4)};
   var expander=new Expander {Header=header,Content=content,IsExpanded=true,Margin=new Thickness(0,10,0,0)};
   RegisterName(name,expander);container.Children.Add(expander);return content;
  }
 internal void ShowFromSecondLaunch(){
  if(closed)return;
  if(IsInTray||WindowState==WindowState.Minimized)RestoreFromTray();
  else Show();
  Topmost=true;Topmost=false;Activate();
 }
 Slider SliderRow(string label,double min,double max,double step,string name,out TextBlock value) {
  var row=new Grid {Margin=new Thickness(0,5,0,5)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(110)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(44)});
  row.Children.Add(new TextBlock {Text=label,VerticalAlignment=VerticalAlignment.Center,Foreground=Brush("#637286")});
  var slider=new Slider {Minimum=min,Maximum=max,TickFrequency=step,SmallChange=step,LargeChange=step,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};
  AutomationProperties.SetName(slider,label);RegisterName(name,slider);Grid.SetColumn(slider,1);row.Children.Add(slider);
  value=new TextBlock {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,Foreground=Brush("#3B4D63")};Grid.SetColumn(value,2);row.Children.Add(value);target.Children.Add(row);return slider;
 }
 ComboBox ComboRow(string label,string name) {
  var row=new Grid {Margin=new Thickness(0,4,0,4)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(110)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(new TextBlock {Text=label,VerticalAlignment=VerticalAlignment.Center,Foreground=Brush("#637286")} );
  var combo=new ComboBox {Padding=new Thickness(8,5,8,5),MinHeight=30};AutomationProperties.SetName(combo,label);RegisterName(name,combo);Grid.SetColumn(combo,1);row.Children.Add(combo);target.Children.Add(row);return combo;
 }
 ComboBox ColorRow(string label,string name,bool isCard){
  var combo=ComboRow(label,name);combo.SelectedValuePath="Value";
  foreach(var choice in NotificationColors.Choices())combo.Items.Add(choice);
  var template=new DataTemplate(typeof(NotificationColorChoice));
  var stack=new FrameworkElementFactory(typeof(StackPanel));stack.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
  var swatch=new FrameworkElementFactory(typeof(Border));swatch.SetValue(FrameworkElement.WidthProperty,14.0);swatch.SetValue(FrameworkElement.HeightProperty,14.0);swatch.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,8,0));swatch.SetValue(Border.BorderThicknessProperty,new Thickness(1));swatch.SetValue(Border.BorderBrushProperty,Brush("#A6AFBC"));swatch.SetBinding(Border.BackgroundProperty,new Binding("Preview"));stack.AppendChild(swatch);
  var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding("Label"));stack.AppendChild(text);template.VisualTree=stack;combo.ItemTemplate=template;
  var row=(Grid)combo.Parent;row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
  var choose=Button("その他…",name+"Custom");choose.Padding=new Thickness(8,5,8,5);choose.Margin=new Thickness(6,0,0,0);Grid.SetColumn(choose,2);row.Children.Add(choose);
  choose.Click+=delegate {ChooseColor(isCard);};return combo;
 }
 void SelectColor(ComboBox combo,string value){
  bool found=false;foreach(NotificationColorChoice choice in combo.Items)if(choice.Value==value){found=true;break;}
  if(!found){for(int i=combo.Items.Count-1;i>=0;i--)if(((NotificationColorChoice)combo.Items[i]).Label=="選んだ色")combo.Items.RemoveAt(i);combo.Items.Add(new NotificationColorChoice("選んだ色",value));}
  combo.SelectedValue=value;
 }
 sealed class DialogOwner : System.Windows.Forms.IWin32Window {public IntPtr Handle {get;private set;}public DialogOwner(IntPtr handle){Handle=handle;}}
 void ChooseColor(bool isCard){
  var settings=controller.Settings;var color=NotificationColors.Color(isCard?settings.CardColor:settings.TextColor,isCard?Color.FromRgb(245,247,250):Color.FromRgb(31,42,56));
  using(var dialog=new System.Windows.Forms.ColorDialog {FullOpen=true,SolidColorOnly=true,Color=System.Drawing.Color.FromArgb(color.R,color.G,color.B)}){
   if(dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle))!=System.Windows.Forms.DialogResult.OK)return;
   string value="#"+dialog.Color.R.ToString("X2")+dialog.Color.G.ToString("X2")+dialog.Color.B.ToString("X2");
   Change(delegate(AppSettings s){if(isCard)s.CardColor=value;else s.TextColor=value;});
  }
 }
 AppSettings ApplySettings(AppSettings s) {
  if(monitors==null||monitors.Length==0)monitors=NativeWindow.GetMonitors();if(monitors.Length==0)throw new InvalidOperationException("表示できる画面がありません。");
  var selected=monitors.FirstOrDefault(delegate(MonitorInfo m){return m.Id==s.MonitorId;});
  if(selected==null) {selected=monitors[0];if(!string.IsNullOrEmpty(s.MonitorId))Report("表示先が見つからないため、メイン画面に切り替えました。");s.MonitorId=selected.Id;}
  AppSettings resolved=null;
  var targetCards=cards??new[]{card};
  for(int i=0;i<targetCards.Length;i++){
   var result=targetCards[i].Apply(s.Copy(),selected);
   if(i==0)resolved=result;
  }
  return resolved;
 }
 DispatcherTimer persistTimer;AppSettings pendingSettings;void Persist(AppSettings s){pendingSettings=s.Copy();if(persistTimer==null){persistTimer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(150)};persistTimer.Tick+=delegate {FlushPersist();};}persistTimer.Stop();persistTimer.Start();}void FlushPersist(){if(persistTimer!=null)persistTimer.Stop();if(pendingSettings==null)return;var toSave=pendingSettings;pendingSettings=null;string warning;if(!SettingsStore.Save(settingsPath,toSave,out warning))Report(warning);}
 void Report(string message){status.Text=UiLocalization.Translate(message,CurrentLanguage);}
 string CurrentLanguage {get{return controller==null?UiLocalization.Language:controller.Settings.Language;}}
 void Change(Action<AppSettings> change){
  if(syncing||closed)return;
  var previous=controller.Settings;var s=previous.Copy();change(s);controller.UpdateSettings(s);
  if(!string.Equals(previous.Language,controller.Settings.Language,StringComparison.Ordinal))UiLocalization.Language=controller.Settings.Language;
  Sync();
  if(!string.Equals(previous.Language,controller.Settings.Language,StringComparison.Ordinal))ApplyLanguage();
  if(!string.Equals(previous.ThemeMode,controller.Settings.ThemeMode,StringComparison.Ordinal))ApplyTheme();
 }
 void ApplyLanguage(){
  UiLocalization.Language=CurrentLanguage;
  syncing=true;try{UiLocalization.Apply(this,CurrentLanguage);LocalizeColorChoices(cardColor);LocalizeColorChoices(textColor);}finally{syncing=false;}
  UpdateTrayLanguage();osNotifications.ApplyLanguage();
 }
 void LocalizeColorChoices(ComboBox combo){
  string selected=combo.SelectedValue as string;combo.Items.Clear();
  bool selectedIncluded=false;
  foreach(var choice in NotificationColors.Choices()){
   combo.Items.Add(new NotificationColorChoice(UiLocalization.Translate(choice.Label,CurrentLanguage),choice.Value));
   if(choice.Value==selected)selectedIncluded=true;
  }
  if(!selectedIncluded&&!string.IsNullOrEmpty(selected))combo.Items.Add(new NotificationColorChoice(UiLocalization.Translate("選んだ色",CurrentLanguage),selected));
  combo.SelectedValue=selected;
 }
 void UpdateTrayLanguage(){
  trayOpen.Text=UiLocalization.Translate("開く",CurrentLanguage);trayPause.Text=UiLocalization.Translate("一時停止",CurrentLanguage);trayResume.Text=UiLocalization.Translate("再開",CurrentLanguage);trayExit.Text=UiLocalization.Translate("終了",CurrentLanguage);
 }
 void ApplyTheme(){
  ApplyTheme(this);
 }
 internal void ApplyTheme(DependencyObject root){
  var settings=controller.Settings;bool dark=settings.ThemeMode=="Dark"||settings.ThemeMode=="System"&&UiLocalization.IsSystemDark();
  UiLocalization.ApplyTheme(root,dark);
 }
 void UserPreferenceChanged(object sender,UserPreferenceChangedEventArgs args){
  if(closed||controller.Settings.ThemeMode!="System")return;
  Dispatcher.BeginInvoke(new Action(ApplyTheme));
 }
 void Sync() {
  syncing=true;
  try {
   var s=controller.Settings;ApplyUiScale(s.UiScalePercent);cardWidth.Value=s.Width;cardHeight.Minimum=Math.Max(88,Math.Ceiling(card.MeasuredMinimumHeight/4)*4);cardHeight.Value=s.Height;cardFont.Value=s.FontSize;margin.Value=s.Margin;
   backdropMode.SelectedIndex=s.Mode==BackdropMode.Acrylic?0:s.Mode==BackdropMode.Mica?1:2;backdropStrength.SelectedIndex=(int)s.BackdropStrength;backdropStrength.IsEnabled=s.Mode==BackdropMode.Acrylic;UpdateBackdropStatus();
   cardFontFamily.SelectedItem=s.FontFamilyName;roundedCorners.IsChecked=s.RoundedCorners;
   SelectColor(cardColor,s.CardColor);SelectColor(textColor,s.TextColor);
   displaySeconds.Value=s.DisplaySeconds;displaySecondsValue.Text=s.DisplaySeconds.ToString("0")+(s.Language=="English"?" sec":"秒");
   imageSize.Value=s.ImageSize;imageSizeValue.Text=s.ImageSize.ToString("0");
   imagePosition.SelectedIndex=s.ImagePosition=="Left"?1:0;
   widthValue.Text=s.Width.ToString("0");heightValue.Text=s.Height.ToString("0");fontValue.Text=s.FontSize.ToString("0");marginValue.Text=s.Margin.ToString("0");
   if(!ReferenceEquals(display.ItemsSource,monitors))display.ItemsSource=monitors;var selMon=monitors.FirstOrDefault(delegate(MonitorInfo m){return m.Id==s.MonitorId;});if(!ReferenceEquals(display.SelectedItem,selMon))display.SelectedItem=selMon;if(position.SelectedIndex!=(int)s.Corner)position.SelectedIndex=(int)s.Corner;if(sound.SelectedIndex!=(int)s.SoundKind)sound.SelectedIndex=(int)s.SoundKind;
   startWithWindows.IsChecked=s.StartWithWindows;startMinimizedToTray.IsChecked=s.StartMinimizedToTray;
   uiScale.SelectedIndex=(int)((s.UiScalePercent-100)/10);
   themeMode.SelectedIndex=s.ThemeMode=="Light"?1:s.ThemeMode=="Dark"?2:0;language.SelectedIndex=s.Language=="English"?1:0;
   highContrast.IsChecked=s.HighContrast;
   preview.IsEnabled=!IsPaused&&s.SoundKind!=SoundKind.None;showSample.IsEnabled=!IsPaused;fileName.Text=UiLocalization.Translate(s.SoundKind==SoundKind.File?(string.IsNullOrEmpty(s.SoundPath)?"WAV を選び直してください":Path.GetFileName(s.SoundPath)):s.SoundKind==SoundKind.Standard?"やわらかなチャイム":"音を鳴らしません",CurrentLanguage);fileName.ToolTip=s.SoundKind==SoundKind.File?s.SoundPath:null;
   RefreshHistoryControls();
  }finally{syncing=false;}
 }
 void ApplyUiScale(double percent){
  double scale=AppSettings.Clamp(percent,100,150,10);
  if(Math.Abs(scale-appliedUiScale)<.001)return;
  double factor=scale/100.0,oldWidth=Width,oldHeight=Height;
   Size available=monitors!=null&&monitors.Length>0?monitors[0].AvailableDip:new Size(860,720);
   Width=Math.Min(860*factor,Math.Max(480,available.Width-48));
   Height=Math.Min(720*factor,Math.Max(400,available.Height-48));
   MinWidth=Math.Min(780*factor,Width);MinHeight=Math.Min(520*factor,Height);
  panel.LayoutTransform=new ScaleTransform(factor,factor);
  if(IsVisible){Left-=(Width-oldWidth)/2;Top-=(Height-oldHeight)/2;}
  appliedUiScale=scale;
 }
 async void ChooseSound() {
  long request=++soundRequest;
  var dialog=new OpenFileDialog {Title=UiLocalization.Translate("通知音に使う WAV を選んでください",CurrentLanguage),Filter=CurrentLanguage=="English"?"WAV audio (*.wav)|*.wav":"WAV 音声 (*.wav)|*.wav",CheckFileExists=true};
  if(dialog.ShowDialog(this)==true) {
   Report("音声ファイルを確認しています…");string path=dialog.FileName;
   var valid=await Task.Run(delegate{return SoundService.Validate(path);});
   if(closed||request!=soundRequest)return;
   if(valid.IsValid)Change(delegate(AppSettings s){s.SoundPath=dialog.FileName;s.SoundKind=SoundKind.File;});else Report(valid.Error);
   if(valid.IsValid)Report("通知音を変更しました。「試聴」で確認できます。");
  }
  Sync();
 }
 async void CheckRestoredSound(){var s=controller.Settings;long request=soundRequest;if(s.SoundKind==SoundKind.File){var v=await Task.Run(delegate{return SoundService.Validate(s.SoundPath);});if(!closed&&request==soundRequest&&!v.IsValid)Report(v.Error);}}
 void DisplayChanged(object sender,EventArgs args){monitors=null;if(!closed)Dispatcher.BeginInvoke(new Action(RefreshEnvironment));}
 void BackdropChanged(object sender,EventArgs args){if(!closed)UpdateBackdropStatus();}
 void UpdateBackdropStatus(){backdropStatus.Text=UiLocalization.Translate(card==null?"通知の表示待ち":"通知：",CurrentLanguage)+ (card==null?"":card.BackdropStatus)+"\n"+UiLocalization.Translate("Mica は透過ぼかしではありません。非アクティブ時は不透明になる場合があります。",CurrentLanguage);}
 void RefreshEnvironment(){if(closed)return;controller.Initialize(controller.Settings.Copy());Sync();}
 public void SavePreview(string path) {
  UpdateLayout();var visual=(FrameworkElement)Content;var bmp=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var stream=File.Create(path))png.Save(stream);
 }
}
}
