# GlassNotification

Windowsの通知を、すりガラス風のカードで表示するWindows 11向けアプリです。Chromeなどから届くWindows通知を受け取り、カードの色・画像・表示位置・通知音を自分好みに設定できます。

## ダウンロードとインストール

配布版は **1.7.0.12 / Windows 11 22H2以降 / x64** です。

- [GlassNotification.OSLink.msix](dist/GlassNotification.OSLink.msix) — アプリ本体と必要なランタイムを含むインストーラー
- [GlassNotification.OSLink.cer](dist/GlassNotification.OSLink.cer) — 初回インストール用の公開証明書
- [インストール手順](dist/インストール手順.md) — 証明書の登録、更新、アンインストール

GitHubで各ファイルを開いたら、右上のダウンロードボタン（Download raw file）から保存してください。MSIXとCERは同じフォルダーへ置いてください。MSIXを使う場合、EXEを別途ダウンロードする必要はありません。

## 主な機能

- Windows通知との連動、通知元ごとの表示設定、通知履歴
- Acrylic・Mica・不透明表示の切り替えと、Acrylicの弱・標準・強
- カードと文字の色、フォント、大きさ、表示時間、配置モニター、表示位置の変更
- 本文画像 → 送信者アイコン → アプリアイコンの順で画像を選択。画像の大きさと左右の位置を変更可能
- 通知は最大3枚同時に表示。マウスを乗せると画像の反対側に閉じるボタンを表示
- 通知カードをクリックして元の通知の操作を実行。閉じるボタンはカードだけを閉じる
- スライドアニメーション、通知音、通知領域への最小化、自動起動
- 設定画面のライト／ダークテーマ、日本語／English

操作方法と制限事項は[使い方](docs/使い方.md)をご覧ください。

## 開発・ビルド

ソースは [outputs/GlassNotification](outputs/GlassNotification) にあります。Visual StudioのC++開発ツール、Windows SDK、.NET Framework 4.8が必要です。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\outputs\GlassNotification\build.ps1 -Test All
```

依存SDKは公式NuGetから取得し、固定バージョンとSHA-256を確認します。ビルドはEXE・連動版EXE・署名付きMSIXを生成します。詳細は[開発・配布手順](docs/開発・配布.md)をご覧ください。

## ファイル構成

| 場所 | 内容 |
| --- | --- |
| `dist/` | 配布用MSIX・公開証明書・インストール手順 |
| `docs/` | 操作説明と開発・配布手順 |
| `outputs/GlassNotification/src/` | C# / WPFのソース |
| `outputs/GlassNotification/native/` | C++の描画・Windows通知連動とビルドスクリプト |
| `outputs/GlassNotification/identity/` | パッケージ定義と署名処理 |
| `outputs/GlassNotification/tests/` | 自動テスト・動作検証用コード |
| `outputs/GlassNotification/licenses/` | 同梱するWindows App SDKのライセンス |

個人設定・通知の記録・署名の秘密鍵・ビルドキャッシュは収録していません。第三者コンポーネントのライセンスは `licenses/` を参照してください。本アプリ本体の利用許諾は、このリポジトリではまだ定義していません。
