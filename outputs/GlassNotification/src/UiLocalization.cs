using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Glass {
public static class UiLocalization {
 const int DwmUseImmersiveDarkModeBefore20H1=19;
 const int DwmUseImmersiveDarkMode=20;
 [DllImport("dwmapi.dll",PreserveSig=true)]
 static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int valueSize);
 static readonly Dictionary<string,string> English=new Dictionary<string,string>(StringComparer.Ordinal) {
  {"Glass Notification — 通知のプレビュー","Glass Notification — Preview"},
  {"設定モード","Settings mode"},{"動作モード","Active mode"},
  {"設定モード：通知カードを動かさず固定表示したまま、設定の結果を確認・変更できます。","Settings mode: Keeps the notification card stationary so you can check and adjust settings live."},
  {"動作モード：通常の通知受信とアニメーション表示を行います。","Active mode: Receives and animates notifications normally."},
  {"通知をまとめてテスト","Test notification"},
  {"現在のカード表示・文字設定と、選択中の通知音を確認します。音なし設定では音は鳴りません。","Test the current card appearance, text settings, and selected sound. No sound plays when sound is off."},
  {"通知カード","Notification card"},{"背景","Background"},{"Acrylic：すりガラス（推奨）","Acrylic: frosted glass (recommended)"},{"Mica：壁紙になじむ背景","Mica: blends with wallpaper"},{"オフ：不透明","Off: opaque"},
  {"すりガラスの強さ","Frosted-glass strength"},{"弱","Weak"},{"標準","Standard"},{"強","Strong"},{"強さは色の重なりを調整します。ぼかし半径はWindowsが管理します。","Strength adjusts color tint. Windows controls the blur radius."},
  {"カードの幅","Card width"},{"カードの高さ","Card height"},{"通知のフォント","Notification font"},{"文字サイズ","Font size"},{"通知カードの角を丸くする","Round notification card corners"},
  {"表示時間（秒）","Display duration (seconds)"},{"高コントラスト表示","High contrast"},{"カードの色","Card color"},{"文字の色","Text color"},
  {"文字の縁取り色","Text outline color"},{"縁取りなし","No outline"},
  {"通知画像の大きさ","Image size"},{"拡大枠の画像の大きさを調整します。","Adjust the size of the notification image."},
  {"通知画像の位置","Image position"},{"右","Right"},{"左","Left"},
  {"本文画像、送信者アイコン、アプリアイコンの順で表示します。","Shows the content image, then the sender icon, then the app icon when available."},
  {"画像サイズの設定が不正なため、56に戻しました。","The image size setting is invalid. Restored the default size of 56."},
  {"表示位置","Position"},{"画面","Display"},{"位置","Corner"},{"右上","Top right"},{"左上","Top left"},{"右下","Bottom right"},{"左下","Bottom left"},{"端からの余白","Screen margin"},
  {"通知音","Notification sound"},{"音の種類","Sound"},{"音なし","Off"},{"標準の音","Default sound"},{"ファイルを選ぶ","Choose a file"},{"WAVを選択…","Choose WAV..."},{"試聴","Preview"},
  {"起動","Startup"},{"Windowsへのサインイン時に起動する","Start when I sign in to Windows"},{"通知領域に最小化して起動する","Start minimized to the notification area"},
  {"画面表示","Application display"},{"アプリの拡大率","Application scale"},{"初期値に戻す","Reset to defaults"},{"終了","Exit"},{"その他…","Custom..."},
  {"通知履歴（起動中のみ・最大50件）","Notification history (this session only, up to 50)"},{"Chromeで開く","Open in Chrome"},{"選択した通知を再表示","Show selected notification"},{"履歴を消去","Clear history"},
  {"通知をChromeで開きました。","Opened the notification in Chrome."},{"通知を開くためのChromeが見つかりませんでした。","Could not find Chrome to open the notification."},
  {"通知元フィルター","Notification source filters"},{"Chromeの通知との連動","Chrome notification integration"},
  {"Chromeに登録して連動版を開く","Register with Chrome and open linked version"},{"Chrome通知の連動を開始","Start Chrome notification integration"},
  {"Chromeの通知バナーを止める","Suppress Chrome notification banners"},{"Chromeの通知音を止める","Suppress Chrome notification sounds"},
  {"Chromeの通知設定を元に戻す","Restore Chrome notification settings"},{"Chromeから確認用の通知を送る","Send a test notification from Chrome"},
  {"新しい通知元は許可されます。受信開始後に一覧へ表示します。","New notification sources are allowed by default and appear here after receiving starts."},
  {"通知センターには通知を残します。バナー抑制は登録済み通知元が対象です。新しい通知元の最初の通知や、通知アクセス未許可時は通常表示されます。","Notifications remain in Notification Center. Banner suppression applies to registered sources; the first notification from a new source and notifications before access is granted appear normally."},
  {"連動は停止中です。初回はChromeの通知アクセス許可が必要です。","Integration is stopped. Chrome notification access is required the first time."},
  {"Chrome通知の連動を停止しました。","Chrome notification integration stopped."},{"Chromeの通知設定を元に戻しました。","Chrome notification settings restored."},
  {"Chrome通知の連動を停止","Stop Chrome notification integration"},
  {"Chromeへ確認用通知を送信しました。カードが実際に届くことを確認してください。","Test notification sent to Chrome. Check that the card arrives."},
  {"前回残ったChrome通知設定を元に戻しました。連動は停止中です。","Restored Chrome notification settings left over from the previous session. Integration is stopped."},
  {"Chromeの許可画面で、通知アクセスを許可してください。","Allow notification access in the Chrome permission prompt."},
  {"通知へのアクセスを確認しています。","Checking notification access."},
  {"Chrome通知の受信を開始しました。既存の通知は再表示しません。","Receiving Chrome notifications. Existing notifications will not be shown again."},
  {"Chromeから届いた通知をカードに表示しました。","Displayed a Chrome notification card."},
  {"登録しました。連動版を開きます。","Registered. Opening the linked version."},
  {"Chromeの通知設定を元に戻すには、通知アクセスの許可を手動で解除してください。","To fully restore Chrome notification settings, revoke notification access manually."},
  {"許可","Allow"},{"ブロック","Block"},{"許可を保存しました： ","Allowed and saved: "},{"ブロックを保存しました： ","Blocked and saved: "},
  {"受信を開始すると、見つかった通知元がここに表示されます。","Discovered notification sources will appear here after receiving starts."},
  {"通知元ごとに表示を許可またはブロックできます。","Allow or block cards for each notification source."},
  {"通知を表示","Show notifications"},{"WAV を選び直してください","Choose a WAV file again"},{"やわらかなチャイム","Soft chime"},{"音を鳴らしません","No sound"},
  {"PCM WAV（最大5秒・8/16bit・8〜48kHz）","PCM WAV (up to 5 seconds, 8/16-bit, 8–48 kHz)"},
  {"システムに合わせる","Follow system"},{"ライト","Light"},{"ダーク","Dark"},{"日本語","Japanese"},{"English","English"},
  {"外観","Appearance"},{"テーマ","Theme"},{"言語","Language"},
  {"基本設定","Basic settings"},{"外観の詳細（サイズ・フォント・すりガラス）","Appearance details (size, font, frosted glass)"},
  {"Chrome連携と通知元フィルター","Chrome integration and source filters"},
  {"通知元アプリで開く","Open in source app"},{"通知元アプリを開きました。","Opened the notification in its source app."},{"通知を開くためのアプリが見つかりませんでした。","Could not find the app to open the notification."},
  {"アプリ・システム通知の連動とフィルター","App and system notifications and filters"},{"Windows・アプリ通知の連動設定","Windows and app notification integration"},
  {"Windows通知の連動版を開く","Open linked Windows notification app"},{"通知の連動を開始","Start Chrome notification integration"},{"通知の連動を停止","Stop Chrome notification integration"},
  {"Windows標準の通知バナーを非表示にする","Suppress Windows notification banners"},{"Windows標準の通知音を鳴らさない","Suppress Windows notification sounds"},
  {"Windowsの通知設定を元に戻す","Restore Windows notification settings"},{"確認用のテスト通知を送る","Send a test notification"},
  {"連動は停止中です。初回はWindowsの「通知へのアクセス」許可が必要です。","Integration is stopped. Windows notification access is required the first time."},
  {"Windowsのアクションセンター（通知履歴）には通知を残します。通知バナーの抑制を設定すると、Glassカードとの二重表示を防ぎます。","Notifications remain in Action Center. Banner suppression prevents duplicate popups with Glass cards."},
  {"通知の連動を停止しました。","Notification integration stopped."},{"Windowsの通知設定を元に戻しました。","Windows notification settings restored."},
  {"通知設定を戻せませんでした","Could not restore notification settings"},{"確認用のテスト通知を送信しました。Glassカードが届くことを確認してください。","Test notification sent. Check that the Glass card arrives."},
  {"前回の一時設定を元に戻しました。連動は停止中です。","Restored temporary notification settings. Integration is stopped."},
  {"通知連動の処理中です。少し待ってから再試行してください。","Notification integration is processing. Please wait briefly and try again."},
  {"通知の連動を開始できませんでした","Could not start notification integration"},
  {"Windowsの「設定 > システム > 通知」で通知へのアクセスを許可してください。","Allow notification access in Windows Settings > System > Notifications."},
  {"アプリ・システム通知の受信を開始しました。","Receiving app and system notifications."},
  {"届いた通知をカードに表示しました。","Displayed a notification card."},
  {"標準通知の抑制設定を反映しました。","Applied notification suppression settings."},
  {"標準通知の抑制設定を反映できませんでした","Could not apply notification suppression settings"},
  {"通知の連動を終了しました。","Notification integration ended."},
  {"通知設定を完全に元に戻すには、Windowsの通知アクセス許可を手動で解除してください。","To fully restore notification settings, revoke notification access in Windows manually."},{"システム・詳細設定（自動起動・言語など）","System and advanced (startup, language, etc.)"}
  ,{"開く","Open"},{"一時停止","Pause"},{"再開","Resume"},{"履歴を消去しました。","History cleared."},
  {"現在「一時停止（休憩モード）」中です。通知カードの表示と音が停止しています。","Do Not Disturb is on. Card display and sounds are paused."},
  {"休憩モード中です。通知の受信は継続し、カード表示と音を停止しています。","Do Not Disturb is on. Notifications are still received, but cards and sounds are paused."},
  {"カード表示を一時停止しました。Windows通知の受信は継続します。","Card display paused. Windows notifications continue to be received."},
  {"カード表示を再開しました。","Card display resumed."},
  {"Chrome通知の連動を一度だけ待ってから再試行してください。","Wait briefly for the current Chrome integration operation to finish, then try again."},
  {"Chromeの通知連動を開始できませんでした","Could not start Chrome notification integration"},
  {"Chrome設定を戻せませんでした","Could not restore Chrome settings"},
  {"確認用通知を送れませんでした","Could not send the test notification"},
  {"前回のChrome設定を戻せませんでした","Could not restore the previous Chrome settings"},
  {"前回残ったChrome設定を戻しました。通知アクセスを確認しています。","Restored leftover Chrome settings. Checking notification access."},
  {"Chrome通知の連動を開始できませんでした。","Could not start Chrome notification integration."},
  {"通知アクセスが許可されていないため停止しました。Chromeの「通知アクセス」設定を確認してください。","Stopped because notification access is not allowed. Check Chrome's notification access setting."},
  {"Chrome通知の抑制設定を反映しました。","Applied Chrome notification suppression settings."},
  {"Chrome通知の抑制設定を反映できませんでした","Could not apply Chrome notification suppression settings"},
  {"Chrome通知設定の停止処理に失敗しました","Could not restore Chrome notification settings while stopping"},
  {"Chromeに連動版を登録しています…","Registering the linked version with Chrome..."},
  {"Chrome連動の登録を完了できませんでした。","Could not complete Chrome integration registration."},
  {"Chrome連動版の登録に失敗しました。","Failed to register the Chrome-linked version."},
  {"通知元フィルターを保存できませんでした。","Could not save the notification source filter."},
  {"通知元フィルターを保存できませんでした","Could not save the notification source filter"},
  {"Chrome通知連動時に問題が発生しました。","A problem occurred while integrating Chrome notifications."},
  {"Windows起動登録を解除できなかったため、初期化しませんでした。","Settings were not reset because the Windows startup registration could not be removed."},
  {"Windows起動時の登録を適用できませんでした。","Could not apply the Windows startup registration."},
  {"Windowsへのサインイン時に起動します。","The app will start when I sign in to Windows."},
  {"Windows起動時の自動起動を解除しました。","The app will no longer start automatically with Windows."},
  {"Windows起動設定を変更できませんでした。","Could not change the Windows startup setting."},
  {"前回状態を保存できないため、休憩モードを変更できません。","Could not change Do Not Disturb because the previous state could not be saved."},
  {"前回状態を保存できないため、通知領域へ最小化できません。","Could not minimize to the notification area because the previous state could not be saved."},
  {"前回状態を保存できないため、ウィンドウを復元できません。","Could not restore the window because the previous state could not be saved."},
  {"表示できる画面がありません。","No display is available."},{"表示先が見つからないため、メイン画面に切り替えました。","The selected display was not found. Switched to the primary display."}
  ,{"白","White"},{"黒","Black"},{"青","Blue"},{"水色","Sky blue"},{"ピンク","Pink"},{"赤","Red"},{"緑","Green"},{"紫","Purple"},{"黄色","Yellow"},{"選んだ色","Custom color"},
  {"通知の表示待ち","Waiting for a notification"},{"通知：","Notification:"},{"Mica は透過ぼかしではありません。非アクティブ時は不透明になる場合があります。","Mica does not use transparent blur. The background may become opaque while inactive."},
  {"通知音に使う WAV を選んでください","Choose a WAV file for notification sound"},
  {"背景の設定が不明なため、OFF（不透明）で起動しました。","Unknown background setting. Started with the opaque background."},
  {"通知のフォントが見つからないため、既定のフォントに戻しました。","The selected notification font was not found. Restored the default font."},
  {"角丸の設定が不正なため、オンで起動しました。","Invalid rounded-corner setting. Started with rounded corners enabled."},
  {"通知の色の設定が不正なため、標準の色に戻しました。","Invalid notification color. Restored the default color."},
  {"表示時間の設定が不正なため、4秒に戻しました。","Invalid display duration. Restored the 4-second default."},
  {"アプリの拡大率が不正なため、100%に戻しました。","Invalid application scale. Restored 100%."},
  {"音声ファイルの設定が不正です。WAV を選び直してください。","Invalid audio file setting. Choose a WAV file again."},
  {"設定を読み込めなかったため、初期値で起動しました。","Could not load settings. Started with defaults."},
  {"設定を保存できません。この起動中は変更を使えます。","Could not save settings. Changes are active for this session."},
  {"音を読み込めません。再選択してください。","Could not load the sound. Select it again."},
  {"音を再生できません。別の WAV を選んでください。","Could not play the sound. Choose another WAV file."},
  {"5秒以内の PCM WAV を選んでください。","Choose a PCM WAV file no longer than 5 seconds."},
  {"音声ファイルが途中で切れています。","The audio file ended unexpectedly."},
  {"音声ファイルの長さが変わりました。","The audio file length changed while being read."},
  {"WAV ファイルではありません。","This is not a WAV file."},
  {"WAV ファイルの長さが不正です。","The WAV file length is invalid."},
  {"WAV ファイルが壊れています。","The WAV file is corrupted."},
  {"WAV 形式が不正です。","The WAV format is invalid."},
  {"WAV のパディングが不正です。","The WAV padding is invalid."},
  {"PCM WAV（8/16bit・モノラル/ステレオ・8〜48kHz）を選んでください。","Choose PCM WAV (8/16-bit, mono/stereo, 8–48 kHz)."},
  {"音声は5秒以内にしてください。","Audio must be no longer than 5 seconds."},
  {"Windowsの自動起動設定を開けませんでした。","Could not open Windows startup settings."},
  {"実行ファイルのパスが必要です。","The executable path is required."},
  {"Chromeの通知設定を完全に元に戻すには、通知アクセスの許可を手動で解除してください。","To restore all Chrome notification settings, revoke notification access manually."}
 };
 static readonly Dictionary<string,string> Japanese=BuildJapaneseDictionary();
 static readonly KeyValuePair<string,string>[] EnglishPrefixes= English.Where(item=>item.Key.EndsWith("。",StringComparison.Ordinal)||item.Key.EndsWith("…",StringComparison.Ordinal)||item.Key.EndsWith("：",StringComparison.Ordinal)||item.Key.EndsWith("： ",StringComparison.Ordinal)).OrderByDescending(item=>item.Key.Length).ToArray();
 static readonly KeyValuePair<string,string>[] JapanesePrefixes=Japanese.Where(item=>item.Key.EndsWith(".",StringComparison.Ordinal)||item.Key.EndsWith("...",StringComparison.Ordinal)||item.Key.EndsWith(": ",StringComparison.Ordinal)).OrderByDescending(item=>item.Key.Length).ToArray();
 static Dictionary<string,string> BuildJapaneseDictionary(){
  var dict=new Dictionary<string,string>(StringComparer.Ordinal);
  foreach(var pair in English)dict[pair.Value]=pair.Key;
  return dict;
 }
 public static string Language {get;set;}
 public static string Translate(string value) {return Translate(value,Language);}
 public static string Translate(string value,string language) {
  if(string.IsNullOrEmpty(value))return value;
  string translated;
  if(string.Equals(language,"English",StringComparison.OrdinalIgnoreCase)) {
   if(English.TryGetValue(value,out translated))return translated;
   if(Japanese.ContainsKey(value))return value;
   for(int i=0;i<EnglishPrefixes.Length;i++){
    var pair=EnglishPrefixes[i];
    if(value.StartsWith(pair.Key,StringComparison.Ordinal))
     return pair.Value+value.Substring(pair.Key.Length);
   }
   const string allowed=" — 通知を表示";
   if(value.EndsWith(allowed,StringComparison.Ordinal))return value.Substring(0,value.Length-allowed.Length)+" — Show notifications";
   const string history="この起動中の通知：";
   if(value.StartsWith(history,StringComparison.Ordinal))return "Notifications this session: "+value.Substring(history.Length);
   return value;
  }
  if(Japanese.TryGetValue(value,out translated))return translated;
  if(English.ContainsKey(value))return value;
  for(int i=0;i<JapanesePrefixes.Length;i++){
   var pair=JapanesePrefixes[i];
   if(value.StartsWith(pair.Key,StringComparison.Ordinal))
    return pair.Value+value.Substring(pair.Key.Length);
  }
  if(value.EndsWith(" — Show notifications",StringComparison.Ordinal))return value.Substring(0,value.Length-" — Show notifications".Length)+" — 通知を表示";
  const string englishHistory="Notifications this session: ";
  if(value.StartsWith(englishHistory,StringComparison.Ordinal))return "この起動中の通知："+value.Substring(englishHistory.Length);
  return value;
 }
 public static void Apply(DependencyObject root) {Apply(root,Language);}
 public static void Apply(DependencyObject root,string language) {Apply(root,language,new HashSet<DependencyObject>(),false);}
 static void Apply(DependencyObject current,string language,HashSet<DependencyObject> visited,bool insideHistory) {
  if(current==null||!visited.Add(current))return;
  bool historyContent=insideHistory||current is ListBox;
  var window=current as Window;
  if(window!=null&&!historyContent)window.Title=Translate(window.Title,language);
  var text=current as TextBlock;
  if(text!=null&&!historyContent)text.Text=Translate(text.Text,language);
  var combo=current as ComboBox;
  if(combo!=null) {
   int selected=combo.SelectedIndex;
   var source=new[]{"BackdropMode","BackdropStrength","CardPosition","CardSound","UiScale","ThemeMode","ImagePosition"}.Contains(combo.Name)?combo.ItemsSource as IEnumerable<string>:null;
   if(source!=null)combo.ItemsSource=source.Select(item=>Translate(item,language)).ToArray();
   else if(combo.ItemsSource==null)for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string)combo.Items[i]=Translate((string)combo.Items[i],language);
   if(selected>=0)combo.SelectedIndex=selected;
  }
  var content=current as ContentControl;
  if(content!=null&&!historyContent&&content.Content is string)content.Content=Translate((string)content.Content,language);
  var header=current as HeaderedContentControl;
  if(header!=null&&!historyContent&&header.Header is string)header.Header=Translate((string)header.Header,language);
  var element=current as FrameworkElement;
  if(element!=null&&!historyContent&&element.ToolTip is string)element.ToolTip=Translate((string)element.ToolTip,language);
  foreach(object child in LogicalTreeHelper.GetChildren(current)) {
   var dependency=child as DependencyObject;
   if(dependency!=null)Apply(dependency,language,visited,historyContent);
  }
  var visual=current as Visual;
  if(visual!=null)for(int i=0;i<VisualTreeHelper.GetChildrenCount(visual);i++)Apply(VisualTreeHelper.GetChild(visual,i),language,visited,historyContent);
 }
 public static bool IsSystemDark() {
  using(RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
   if(key==null)return false;
   int preference;
   return int.TryParse(Convert.ToString(key.GetValue("AppsUseLightTheme",1)),out preference)&&preference==0;
  }
 }
 static SolidColorBrush FrozenBrush(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
 public static void ApplyTheme(Window window,bool dark) {
  ApplyTheme((DependencyObject)window,dark);
 }
 public static void ApplyTheme(DependencyObject root,bool dark) {
  Color background=dark?Color.FromRgb(25,28,33):Color.FromRgb(245,247,250);
  Color surface=dark?Color.FromRgb(39,43,50):Colors.White;
  Color foreground=dark?Color.FromRgb(230,233,238):Color.FromRgb(61,79,101);
  Color border=dark?Color.FromRgb(70,76,86):Color.FromRgb(221,227,235);
  Color selectionSurface=dark?Color.FromRgb(76,92,116):Color.FromRgb(214,229,246);
  Color selectionForeground=dark?Color.FromRgb(236,240,246):Color.FromRgb(32,44,64);
  Color scrollTrack=dark?Color.FromRgb(25,28,33):Color.FromRgb(238,241,245);
  Color scrollThumb=dark?Color.FromRgb(83,91,103):Color.FromRgb(170,180,194);
  var window=root as Window;if(window!=null)window.Background=FrozenBrush(background);
  if(window!=null){ApplyTitleBarTheme(window,dark);ApplySystemThemeResources(window,surface,foreground,border,selectionSurface,selectionForeground);ApplyControlStyles(window,dark,surface,foreground,border,selectionSurface,scrollTrack,scrollThumb);}
  ApplyThemeElement(root,new HashSet<DependencyObject>(),dark,surface,foreground,border,selectionSurface,selectionForeground,scrollTrack,scrollThumb,FrozenBrush(surface),FrozenBrush(foreground),FrozenBrush(border),FrozenBrush(selectionSurface),FrozenBrush(scrollTrack),FrozenBrush(scrollThumb),FrozenBrush(background),FrozenBrush(Color.FromRgb(84,118,159)));
 }
 static void ApplyTitleBarTheme(Window window,bool dark) {
  IntPtr handle=new WindowInteropHelper(window).Handle;
  if(handle==IntPtr.Zero)return;
  int value=dark?1:0;
  int result=DwmSetWindowAttribute(handle,DwmUseImmersiveDarkMode,ref value,sizeof(int));
  if(result<0)DwmSetWindowAttribute(handle,DwmUseImmersiveDarkModeBefore20H1,ref value,sizeof(int));
 }
 static void ApplyThemeElement(DependencyObject current,HashSet<DependencyObject> visited,bool dark,Color surface,Color foreground,Color border,Color selectionSurface,Color selectionForeground,Color scrollTrack,Color scrollThumb,SolidColorBrush surfaceBrush,SolidColorBrush foregroundBrush,SolidColorBrush borderBrush,SolidColorBrush selectionSurfaceBrush,SolidColorBrush scrollTrackBrush,SolidColorBrush scrollThumbBrush,SolidColorBrush backgroundBrush,SolidColorBrush accentBrush) {
  if(current==null||!visited.Add(current))return;
  var borderElement=current as Border;
  if(borderElement!=null) {
    if(!IsColorSwatch(borderElement))borderElement.Background=ThemeBackground(borderElement.Background,surface,surfaceBrush);
   borderElement.BorderBrush=borderBrush;
  }
  var panel=current as Panel;
  if(panel!=null)panel.Background=ThemeBackground(panel.Background,surface,surfaceBrush);
  var control=current as Control;
  if(control!=null) {
   control.Foreground=foregroundBrush;
   if(control is ButtonBase||control is ComboBox||control is ListBox||control is TextBox||control is Slider)control.Background=surfaceBrush;
   if(control is ButtonBase||control is ComboBox||control is ListBox||control is TextBox||control is Slider)control.BorderBrush=borderBrush;
  }
  var combo=current as ComboBox;
  if(combo!=null){
   combo.ItemContainerStyle=ItemStyle(typeof(ComboBoxItem),dark,surface,foreground,border,selectionSurface,selectionForeground);
   ApplyControlThemeResources(combo.Resources,surface,foreground,border,selectionSurface,selectionForeground);
  }
  var list=current as ListBox;
  if(list!=null){
   list.ItemContainerStyle=ItemStyle(typeof(ListBoxItem),dark,surface,foreground,border,selectionSurface,selectionForeground);
   ApplyControlThemeResources(list.Resources,surface,foreground,border,selectionSurface,selectionForeground);
  }
  var slider=current as Slider;
  if(slider!=null){slider.Background=scrollTrackBrush;slider.Foreground=selectionSurfaceBrush;}
  var scroll=current as ScrollViewer;if(scroll!=null)scroll.Background=backgroundBrush;
  var scrollBar=current as ScrollBar;
  if(scrollBar!=null){scrollBar.Background=scrollTrackBrush;scrollBar.Foreground=scrollThumbBrush;scrollBar.BorderBrush=borderBrush;}
  var thumb=current as Thumb;
  if(thumb!=null){thumb.Background=scrollThumbBrush;thumb.BorderBrush=borderBrush;}
  var text=current as TextBlock;if(text!=null)text.Foreground=foregroundBrush;
  foreach(object child in LogicalTreeHelper.GetChildren(current)) {
   var dependency=child as DependencyObject;
   if(dependency!=null)ApplyThemeElement(dependency,visited,dark,surface,foreground,border,selectionSurface,selectionForeground,scrollTrack,scrollThumb,surfaceBrush,foregroundBrush,borderBrush,selectionSurfaceBrush,scrollTrackBrush,scrollThumbBrush,backgroundBrush,accentBrush);
  }
  var visual=current as Visual;
  if(visual!=null)for(int i=0;i<VisualTreeHelper.GetChildrenCount(visual);i++)ApplyThemeElement(VisualTreeHelper.GetChild(visual,i),visited,dark,surface,foreground,border,selectionSurface,selectionForeground,scrollTrack,scrollThumb,surfaceBrush,foregroundBrush,borderBrush,selectionSurfaceBrush,scrollTrackBrush,scrollThumbBrush,backgroundBrush,accentBrush);
 }
  static void ApplySystemThemeResources(Window window,Color surface,Color foreground,Color border,Color selectionSurface,Color selectionForeground){
   window.Resources[SystemColors.WindowBrushKey]=FrozenBrush(surface);
   window.Resources[SystemColors.ControlBrushKey]=FrozenBrush(surface);
   window.Resources[SystemColors.ControlLightBrushKey]=FrozenBrush(surface);
   window.Resources[SystemColors.ControlDarkBrushKey]=FrozenBrush(border);
   window.Resources[SystemColors.WindowTextBrushKey]=FrozenBrush(foreground);
   window.Resources[SystemColors.ControlTextBrushKey]=FrozenBrush(foreground);
   window.Resources[SystemColors.GrayTextBrushKey]=FrozenBrush(Color.FromRgb(168,176,188));
   ApplyControlThemeResources(window.Resources,surface,foreground,border,selectionSurface,selectionForeground);
   window.Resources[SystemColors.WindowColorKey]=surface;
   window.Resources[SystemColors.ControlColorKey]=surface;
  }
  static Color backgroundColor(bool dark){return dark?Color.FromRgb(25,28,33):Color.FromRgb(245,247,250);}
 static bool IsColorSwatch(Border border){return Math.Abs(border.Width-14)<.1&&Math.Abs(border.Height-14)<.1;}
 static Brush ThemeBackground(Brush existing,Color surface,SolidColorBrush surfaceBrush) {
  var solid=existing as SolidColorBrush;
  if(solid==null)return existing;
  Color color=solid.Color;
  bool pale=color.A>0&&color.R>=225&&color.G>=225&&color.B>=225;
  return pale?(color.A<255?FrozenBrush(Color.FromArgb(color.A,surface.R,surface.G,surface.B)):surfaceBrush):existing;
 }
 static Style ItemStyle(Type itemType,bool dark,Color surface,Color foreground,Color border,Color selectionSurface,Color selectionForeground) {
  var style=new Style(itemType);
  if(dark)style.Setters.Add(new Setter(Control.TemplateProperty,ParseTemplate(itemType.Name,"<Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border>")));
  style.Setters.Add(new Setter(Control.BackgroundProperty,FrozenBrush(surface)));
  style.Setters.Add(new Setter(Control.ForegroundProperty,FrozenBrush(foreground)));
  style.Setters.Add(new Setter(Control.BorderBrushProperty,FrozenBrush(border)));
  var hover=new Trigger {Property=Control.IsMouseOverProperty,Value=true};
  hover.Setters.Add(new Setter(Control.BackgroundProperty,FrozenBrush(selectionSurface)));
  hover.Setters.Add(new Setter(Control.ForegroundProperty,FrozenBrush(selectionForeground)));
  style.Triggers.Add(hover);
  var selected=new Trigger {Property=ListBoxItem.IsSelectedProperty,Value=true};
  selected.Setters.Add(new Setter(Control.BackgroundProperty,FrozenBrush(selectionSurface)));
  selected.Setters.Add(new Setter(Control.ForegroundProperty,FrozenBrush(selectionForeground)));
  style.Triggers.Add(selected);
  return style;
 }
 static void ApplySelectionResources(ResourceDictionary resources,Color selectionSurface,Color selectionForeground){
  resources[SystemColors.HighlightBrushKey]=FrozenBrush(selectionSurface);
  resources[SystemColors.HighlightTextBrushKey]=FrozenBrush(selectionForeground);
  resources[SystemColors.InactiveSelectionHighlightBrushKey]=FrozenBrush(selectionSurface);
  resources[SystemColors.InactiveSelectionHighlightTextBrushKey]=FrozenBrush(selectionForeground);
 }
 static void ApplyControlThemeResources(ResourceDictionary resources,Color surface,Color foreground,Color border,Color selectionSurface,Color selectionForeground){
  resources[SystemColors.WindowBrushKey]=FrozenBrush(surface);
  resources[SystemColors.ControlBrushKey]=FrozenBrush(surface);
  resources[SystemColors.ControlLightBrushKey]=FrozenBrush(surface);
  resources[SystemColors.ControlDarkBrushKey]=FrozenBrush(border);
  resources[SystemColors.WindowTextBrushKey]=FrozenBrush(foreground);
  resources[SystemColors.ControlTextBrushKey]=FrozenBrush(foreground);
  ApplySelectionResources(resources,selectionSurface,selectionForeground);
 }
 const string XamlNamespaces="xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";
 static string TemplateOpen(string targetType){return "<ControlTemplate "+XamlNamespaces+" TargetType='"+targetType+"'>";}
 static ControlTemplate ParseTemplate(string targetType,string content){
  return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(TemplateOpen(targetType)+content+"</ControlTemplate>");
 }
 static string Hex(Color color){return "#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2");}
 static Style TemplateStyle(string targetType,string template){
  return (Style)System.Windows.Markup.XamlReader.Parse("<Style "+XamlNamespaces+" TargetType='"+targetType+"'><Setter Property='Template'><Setter.Value>"+template+"</Setter.Value></Setter></Style>");
 }
 // Standard WPF templates hard-code their colors, so dark mode supplies its own control templates.
 static void ApplyControlStyles(Window window,bool dark,Color surface,Color foreground,Color border,Color selection,Color track,Color thumb){
  var targets=new[]{typeof(Button),typeof(CheckBox),typeof(ComboBox),typeof(Slider),typeof(ScrollBar),typeof(Expander)};
  foreach(var target in targets)window.Resources.Remove(target);
  Func<string,string> p=delegate(string xaml){
   return xaml.Replace("%SURFACE%",Hex(surface)).Replace("%FG%",Hex(foreground)).Replace("%BORDER%",Hex(border)).Replace("%SEL%",Hex(selection)).Replace("%TRACK%",Hex(track)).Replace("%THUMB%",Hex(thumb))
    .Replace("%ACCENT%","#5476A0").Replace("%BOX%","#8A94A3").Replace("%SLIDERTHUMB%","#B0BED2");
  };
  string expander=TemplateOpen("Expander")+p("<Border Background='%SURFACE%' BorderBrush='%BORDER%' BorderThickness='1' CornerRadius='6' SnapsToDevicePixels='True'><DockPanel><ToggleButton x:Name='Head' DockPanel.Dock='Top' Focusable='True' Cursor='Hand' Content='{TemplateBinding Header}' Foreground='{TemplateBinding Foreground}' IsChecked='{Binding IsExpanded,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='Hd' Background='Transparent' Padding='12,9' CornerRadius='6'><DockPanel><Path x:Name='Arrow' DockPanel.Dock='Left' Data='M0,0 L4,4 L0,8' Stroke='%FG%' StrokeThickness='1.5' VerticalAlignment='Center' Margin='0,0,10,0' RenderTransformOrigin='0.5,0.5'><Path.RenderTransform><RotateTransform Angle='0'/></Path.RenderTransform></Path><ContentPresenter TextElement.FontWeight='SemiBold' VerticalAlignment='Center'/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='RenderTransform'><Setter.Value><RotateTransform Angle='90'/></Setter.Value></Setter></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hd' Property='Background' Value='%SEL%'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter x:Name='Body' Visibility='Collapsed' Margin='12,2,12,8'/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='Body' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  window.Resources[typeof(Expander)]=TemplateStyle("Expander",expander);
  if(!dark)return;
  string button=TemplateOpen("Button")+p("<Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='BorderBrush' Value='%ACCENT%'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Bd' Property='BorderBrush' Value='%ACCENT%'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Bd' Property='Opacity' Value='0.75'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  string check=TemplateOpen("CheckBox")+p("<Grid Background='Transparent' SnapsToDevicePixels='True'><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Border x:Name='Box' Width='14' Height='14' Background='%SURFACE%' BorderBrush='%BOX%' BorderThickness='1' VerticalAlignment='Center'><Path x:Name='Mark' Data='M2,6 L5,9 L11,2' Stroke='%FG%' StrokeThickness='2' Visibility='Collapsed'/></Border><ContentPresenter Grid.Column='1' Margin='6,0,0,0' VerticalAlignment='Center' RecognizesAccessKey='False'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Mark' Property='Visibility' Value='Visible'/><Setter TargetName='Box' Property='Background' Value='%SEL%'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='%ACCENT%'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  string combo=TemplateOpen("ComboBox")+p("<Grid SnapsToDevicePixels='True'><ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='Bd' Background='%SURFACE%' BorderBrush='%BORDER%' BorderThickness='1'><Path Data='M0,0 L4,4 L8,0' Stroke='%FG%' StrokeThickness='1.5' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,9,0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='BorderBrush' Value='%ACCENT%'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter IsHitTestVisible='False' Margin='{TemplateBinding Padding}' HorizontalAlignment='Left' VerticalAlignment='Center' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'/><Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'><Border MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='{TemplateBinding MaxDropDownHeight}' Background='%SURFACE%' BorderBrush='%BORDER%' BorderThickness='1' SnapsToDevicePixels='True'><ScrollViewer CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  string slider=TemplateOpen("Slider")+p("<Grid Height='22' Background='Transparent' SnapsToDevicePixels='True'><Border Height='4' CornerRadius='2' Background='%TRACK%' BorderBrush='%BORDER%' BorderThickness='1' VerticalAlignment='Center'/><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Grid Background='Transparent'><Border Height='4' CornerRadius='2' Background='%ACCENT%' VerticalAlignment='Center'/></Grid></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Grid Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb Focusable='False'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Width='14' Height='14' Fill='%SLIDERTHUMB%' Stroke='%BORDER%'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  string scroll=TemplateOpen("ScrollBar")+p("<Grid Background='%TRACK%' SnapsToDevicePixels='True'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border x:Name='T' Margin='2' CornerRadius='3' Background='%THUMB%'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='Background' Value='%BOX%'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid><ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
  window.Resources[typeof(Button)]=TemplateStyle("Button",button);
  window.Resources[typeof(CheckBox)]=TemplateStyle("CheckBox",check);
  window.Resources[typeof(ComboBox)]=TemplateStyle("ComboBox",combo);
  window.Resources[typeof(Slider)]=TemplateStyle("Slider",slider);
  window.Resources[typeof(ScrollBar)]=TemplateStyle("ScrollBar",scroll);
 }
}
}
