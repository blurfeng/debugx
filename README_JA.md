![](Documents/Images/Debugx.png)

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

# Debugx
Unity 専用のデバッグ機能拡張プラグイン。設定によりデバッグメンバー別に Debug Log を分類印刷・管理し、ログファイルをローカルに出力できます。

詳細情報は [ユーザーマニュアル](Documents/UserManual_ja.md) をご覧ください。

# 注意事項
> [!TIP]
> 1. プラグインは 2.3.0 以前のバージョンではフォルダ構造と UPM リンクが変更されたため、正常に更新できず、古いバージョンを削除してから再インストールする必要があります。

## Unity バージョン要件
Unity 2021.3 以上。

## UPM インストール
UPM（Unity Package Manager）を使用してプラグインをインストールします。
```
https://github.com/BlurFeng/Debugx.git?path=DebugxDemo/Assets/Plugins/Debugx
```
1. 上記のリンクをコピー
2. Unity エディタを開き、Window > Package Manager に移動
3. ウィンドウ左上の + ボタンをクリックし、"Add package from git URL..." を選択
4. リンクを貼り付けて、プラグインをプロジェクトにインストール

## 概要
Debugx は Unity エンジン専用に開発されたデバッグプラグインです。  
デバッグメンバー別に DebugLog を管理し、ログファイルをローカルに出力します。  
プロジェクトにマクロ "DEBUG_X" を追加して Debugx 機能を有効にする必要があります。

### 使い方
設定されたデバッグメンバーに基づいて、対応する Log メソッドが自動的に生成されます。   
コード内で Debugx.LogMemberName() などのメソッドを使用して、簡単にログを印刷できます。   
![](Documents/Images/Debugx_Use.png)

### DOTS Burst 環境
Unity DOTS の Burst 環境では、多くのメソッドとフィールドが Burst で利用できないため、Debugx の代わりに DebugxBurst を使用する必要があります。  
ただし、Unity DOTS の更新が非常に頻繁なため、異なる DOTS バージョンでは完全な信頼性を保証できません。  
![](Documents/Images/DebugxBurst.png)
