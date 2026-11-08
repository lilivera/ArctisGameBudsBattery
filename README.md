# Arctis GameBuds Battery

<img src="images/icon.png" alt="左右のイヤホンとミント色のバッテリーを組み合わせたアイコン" width="128" height="128">

SteelSeries Arctis GameBudsの左右バッテリー残量を、FINAL FANTASY XIV内に表示するDalamudプラグインです。
[InzoneBudsBattery](https://github.com/zxe-ll/InzoneBudsBattery)をベースに、GameBuds用の通信・解析処理へ置き換えています。

**バージョン0.1.2：プラグイン用アイコンを追加。残量取得・再接続・停止はv0.1.1でWindows実機確認済み。ゲーム内での表示・操作は未検証です。**

## 機能

- 左右個別、または使用中のイヤホンで低い側の残量をオーバーレイ表示
- DTRバー（サーバー情報バー）への表示
- 片耳使用時は使用中の側だけ表示
- 残量に応じた警告色、文字サイズ、背景透明度、位置固定
- 戦闘中・ゲームUI非表示時・未接続時の表示設定
- 定期照会（初期値30秒）、エラー時の再試行、手動再接続
- 古い値の明示、DTRでは古い残量を非表示
- 更新間隔に合わせた鮮度しきい値の自動補正（例：更新300秒なら最低6分）
- ゲームを起動せず利用できる診断ツール

## 対応環境と制限

- Windows 10/11 x64、XIVLauncher / Dalamud API Level 15
- Arctis GameBuds USBドングル：VID `1038` / PID `230A`
- 対象HID：インターフェース3、Usage Page `FFC0` / Usage `0001`
- イヤホンをドングルに接続する2.4GHzモードで使用
- ケースの残量、ケース内のイヤホン残量、Bluetooth単独接続には非対応
- 別PIDのモデルやファームウェアは未対応。製品名だけで他のSteelSeries機器へ送信しません。

外部HIDライブラリや管理者権限は不要です。共有アクセスを使用しますが、SteelSeries GGとの同時利用は実機未検証です。

## インストール

Dalamudのカスタムプラグインリポジトリからインストールできます。

```text
https://raw.githubusercontent.com/lilivera/ArctisGameBudsBattery/main/pluginmaster.json
```

1. XIVLauncherからFF14を起動し、`/xlsettings` を開きます。
2. 「試験的機能 / Experimental」→「カスタムプラグインリポジトリ」へ上記URLを追加し、有効にします。
3. `/xlplugins` で `Arctis GameBuds Battery` を検索してインストールします。
4. `/gamebuds` で設定画面を開きます。

配布ZIPは[GitHub Releases](https://github.com/lilivera/ArctisGameBudsBattery/releases/latest)からも取得できます。
カスタムリポジトリでは、GitHub Releasesへの通信で504エラーになる環境でも取得できるよう、同じZIPを `dist/vX.Y.Z/latest.zip` から配布します。
すでにローカルのDev Pluginとして読み込んでいる場合は、そちらをアンロードしてから切り替えてください。

導入済みの場合は `/xlplugins` の更新操作で最新版へ更新できます。設定は引き継がれ、更新間隔に対して短すぎる鮮度しきい値だけ自動補正されます。

## ローカルビルドを試す

この作業フォルダでは、ビルド済みのプラグインを次へ出力します。

```text
src\ArctisGameBudsBattery\bin\Release\ArctisGameBudsBattery.dll
```

1. XIVLauncherからFF14を起動します。
2. `/xlsettings` の「試験的機能 / Experimental」を開きます。
3. 「Dev Plugin Locations」へ上記DLLの**絶対パス**を追加します。
4. `/xlplugins` の開発用プラグイン一覧から `Arctis GameBuds Battery` を読み込みます。
5. `/gamebuds` で設定画面を開きます。最初の取得後に左右残量が表示されます。

DLLだけを別の場所へ移動しないでください。`ArctisGameBudsBattery.Core.dll`、manifest、依存関係情報も同じフォルダに必要です。
別フォルダで使う場合は `artifacts/latest.zip` をすべて展開し、展開先のDLLを指定してください。

| コマンド | 動作 |
| --- | --- |
| `/gamebuds` | 設定画面の表示切り替え |
| `/gamebuds status` | 接続状態と残量を通知 |
| `/gamebuds reconnect` | 待機を中断して再接続・再取得 |
| `/gamebuds debug` | 生レポートの診断ログを切り替え |

## ビルド・テスト

.NET 10 SDKとDalamudの開発ファイルが必要です。通常は `%APPDATA%\XIVLauncher\addon\Hooks\dev` が自動検出されます。

```powershell
# ビルド、テスト、ZIP検査、artifactsへのコピー
.\scripts\Build.ps1

# 個別に実行する場合
dotnet build .\ArctisGameBudsBattery.slnx -c Release
dotnet test .\tests\ArctisGameBudsBattery.Tests -c Release --no-build
dotnet test .\tests\ArctisGameBudsBattery.Plugin.Tests -c Release --no-build
```

再ビルドする際は、ゲーム内でプラグインをアンロードしてDLLのロックを解除してください。

## 新しいバージョンの公開

1. プロジェクトの `AssemblyVersion` / `FileVersion` を更新します。
2. `pluginmaster.json` のバージョン、3つのダウンロードURL、`LastUpdate`（UTCのUnix秒）を更新します。
3. `docs/releases/vX.Y.Z.md` に日本語のリリースノートを追加します。
4. `scripts/Build.ps1` で検証してコミットし、バージョンに対応するタグ（例：`0.1.1.0` → `v0.1.1`）を先にpushします。
5. GitHub Actionsの成功後、公開された `latest.zip` を `dist/vX.Y.Z/latest.zip` にダウンロードします。ローカルで再ビルドしたZIPではなく、公開されたファイルをそのまま配置してください。
6. GitHub ReleaseのSHA256と一致すること、および `scripts/Test-PluginRepository.ps1 -PackagePath dist/vX.Y.Z/latest.zip -ReleaseTag vX.Y.Z` の成功を確認し、ZIPの追加をコミットして `main` へpushします。
7. 公開された `pluginmaster.json` のダウンロード先へ、Dalamudと同じ `Accept: application/zip` を付けてGETし、取得したZIPを再検証します。

GitHub Actionsがビルド・テスト・配布定義の整合性検査を実行し、GitHub Releaseへ `latest.zip` を添付します。3つのダウンロードURLは、バージョンに対応する `https://raw.githubusercontent.com/lilivera/ArctisGameBudsBattery/main/dist/vX.Y.Z/latest.zip` を指定してください。
公開が完了すると、同じカスタムリポジトリURLからインストール・更新できます。
この順序により、配布ZIPが完成するまでカスタムリポジトリは既存の利用可能なバージョンを案内します。

## ゲームなしで残量を確認する

診断ツールはDalamud不要です。プラグインと同じ通信・解析処理を使用します。

```powershell
# 対象HIDの列挙だけを行う
dotnet run --project .\probes\ArctisGameBudsProbe -c Release -- --list

# 1回取得
dotnet run --project .\probes\ArctisGameBudsProbe -c Release

# 生レポート付きで5秒ごとに3回取得
dotnet run --project .\probes\ArctisGameBudsProbe -c Release -- --samples 3 --interval 5 --raw
```

## 構成

```text
src/ArctisGameBudsBattery/       Dalamud・設定・表示
src/ArctisGameBudsBattery.Core/  HID通信・残量解析・定期取得
probes/ArctisGameBudsProbe/      実機診断ツール
tests/ArctisGameBudsBattery.Tests/  解析・鮮度・再接続のテスト
tests/ArctisGameBudsBattery.Plugin.Tests/  初期化・終了・設定適用の回帰テスト
docs/PROTOCOL.md                通信仕様と根拠
docs/VALIDATION.md              確認済みの結果と残りの検証項目
```

MIT License。元実装の著作権表示を[LICENSE](LICENSE)に保持しています。利用箇所と調査元は[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)を参照してください。
