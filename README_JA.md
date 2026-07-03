![](Documents/debugx.png)

<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/blurfeng/debugx?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/blurfeng/debugx/total?color=green">
  <img alt="GitHub Repo License" src="https://img.shields.io/github/license/blurfeng/debugx?color=blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/blurfeng/debugx?color=yellow">
</p>

<p align="center">
  🌍
  <a href="./README.md">中文</a> |
  <a href="./README_EN.md">English</a> |
  日本語
</p>

<p align="center">
  📥
  <a href="#upm-を使う">インストール</a> |
  <a href="#パッケージをダウンロード">ダウンロード</a>
</p>

# Debugx - Unity デバッグログ管理プラグイン
Debugx は `Unity` 向けのデバッグログ拡張プラグインで、すぐに使えます。`Debug.Log` の出力を**デバッグメンバー**（開発者や機能モジュール）単位で分類・管理し、ログをローカルファイルに出力できます。  
多人数で開発するプロジェクトでは、全員が `UnityEngine.Debug.Log()` を使うとログの区別と管理が難しくなります。また自分の機能をテストするとき、他人のログ出力に邪魔されたくありません。Debugx は**メンバー分類 + 多段スイッチ**によって、各自が自分のログだけに集中でき、互いに干渉しません。  
すべての出力メソッドはマクロ `DEBUG_X` で制御されます。マクロを追加すると有効になり、リリース時にマクロを外すと、すべてのログ呼び出しが**コンパイル時**に取り除かれ、Release ビルドでオーバーヘッドゼロ・残留ゼロを実現します。  
自動生成されるメンバー専用メソッド（例：`DebugxLogger.LogBlur("...")`）と、ラップされた `DebugxLog.dll` により、Key を覚えることなく出力でき、コンソールのログをダブルクリックするとプラグイン内部ではなく業務側の呼び出し箇所へ直接ジャンプできます。

![](Documents/overview.png)

## 📜 目次
- [概要](#概要)
  - [特徴](#特徴)
- [💻 動作環境](#-動作環境)
- [🌱 クイックスタート](#-クイックスタート)
  - [1.プラグインのインストール](#1プラグインのインストール)
  - [2.DEBUG_X マクロの追加](#2debug_x-マクロの追加)
  - [3.デバッグメンバーの設定](#3デバッグメンバーの設定)
  - [4.コードでログを出力](#4コードでログを出力)
- [⚙️ 設定ガイド](#-設定ガイド)
  - [設定 UI と Tooltip](#設定-ui-と-tooltip)
  - [ProjectSettings プロジェクト設定](#projectsettings-プロジェクト設定)
  - [Preferences ユーザー設定](#preferences-ユーザー設定)
- [✍️ コードでのログ出力](#-コードでのログ出力)
  - [出力メソッド](#出力メソッド)
  - [プリセットメンバーと Key](#プリセットメンバーと-key)
  - [実行時スイッチ](#実行時スイッチ)
- [🎛️ DebugxConsole コンソール](#-debugxconsole-コンソール)
  - [PlayingSettings 実行時設定](#playingsettings-実行時設定)
  - [Test テスト](#test-テスト)
- [🧩 DebugxManager マネージャー](#-debugxmanager-マネージャー)
- [⚠️ 注意事項](#-注意事項)

## 概要
Debugx を使うと、多人数開発のプロジェクトでログをデバッグメンバー単位で分類・一元管理でき、全員の `Debug.Log` が混ざって区別しづらくなるのを防げます。  
Debugx は `ProjectSettings` と `Preferences` の両方に設定 UI を用意しています。`ProjectSettings` の設定は**プロジェクト全体**に影響し、`Preferences` のユーザー設定は**自分のローカル環境のみ**に影響し、プロジェクトや他の開発者には影響しません。さらに `DebugxConsole` は**実行時**に出力スイッチなどを管理します。  
業務向けの出力メソッドはすべて `[Conditional("DEBUG_X")]` が付いているため、`DEBUG_X` マクロが無い場合、これらの呼び出しはコンパイル時に丸ごと取り除かれ、実行時オーバーヘッドは一切発生しません。

### 特徴
| 特徴 | 説明 |
| --- | --- |
| メンバー別ログ | 「デバッグメンバー」（開発者 / モジュール）単位で分類出力。各メンバーは個別のスイッチ・署名・色を持ち、ログが見やすく干渉しません。 |
| 3 段階スイッチ | プロジェクト単位（`ProjectSettings`）、ローカルユーザー単位（`Preferences`）、実行時単位（`DebugxConsole` / コード）の 3 段を自由に組み合わせ。 |
| DEBUG_X マクロで一括 ON/OFF | すべての出力メソッドは `[Conditional("DEBUG_X")]` 付き。マクロを外せば全ログ呼び出しがコンパイル時に消え、Release でオーバーヘッド・残留ゼロ。 |
| メンバーメソッドの自動生成 | メンバー設定から `DebugxLogger.LogXxx()` などの専用メソッドを生成。Key を覚えずに `LogBlur("...")` と呼べます。 |
| 正確なスタックジャンプ | コアコードを `DebugxLog.dll` にラップし、`Logger` 命名と `[HideInCallstack]` と組み合わせることで、コンソールのログをダブルクリックするとプラグイン内部ではなく業務側の呼び出し箇所へ直接ジャンプ。 |
| ローカルログ出力 | 実行時にログをローカルファイルへ記録。エディターはプロジェクトの `Logs/` へ、各プラットフォームは対応ディレクトリへ出力。スタックトレースや画面描画なども設定可能。 |
| 豊富な出力オプション | タイムスタンプ、ネットワークタグ（Server / Client）、色、署名、Header に対応。`Log` / `LogWarning` / `LogError` を提供。 |
| エディター親和性 | `ProjectSettings` と `Preferences` に統合され、各フィールドに Tooltip 付き。Dark / Light スキンに対応し、UI はシステム言語に応じて中国語 / 英語を切り替え。 |

## 💻 動作環境
- `Unity 2021.3` 以降（それより古いバージョンは未検証）。
- 機能を有効にするには、プロジェクトに `DEBUG_X` マクロを追加する必要があります（[2.DEBUG_X マクロの追加](#2debug_x-マクロの追加) を参照）。
- サードパーティ依存なし。

## 🌱 クイックスタート
好きな方法でプラグインをインストールし、以下の手順で Debugx をプロジェクトに追加します。

### 1.プラグインのインストール
#### UPM を使う
UPM（Unity Package Manager）でプラグインをインストールします：
```
https://github.com/BlurFeng/Debugx.git?path=Assets/Plugins/Debugx
```
1. 上記のリンクをコピーします。
2. `Window -> Package Manager` を開きます。
3. 左上の `+` ボタンをクリックし、`Add package from git URL...` を選択します。
4. リンクを貼り付け、`Install` をクリックしてプラグインをプロジェクトに追加します。

#### パッケージをダウンロード
[Releases](https://github.com/blurfeng/debugx/releases) ページから最新の `.unitypackage` をダウンロードし、プロジェクトにインポートします。

### 2.DEBUG_X マクロの追加
ログ出力機能を有効にするには、プロジェクトにマクロ `DEBUG_X` を追加する必要があります。`Project Settings -> Player -> Other Settings -> Scripting Define Symbols` に `DEBUG_X` を追加してください。  
リリース時にマクロ `DEBUG_X` を外すと、Debugx の全機能を素早く無効化できます（関連する呼び出しはコンパイル時に取り除かれます）。  
![](Documents/qs_macro_1.png)

### 3.デバッグメンバーの設定
`Editor -> Project Settings -> Debugx` を開き、**デバッグメンバー**でメンバーを設定します。  
各メンバーは一意の `Key`、`Signature`（署名 / 名前）、色、スイッチなどの属性を持ちます。**最も重要なのはメンバーの `Key`** で、ログ出力時に使用します。各メンバーは自分の `Key` だけ覚えておけば十分です。  
保存すると、Debugx は各メンバーの専用出力メソッドを**自動生成**します（[4.コードでログを出力](#4コードでログを出力) を参照）。  
![](Documents/qs_member_1.png)

### 4.コードでログを出力
これでコードからログを出力できます。**メンバー専用メソッド**（Key 不要）でも、**汎用メソッド**（Key を渡す）でも使えます：

```csharp
using DebugxLog;

// メンバー専用メソッド（推奨、Key を覚える必要なし）。メソッド名はメンバーの Signature から生成されます。
DebugxLogger.LogBlur("Hello from Blur.");
DebugxLogger.LogWarningBlur("Something looks off.");
DebugxLogger.LogErrorBlur("Something went wrong.");

// 汎用メソッド（メンバーの Key が必要）。
Debugx.Log(1, "Hello from key 1.");
Debugx.LogWarning(1, "Warning from key 1.");
Debugx.LogError(1, "Error from key 1.");
```

> [!TIP]
> `DebugxLogger` クラスはメンバー設定から**自動生成**されます。プラグイン更新後に `DebugxLogger` が生成されない場合や、メンバーを追加したのに対応メソッドが無い場合は、メニュー `Tools -> Debugx -> Regenerate DebugxLogger Class` で強制的に再生成してください。

> [!TIP]
> ここまでで Debugx は正常に動作します。各種設定や使い方をより詳しく知りたい場合は、下の[設定ガイド](#-設定ガイド)と[コードでのログ出力](#-コードでのログ出力)を読み進めてください。

## ⚙️ 設定ガイド
Debugx の設定は 2 か所に分かれています：`ProjectSettings`（プロジェクト全体に影響）と `Preferences`（自分のローカル環境のみに影響）。以下では主要なオプションを説明します。各フィールドにマウスを重ねると Tooltip で詳細を確認できます。

### 設定 UI と Tooltip
フィールドにマウスを重ねると Tooltip が表示され、Debugx に慣れる助けになります。Tooltip で詳細を確認できるため、ここでは一つ一つ繰り返し説明しません。  
![](Documents/cfg_tooltip_1.png)

### ProjectSettings プロジェクト設定
`Editor -> Project Settings -> Debugx` を開きます。プロジェクト設定はプロジェクト全体に影響します。デバッグメンバーを追加したり、グローバルな既定動作を調整したりする際はここで設定します。  
![](Documents/cfg_projectsettings_1.png)

#### Toggle スイッチ設定
各種グローバルスイッチの既定値です。メインスイッチはここに表示され、各デバッグメンバーもメンバー情報で個別にスイッチを設定できます。主なオプション：
- `enableLogDefault`：ログのメインスイッチの既定値。オフにするとメンバーログを一切出力しません。
- `enableLogMemberDefault`：メンバーログのメインスイッチの既定値。
- `allowUnregisteredMember`：未登録（対応する Key / 署名が見つからない）のメンバーによる出力を許可するか。
- `logThisKeyMemberOnlyDefault`：特定の Key のメンバーのログのみ出力。`0` でこのフィルタを無効化。

![](Documents/cfg_toggle_1.png)

#### Member デバッグメンバー設定
メンバー設定でデバッグメンバーを構成します。ここにはいくつかの**プリセットメンバー**（[プリセットメンバーと Key](#プリセットメンバーと-key) を参照）があり、削除できず、編集も限定的です。**カスタムメンバー**では、プロジェクト利用者ごとに専用の設定を追加できます。  
メンバーごとに設定できる主な属性：
- `Key`：メンバーの一意識別子。出力時に使用します。**各メンバーは自分の Key だけ覚えておけば十分です。**
- `Signature`：署名 / 名前。`DebugxLogger` のメソッド名の生成にも使われます（例：`Blur` -> `LogBlur`）。
- `Color`：ログの色。コンソールでメンバーを素早く見分けられます。
- `Header`：任意のログ接頭ラベル。
- `EnableDefault`：このメンバーのログの既定スイッチ。

![](Documents/cfg_member_1.png)

#### LogOutput ログ出力
ログ出力は、プロジェクトの実行開始時に記録を開始し、実行停止時に記録を終了してローカルファイルへ出力します。主なオプション：
- `logOutput`：ログをローカルファイルへ出力するか。
- `enableLogStackTrace` / `enableWarningStackTrace` / `enableErrorStackTrace`：それぞれ Log / Warning / Error タイプのスタックトレースを記録するか。
- `recordAllNonDebugxLogs`：Debugx 以外が出力したすべてのログを記録するか。
- `drawLogToScreen` / `restrictDrawLogCount` / `maxDrawLogs`：ログを画面に描画するか、描画数を制限するか、その上限。

ログファイルの出力先：
- **エディター**：プロジェクトルートの `Logs` フォルダー。
- **リリースビルド**：プラットフォームごとの対応ディレクトリに保存。PC では通常 `C:\Users\ユーザー名\AppData\LocalLow\会社名\製品名`、モバイルでは対応する永続データディレクトリです。

![](Documents/cfg_logoutput_1.png)

### Preferences ユーザー設定
`Editor -> Preferences -> Debugx` を開きます。  
ユーザー設定は**自分のローカルプロジェクト環境のみ**に影響し、他の開発者やリリースビルドには影響しません。主に、各開発者がローカルで好みに合わせて設定するためのものです。通常は各自が自分のデバッグメンバーのスイッチだけを有効にし、他人の出力に邪魔されないようにします。  
![](Documents/cfg_preferences_1.png)

> [!NOTE]
> エディターで実行しているときに有効なのはローカルの `Preferences` 設定です。リリースビルドでは、`ProjectSettings` にコミットされたプロジェクト設定が有効になります。

## ✍️ コードでのログ出力
`DebugxLogger` または `Debugx` クラスの静的メソッドを呼ぶだけでログを出力できます。すべての出力メソッドはマクロ `DEBUG_X` で制御されます。  
![](Documents/code_1.png)

### 出力メソッド
**`DebugxLogger.LogXxx(message, showTime, showNetTag)`**  
対応するデバッグメンバーの専用メソッドを呼び出します。`Xxx` はメンバーの署名（Signature）です。最も推奨される方法で、**Key を覚える必要がありません**。`LogWarningXxx` / `LogErrorXxx` も提供されます。

**`Debugx.Log(key, message, showTime, showNetTag)`**  
最も汎用的なメソッドで、メンバーの `Key` と出力内容を渡します。`Key` はメンバー設定でメンバーに割り当てた識別子です。`Debugx.LogWarning` / `Debugx.LogError` も提供されます。Key の代わりに**署名**を使うこともできます：`Debugx.Log(signature, message, ...)`。

主なパラメーター：
- `showTime`：ログにタイムスタンプを表示するか。
- `showNetTag`：ネットワークタグ（Server / Client）を表示するか。これはプロジェクト側の実装に依存し、`Debugx.SetServerCheck(Func<bool>)` で「サーバーかどうか」を判定するメソッドを設定して初めて有効になります。

**`Debugx.LogAdm(message)`**  
`LogAdm` シリーズは **Debugx プラグイン開発者専用**で、他の人は使用すべきではありません。これで出力したログは `DebugxManager` のメンバースイッチでは制御されませんが、マクロ `DEBUG_X` の影響は受けます。

### プリセットメンバーと Key
Debugx にはいくつかの固定プリセットメンバーが組み込まれており、その Key は予約済みです。カスタムメンバーには使用しないでください：
- `Normal`（Key `-1`）：通常メンバー。
- `Master`（Key `-2`）：上級メンバー。
- `Admin`（Key `0`）：管理者メンバー。`LogAdm` チャンネルに対応。

カスタムメンバーには**正の整数**の Key を使ってください（`Key > 0` のみが有効なカスタム Key として扱われます）。

### 実行時スイッチ
実行時にコードから出力を動的に制御できます：
- `Debugx.SetMemberEnable(int key, bool enable)`：特定メンバーのログを切り替え（`DebugxManager.Instance.SetMemberEnable(...)` でも可）。
- `Debugx.enableLog` / `Debugx.enableLogMember`：ログのメインスイッチ / メンバーログのメインスイッチ。
- `Debugx.logThisKeyMemberOnly`：ある Key に設定すると、その Key のメンバーのログのみ出力（`0` でこのフィルタを無効化）。

これらのスイッチは、実行時に [DebugxConsole コンソール](#-debugxconsole-コンソール) から視覚的に調整することもできます。

## 🎛️ DebugxConsole コンソール
`DebugxConsole` は主に、プロジェクトの**実行時**に Debugx 機能を切り替えるために使います。`Window -> Debugx -> DebugxConsole` からウィンドウを開きます。便利のため、`Game` タブと並べて配置できます。  
![](Documents/console_1.png)

### PlayingSettings 実行時設定
実行時設定の内容は基本的に `ProjectSettings` と同じですが、プロジェクトの実行中にリアルタイムで調整でき、動かしながらの調整に便利です。  
![](Documents/console_playing_1.png)

### Test テスト
テスト機能モジュール。Debugx が正常に動作しているかを確認するための便利なスイッチ（`EnableAwakeTestLog` / `EnableUpdateTestLog` などのテスト出力スイッチ）を提供します。

## 🧩 DebugxManager マネージャー
`DebugxManager` はゲーム実行時に**自動生成**され、通常は手動管理は不要です。主な役割は `LogOutput` 関連の処理（記録の開始 / 終了、出力パスの設定、画面への描画など）です。  
`DebugxManager` は、マクロ `DEBUG_X` がプロジェクトに追加されている場合にのみ、`[RuntimeInitializeOnLoadMethod]` によって実行時に自動生成されます。`Create()` メソッドは `virtual` で、プロジェクト側で派生して拡張できます。

## ⚠️ 注意事項
> [!TIP]
> 1. Debugx 機能を有効にするには、プロジェクトにマクロ `DEBUG_X` を追加する必要があります。
> 2. プラグイン更新後に `DebugxLogger` クラスが生成されない場合は、メニュー `Tools -> Debugx -> Regenerate DebugxLogger Class` で強制的に再生成してください。
> 3. `2.3.0` より前のバージョンは、フォルダー構成と UPM リンクの変更により通常の更新ができません。旧バージョンを削除してから再インストールしてください。
