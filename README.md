# GlassNotification

Windowsの通知を、すりガラス風のカードで表示するWindows 11向けアプリです。Chromeなどから届くWindows通知を受け取り、カードの色・画像・表示位置・通知音を自分好みに設定できます。

## ダウンロードとインストール

配布版は **[1.7.0.20](https://github.com/sukesan666/GlassNotification/releases/tag/v1.7.0.20) / Windows 11 22H2以降 / x64** です。

- [GlassNotification.OSLink.msix](https://github.com/sukesan666/GlassNotification/releases/download/v1.7.0.20/GlassNotification.OSLink.msix) — アプリ本体と必要なランタイムを含むインストーラー
- [GlassNotification.OSLink.cer](https://github.com/sukesan666/GlassNotification/releases/download/v1.7.0.20/GlassNotification.OSLink.cer) — 初回インストール用の公開証明書
- [インストール手順](dist/インストール手順.md) — 証明書の登録、更新、アンインストール

上のリンクからMSIXとCERをダウンロードし、同じフォルダーへ置いてください。MSIXを使う場合、EXEを別途ダウンロードする必要はありません。

## 主な機能

- Windows通知との連動、通知元ごとの表示設定、通知履歴
- Acrylic・Mica・不透明表示の切り替えと、Acrylicの弱・標準・強
- カードと文字の色、フォント、大きさ、表示時間、配置モニター、表示位置の変更
- 本文画像 → 送信者アイコン → アプリアイコンの順で画像を選択。画像の大きさと左右の位置を変更可能
- 通知は最大3枚同時に表示
- 通知カードをクリックして元の通知の操作を実行
- スライドアニメーション、通知音、通知領域への最小化、自動起動
- 設定画面のライト／ダークテーマ、日本語／English
- 設定モードでは固定プレビューを表示し、数値設定をスライダーで調整。動作モードでは通常の通知を受信

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

---

# GlassNotification (English)

GlassNotification is a Windows 11 app that displays Windows notifications as frosted-glass-style cards. It receives notifications from apps such as Chrome and lets you customize the cards' colors, images, position, and notification sounds.

## Download and Installation

The current release is **[1.7.0.20](https://github.com/sukesan666/GlassNotification/releases/tag/v1.7.0.20) / Windows 11 version 22H2 or later / x64**.

- [GlassNotification.OSLink.msix](https://github.com/sukesan666/GlassNotification/releases/download/v1.7.0.20/GlassNotification.OSLink.msix) — Installer containing the app and required runtimes
- [GlassNotification.OSLink.cer](https://github.com/sukesan666/GlassNotification/releases/download/v1.7.0.20/GlassNotification.OSLink.cer) — Public certificate required for the initial installation
- [Installation instructions (Japanese)](dist/インストール手順.md) — Certificate setup, updates, and uninstallation

Download the MSIX and CER files from the links above and place them in the same folder. If you use the MSIX, you do not need to download the EXE separately.

## Features

- Integration with Windows notifications, per-app display settings, and notification history
- Acrylic, Mica, and opaque display modes, with low, standard, and high Acrylic intensity
- Customizable card and text colors, fonts, sizes, display duration, monitor, and position
- Images are selected in this order: notification image, sender icon, then app icon. Image size and left/right placement can be adjusted.
- Displays up to three notifications at once
- Click a notification card to invoke actions from the original notification
- Slide animations, notification sounds, minimization to the notification area, and automatic startup
- Light and dark themes, and Japanese and English language options in the settings UI
- Settings mode shows a pinned preview and lets you adjust numeric settings with sliders. In normal mode, the app receives notifications as usual.

See the [user guide (Japanese)](docs/使い方.md) for usage instructions and limitations.

## Development and Build

The source code is in [outputs/GlassNotification](outputs/GlassNotification). Building requires Visual Studio C++ development tools, the Windows SDK, and .NET Framework 4.8.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\outputs\GlassNotification\build.ps1 -Test All
```

Dependencies are obtained from the official NuGet source, and their pinned versions and SHA-256 hashes are verified. The build produces an EXE, a companion EXE, and a signed MSIX. See the [development and distribution guide (Japanese)](docs/開発・配布.md) for details.

## Repository Structure

| Path | Contents |
| --- | --- |
| `dist/` | MSIX package, public certificate, and installation instructions |
| `docs/` | User, development, and distribution guides |
| `outputs/GlassNotification/src/` | C# / WPF source code |
| `outputs/GlassNotification/native/` | C++ rendering, Windows notification integration, and build scripts |
| `outputs/GlassNotification/identity/` | Package definition and signing process |
| `outputs/GlassNotification/tests/` | Automated tests and verification code |
| `outputs/GlassNotification/licenses/` | Licenses for the bundled Windows App SDK |

Personal settings, notification records, private signing keys, and build caches are not included. See `licenses/` for the licenses of third-party components. The license for the app itself has not yet been defined in this repository.
