# TabNest

## 概要

TabNest は、Windows向けの軽量ファイラーである。  
Windows 11 標準エクスプローラーのタブUIに対する不満を解消し、作業単位でフォルダタブを整理できることを目的とする。  

主な目的は以下である。

* タブ幅を小さくする
* タブの閉じるボタンを常時表示しない
* ホイールクリックでのみタブを閉じる
* 閉じたタブを再度開ける
* 作業単位で複数フォルダをグループ化する
* タブグループを複数段で表示する
* グループ単位でフォルダセットをまとめて開く
* ローカルフォルダ操作に特化した軽量なWindowsファイラーにする

本アプリは、Windows標準エクスプローラーの完全互換を目指さない。
ネットワークドライブ、OneDrive、特殊フォルダ、Shell拡張の完全対応は初期対象外とする。

---

## 想定技術スタック

### アプリ本体

```text
言語: C#
UI: WinUI 3
基盤: Windows App SDK
設計: MVVM
対象OS: Windows 11
設定保存: JSON
```

### テスト

```text
単体テスト: xUnit
結合テスト: xUnit + 一時フォルダを使った実ファイル操作
GUIテスト: Appium Windows Driver + WinAppDriver
UI要素検査: Inspect.exe / Accessibility Insights for Windows
CI: GitHub Actions windows-2025
```

### 開発補助

```text
AI支援: GitHub Copilot / Claude Code / Codex
静的解析: .NET analyzers
フォーマット: dotnet format
```

---

## 基本方針

### 作るもの

本アプリは、以下の性質を持つ。

```text
作業グループ対応のタブ型ローカルフォルダビューア
```

### 作らないもの

初期段階では以下を対象外とする。

```text
- Windows Explorerの完全互換
- Shell拡張右クリックメニューの完全再現
- ネットワークドライブ対応
- OneDrive特殊状態表示
- zipフォルダの仮想フォルダ表示
- 高度なサムネイル生成
- Windows Search連携
- 管理者権限が必要なフォルダ操作
```

### 重要な設計方針

* 標準TabViewには強く依存しない
* タブバーは自前実装を基本とする
* ファイル操作ロジックとUIを分離する
* ViewModelとサービス層を単体テスト可能にする
* GUIテストしやすいように主要UI要素へAutomationIdを付与する
* AIに実装させやすいように、タスクを小さく分割する
* AIや自動操作による高速・大量操作を前提に、状態不整合を「稀なケース」として放置しない
* ナビゲーションや履歴操作では、表示中パス、読込済み一覧、履歴スタック、コマンド可否が常に整合することを重視する
* 操作失敗時は、次の自動操作が誤った状態を前提に進まないよう、状態の巻き戻しまたは一貫した失敗状態を明示する

---

## 主要機能

## タブ

### 必須機能

* タブは1つのフォルダパスを表す
* タブにはタイトルを表示する
* タイトルは基本的にフォルダ名とする
* タブ幅は小さめにする
* タブの閉じるボタンは表示しない
* タブはホイールクリックで閉じる
* 左クリックでタブを選択する
* 右クリックでタブメニューを表示する
* Ctrl+Shift+T で最後に閉じたタブを再度開く

---

## タブグループ

### 必須機能

* タブは必ず1つのタブグループに所属する
* タブグループは1行として表示する
* タブグループは最大5個まで作成できる
* 各グループは名前を持つ
* 各グループ内のタブは横方向に並ぶ
* 横幅に収まらない場合は水平スクロールする
* グループ単位でタブセットを保存できる
* グループ単位でタブセットを復元できる
* 各グループの保持するタブ数は最大で20とする

### 表示イメージ

```text
[作業A] [src] [docs] [test] [build] [memo]  →
[作業B] [写真] [export]                   →
[作業C] [tools] [temp] [downloads] ...     →
```

---

## ファイル一覧

### 必須機能

* 選択中タブのフォルダ内容を表示する
* ファイル名を表示する
* フォルダ名を表示する
* 種別を表示する
* ファイルとフォルダの違いをアイコンまたは文字色で区別する
* 更新日時を表示する
* サイズを表示する
* タイトル行のクリックで列ごとの昇順・降順を切り替える
* タイトル列の区切りをダブルクリックすると、表示文字列に応じて列幅を自動調整する
* 自動調整後の列幅は、ウィンドウ幅内に余裕を持って収まる最大幅と、操作不能にならない最小幅を持つ
* ダブルクリックでフォルダへ移動する
* ダブルクリックでファイルを既定アプリで開く
* 上の階層へ移動できる
* 戻る・進むができる
* アドレスバーにダブルクォート括りのパスが貼り付けられた場合は、外側のダブルクォートを除去して移動する

### 初期表示モード

```text
詳細表示
```

### 初期ソート

```text
フォルダを先頭に表示
名前昇順
```

---

## 設定保存

設定はJSONで保存する。

### 保存対象

```text
- ウィンドウサイズ
- 最後に開いていたタブグループ
- 各タブグループの名前
- 各タブグループ内のフォルダパス一覧
- 最後に選択していたタブ
- 閉じたタブ履歴
```

### 設定ファイル保存先

```text
%AppData%\TabNest\settings.json
```

---

## データモデル

## TabGroup

```csharp
public sealed class TabGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<FolderTab> Tabs { get; set; } = new();
    public string? SelectedTabId { get; set; }
}
```

## FolderTab

```csharp
public sealed class FolderTab
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

## ClosedTab

```csharp
public sealed class ClosedTab
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int TabIndex { get; set; }
    public DateTime ClosedAt { get; set; }
}
```

## AppSettings

```csharp
public sealed class AppSettings
{
    public List<TabGroup> TabGroups { get; set; } = new();
    public List<ClosedTab> ClosedTabs { get; set; } = new();
    public string? ActiveGroupId { get; set; }
    public string? ActiveTabId { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
}
```

---

## テスト方針

## テスト階層

本プロジェクトでは、テストを以下の3層に分ける。

```text
Level 1: 単体テスト
Level 2: 結合テスト
Level 3: GUI自動テスト
```

---

## Level 1: 単体テスト

対象:

```text
- タブ追加
- タブ削除
- 閉じたタブ履歴
- Ctrl+Shift+T相当の復元処理
- タブ数上限
- グループ数上限
- 設定JSONのシリアライズ
- 設定JSONのデシリアライズ
- ファイル一覧のソート処理
```

使用ツール:

```text
xUnit
```

実行コマンド:

```powershell
dotnet test
```

---

## Level 2: 結合テスト

対象:

```text
- 一時フォルダを作成する
- 一時フォルダ内にファイルを作成する
- ファイル一覧サービスで読み取る
- フォルダとファイルを分類する
- 更新日時、サイズを取得する
- フォルダ移動履歴を検証する
- 設定ファイルを実際に保存・復元する
```

方針:

```text
- テストごとに一時フォルダを作成する
- テスト終了時に一時フォルダを削除する
- 実ユーザーフォルダには触れない
- C:\Windows など権限が絡むフォルダは使わない
```

---

## Level 3: GUI自動テスト

対象:

```text
- アプリが起動する
- 初期タブが表示される
- フォルダパスを開ける
- タブを追加できる
- タブをホイールクリックで閉じられる
- Ctrl+Shift+Tでタブを復元できる
- グループを追加できる
- グループごとにタブが表示される
- アプリ終了後にセッションが復元される
```

使用候補:

```text
Appium Windows Driver
WinAppDriver
xUnit
```

補助ツール:

```text
Inspect.exe
Accessibility Insights for Windows
```

GUIテストのため、主要なUI要素にはAutomationIdを必ず付与する。

例:

```xml
<Button
    AutomationProperties.AutomationId="AddTabButton" />

<ListView
    AutomationProperties.AutomationId="FileListView" />

<Button
    AutomationProperties.AutomationId="RestoreClosedTabButton" />
```

---

## プロジェクト構成

```text
tabnest/
├─ src/
│  ├─ TabNest.App/
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  ├─ Controls/
│  │  ├─ App.xaml
│  │  └─ MainWindow.xaml
│  │
│  ├─ TabNest.Core/
│  │  ├─ Models/
│  │  ├─ Services/
│  │  ├─ Interfaces/
│  │  └─ Utilities/
│  │
│  └─ TabNest.Testing/
│     └─ TestUtilities/
│
├─ tests/
│  ├─ TabNest.Core.Tests/
│  ├─ TabNest.Integration.Tests/
│  └─ TabNest.UiTests/
│
├─ docs/
│  └─ SPEC.md
│
├─ .github/
│  └─ workflows/
│     └─ ci.yml
│
├─ README.md
└─ TabNest.sln
```

---

# 工程

全体工程は5ステップとする。

```text
Step 1: 土台構築
Step 2: ファイル一覧とナビゲーション
Step 3: タブ・タブグループ
Step 4: 永続化と復元
Step 5: GUI自動テストと仕上げ
```

各タスクは、1日2〜4時間程度で実現可能な単位に分割する。

---

# Step 1: 土台構築

目的:

```text
WinUI 3アプリ、Coreライブラリ、テストプロジェクト、CIを用意する。
```

## Task 1-1: ソリューション作成

作業内容:

```text
- TabNest.sln を作成する
- TabNest.App を作成する
- TabNest.Core を作成する
- TabNest.Core.Tests を作成する
- プロジェクト参照を設定する
```

完了条件:

```text
- dotnet build が成功する
- dotnet test が成功する
```

テスト:

```text
- 空のテストを1つ作成して dotnet test で成功させる
```

---

## Task 1-2: MVVM基本構成を作成

作業内容:

```text
- MainViewModel を作成する
- MainWindow から MainViewModel を参照する
- 最低限のタイトル表示を行う
```

完了条件:

```text
- アプリを起動できる
- ウィンドウタイトルに TabNest と表示される
```

テスト:

```text
- MainViewModel の初期値を単体テストする
```

---

## Task 1-3: CIを作成

作業内容:

```text
- GitHub Actions の ci.yml を作成する
- windows-2025 で dotnet restore / build / test を実行する
```

完了条件:

```text
- push時にCIが実行される
- build と test が成功する
```

テスト:

```text
- GitHub Actions上で dotnet test が成功する
```

---

# Step 2: ファイル一覧とナビゲーション

目的:

```text
指定フォルダの内容を表示し、基本的なフォルダ移動ができるようにする。
```

## Task 2-1: FileSystemServiceを作成

作業内容:

```text
- IFileSystemService を定義する
- FileSystemService を実装する
- 指定フォルダ内のファイル・フォルダ一覧を取得する
```

完了条件:

```text
- フォルダ一覧を取得できる
- ファイル一覧を取得できる
- 存在しないパスの場合はエラー情報を返す
```

テスト:

```text
- 一時フォルダにファイルとフォルダを作成する
- FileSystemServiceで一覧取得する
- 件数、名前、種別を検証する
```

---

## Task 2-2: ファイル一覧ViewModelを作成

作業内容:

```text
- FileItemViewModel を作成する
- FolderViewModel を作成する
- CurrentPath を持たせる
- Items を持たせる
- LoadFolderCommand を作成する
```

完了条件:

```text
- ViewModelから指定フォルダを読み込める
- Itemsにファイル一覧が入る
```

テスト:

```text
- モックまたは一時フォルダを使ってLoadFolderCommandを検証する
```

---

## Task 2-3: ファイル一覧UIを作成

作業内容:

```text
- MainWindowにファイル一覧を表示する
- ファイル名、種別、更新日時、サイズを表示する
- CurrentPathを表示する
```

完了条件:

```text
- アプリ上でフォルダ内容が表示される
```

テスト:

```text
- ViewModel単体テストで表示対象データを検証する
- GUIテストはこの段階では任意
```

---

## Task 2-4: フォルダ移動を実装

作業内容:

```text
- フォルダをダブルクリックしたら移動する
- ファイルをダブルクリックしたら既定アプリで開く
- 上の階層へ移動するコマンドを追加する
```

完了条件:

```text
- フォルダ移動できる
- 上の階層へ移動できる
```

テスト:

```text
- FolderViewModelの移動履歴を単体テストする
- 一時フォルダで結合テストする
```

---

## Task 2-5: 戻る・進むを実装

作業内容:

```text
- BackStack を実装する
- ForwardStack を実装する
- BackCommand を作成する
- ForwardCommand を作成する
```

完了条件:

```text
- 戻る操作ができる
- 進む操作ができる
- 移動履歴が破綻しない
```

テスト:

```text
- A→B→C と移動後、戻る・進むを検証する
```

---

# Step 3: タブ・タブグループ

目的:

```text
複数フォルダをタブとして管理し、作業グループごとに複数段表示できるようにする。
```

## Task 3-1: タブモデルを作成

作業内容:

```text
- FolderTab モデルを作成する
- TabGroup モデルを作成する
- TabManagerService を作成する
```

完了条件:

```text
- タブを追加できる
- タブを削除できる
- アクティブタブを変更できる
```

テスト:

```text
- タブ追加の単体テスト
- タブ削除の単体テスト
- アクティブタブ変更の単体テスト
```

---

## Task 3-2: タブ上限とグループ上限を実装

作業内容:

```text
- 合計タブ数20個制限を実装する
- グループ数5個制限を実装する
- 上限超過時のエラー結果を定義する
```

完了条件:

```text
- タブ数が20個を超えない
- グループ数が5個を超えない
```

テスト:

```text
- 20個までは追加できる
- 21個目は追加できない
- 5グループまでは追加できる
- 6グループ目は追加できない
```

---

## Task 3-3: 自前タブバーUIを作成

作業内容:

```text
- TabGroupRow コントロールを作成する
- 各グループを1段で表示する
- グループ名を左側に表示する
- タブを横並びで表示する
- 水平スクロールを有効にする
```

完了条件:

```text
- 複数段のタブグループが表示される
- 各段で水平スクロールできる
```

テスト:

```text
- ViewModelに複数グループと複数タブを投入し、表示できることを手動確認する
- AutomationIdを付与する
```

---

## Task 3-4: タブ選択を実装

作業内容:

```text
- タブ左クリックでアクティブタブを変更する
- アクティブタブのフォルダ内容を表示する
- アクティブタブの見た目を変更する
```

完了条件:

```text
- タブを切り替えるとファイル一覧が切り替わる
```

テスト:

```text
- TabManagerServiceのアクティブタブ変更を単体テストする
- FolderViewModelとの連携を結合テストする
```

---

## Task 3-5: ホイールクリックでタブを閉じる

作業内容:

```text
- タブ上の中クリックを検出する
- 中クリック時にタブを閉じる
- 左クリックでは閉じない
- 閉じるボタンは表示しない
```

完了条件:

```text
- ホイールクリックでのみタブを閉じられる
```

テスト:

```text
- TabManagerServiceのCloseTabを単体テストする
- GUIテストで中クリックによるタブ削除を検証する
```

---

## Task 3-6: 閉じたタブの復元を実装

作業内容:

```text
- ClosedTab履歴を実装する
- タブを閉じると履歴へ積む
- Ctrl+Shift+Tで復元する
- 復元先は元のグループを優先する
```

完了条件:

```text
- 最後に閉じたタブを復元できる
```

テスト:

```text
- タブを閉じるとClosedTabsに追加される
- RestoreClosedTabで復元される
- 復元後にClosedTabsから削除される
```

---

# Step 4: 永続化と復元

目的:

```text
アプリ終了後もタブグループ、タブ、閉じたタブ履歴を復元できるようにする。
```

## Task 4-1: SettingsServiceを作成

作業内容:

```text
- ISettingsService を定義する
- SettingsService を実装する
- settings.json の保存先を定義する
- AppSettings モデルを作成する
```

完了条件:

```text
- 設定をJSONで保存できる
- JSONから設定を復元できる
```

テスト:

```text
- 一時フォルダにsettings.jsonを保存する
- 保存後に読み戻す
- 内容が一致することを検証する
```

---

## Task 4-2: セッション保存を実装

作業内容:

```text
- アプリ終了時に現在のタブ状態を保存する
- アプリ終了時にウィンドウサイズを保存する
- タブグループを保存する
- アクティブタブを保存する
- 閉じたタブ履歴を保存する
```

完了条件:

```text
- アプリ終了時にsettings.jsonが更新される
```

テスト:

```text
- AppSettings生成処理を単体テストする
- SettingsService保存処理を結合テストする
```

---

## Task 4-3: セッション復元を実装

作業内容:

```text
- アプリ起動時にsettings.jsonを読み込む
- アプリ起動時に前回のウィンドウサイズを復元する
- タブグループを復元する
- タブを復元する
- アクティブタブを復元する
- settings.jsonがない場合は既定状態で起動する
```

完了条件:

```text
- 前回終了時のタブ構成が復元される
- 前回終了時のウィンドウサイズが復元される
```

テスト:

```text
- settings.jsonありの場合の復元を結合テストする
- ウィンドウサイズ復元処理を単体テストまたは結合テストする
- settings.jsonなしの場合の既定起動を単体テストする
- 壊れたsettings.jsonの場合のフォールバックを単体テストする
```

---

## Task 4-4: グループ単位の保存・読込を実装

作業内容:

```text
- 現在のタブグループを名前付きで保存する
- 保存済みグループを一覧表示する
- 保存済みグループを開く
```

完了条件:

```text
- 作業A、作業Bなどのフォルダセットを再利用できる
```

テスト:

```text
- グループ保存の単体テスト
- グループ読込の単体テスト
- 存在しないパスを含む場合の動作を結合テストする
```

---

## Task 4-5: ファイル一覧の表示・操作性改善

作業内容:

```text
- ファイルとフォルダの違いをアイコンまたは文字色で区別する
- タイトル行クリックで列ごとの昇順・降順ソートを切り替える
- タイトル列の区切りダブルクリックで列幅を自動調整する
- 列幅の自動調整には最小幅と、ウィンドウ幅内に余裕を持って収まる最大幅を設ける
- アドレスバーの入力値がダブルクォート括りの場合、外側のダブルクォートを除去して移動する
```

完了条件:

```text
- ファイルとフォルダを視覚的に区別できる
- タイトル行クリックでソート順を切り替えられる
- 列幅を文字列数に応じて自動調整できる
- ダブルクォート括りで貼り付けられたパスへ移動できる
```

テスト:

```text
- ファイル/フォルダの表示区別に必要なViewModel状態を単体テストする
- 列ごとの昇順・降順ソートを単体テストする
- 列幅自動調整の最小幅・最大幅制御を単体テストする
- ダブルクォート括りのパス入力を単体テストする
```

---

# Step 5: GUI自動テストと仕上げ

目的:

```text
実際のGUI操作まで自動テストし、アプリとして安定させる。
```

## Task 5-1: UIテストプロジェクトを作成

作業内容:

```text
- TabNest.UiTests を作成する
- Appium.WebDriver を導入する
- WinAppDriver起動手順をREADMEに記載する
- テスト対象アプリの起動方法を定義する
```

完了条件:

```text
- UIテストプロジェクトがビルドできる
- 空のUIテストが実行できる
```

テスト:

```text
- dotnet test tests/TabNest.UiTests を実行して成功させる
```

---

## Task 5-2: AutomationIdを整備

作業内容:

```text
- 主要ボタンにAutomationIdを付与する
- タブ要素にAutomationIdを付与する
- グループ行にAutomationIdを付与する
- ファイル一覧にAutomationIdを付与する
```

対象例:

```text
- MainWindow
- AddTabButton
- AddGroupButton
- RestoreClosedTabButton
- FileListView
- PathTextBox
- TabGroupRow
- FolderTabItem
```

完了条件:

```text
- Inspect.exe または Accessibility Insights で要素を識別できる
```

テスト:

```text
- UIテストから主要要素を検索できる
```

---

## Task 5-3: 起動テストを作成

作業内容:

```text
- アプリを起動する
- MainWindowを検出する
- 初期タブまたは初期表示を検証する
- 起動時のウィンドウサイズ復元を検証する
```

完了条件:

```text
- UIテストでアプリ起動を検証できる
- UIテストでウィンドウサイズ復元を検証できる
```

テスト:

```text
- Launch_App_Should_Show_MainWindow
- Launch_App_Should_Restore_Window_Size
```

---

## Task 5-4: タブ操作UIテストを作成

作業内容:

```text
- タブ追加をGUI操作で検証する
- タブ選択をGUI操作で検証する
- ホイールクリックによるタブ閉じを検証する
- Ctrl+Shift+Tによる復元を検証する
```

完了条件:

```text
- 主要なタブ操作をGUIテストで検証できる
```

テスト:

```text
- AddTab_Should_Create_New_Tab
- MiddleClick_Tab_Should_Close_Tab
- CtrlShiftT_Should_Restore_Closed_Tab
```

---

## Task 5-5: タブグループUIテストを作成

作業内容:

```text
- グループ追加をGUI操作で検証する
- 複数グループ表示を検証する
- グループ内タブ表示を検証する
```

完了条件:

```text
- 複数段タブグループがGUIテストで確認できる
```

テスト:

```text
- AddGroup_Should_Create_New_Group_Row
- Group_Should_Display_Tabs_In_One_Row
```

---

## Task 5-6: ファイル一覧UIテストを作成

作業内容:

```text
- ファイルとフォルダの視覚的な区別を検証する
- タイトル行クリックで列ごとの昇順・降順が切り替わることを検証する
- タイトル列の区切りダブルクリックで列幅が文字列数に応じて調整されることを検証する
- 自動調整後の列幅が最小幅以上で、ウィンドウ幅内に余裕を持って収まることを検証する
- ダブルクォート括りのパスをアドレスバーへ貼り付けて移動できることを検証する
```

完了条件:

```text
- ファイル一覧の主要なGUI操作をUIテストで検証できる
```

テスト:

```text
- FileList_Should_Distinguish_Files_And_Folders
- FileList_HeaderClick_Should_Toggle_Sort_Direction
- FileList_ColumnDividerDoubleClick_Should_AutoFit_Within_Window
- PathTextBox_Should_Navigate_Quoted_Path
```

---

## Task 5-7: セッション復元UIテストを作成

作業内容:

```text
- テスト用settings.jsonを配置する
- アプリを起動する
- タブグループが復元されることを検証する
- アクティブタブが復元されることを検証する
- ウィンドウサイズが復元されることを検証する
```

完了条件:

```text
- アプリ再起動後の復元をGUIテストで検証できる
```

テスト:

```text
- App_Should_Restore_Previous_Session
- App_Should_Restore_Window_Size
```

---

## Task 5-8: README整備

作業内容:

```text
- 概要を書く
- スクリーンショット掲載場所を用意する
- ビルド手順を書く
- テスト手順を書く
- UIテスト手順を書く
- 既知の制限を書く
```

完了条件:

```text
- 第三者がREADMEを読んでビルド・テストできる
```

---

# Definition of Done

各タスクは以下を満たした場合に完了とする。

```text
- 実装が完了している
- 対応する単体テストまたは結合テストがある
- dotnet build が成功する
- dotnet test が成功する
- 仕様外の大きな変更をしていない
- READMEまたはSPECに必要な追記がある
```

GUI関連タスクは追加で以下を満たす。

```text
- 主要UI要素にAutomationIdが付与されている
- 可能な範囲でUI自動テストが追加されている
- 手動確認結果をREADMEまたはIssueに記録している
```

---

# AI作業時のルール

AIに実装を任せる場合、以下の単位で依頼する。

```text
- 1回の依頼では1タスクのみ実装する
- 仕様変更を勝手に行わない
- 既存テストが失敗した場合は実装を止めて原因を説明する
- 実装後に dotnet build と dotnet test を実行する
- 新規機能には必ずテストを追加する
- UI要素にはAutomationIdを付与する
```

AIへの依頼例:

```text
SPEC.md の Task 3-2 を実装してください。
既存仕様を変更せず、TabManagerServiceにタブ数20個、グループ数5個の上限制御を追加してください。
xUnitで単体テストを追加し、dotnet test が成功する状態にしてください。
```

---

# 初期リリース範囲

## v0.1

```text
- WinUI 3アプリとして起動
- フォルダ一覧表示
- フォルダ移動
- 戻る・進む
- タブ追加
- タブ選択
- ホイールクリックでタブを閉じる
- Ctrl+Shift+Tで閉じたタブを復元
- 最大5段のタブグループ表示
- 最大20タブ制限
- セッション保存・復元
- 単体テスト
- 結合テスト
- 最低限のGUI自動テスト
```

## v0.2以降

```text
- コピー
- 移動
- リネーム
- ごみ箱へ削除
- 右クリックメニュー
- お気に入りフォルダ
- グループテンプレート
- タブ並び替え
- グループ間タブ移動
```

## 開発補助

本プロジェクトでは、AIによる実装支援を前提とする。

推奨する開発補助環境は以下とする。

- GitHub Copilot
- Claude Code
- Codex CLI
- Windows Development Skills

Windows Development Skills は、WinUI 3 / Windows App SDK に関する実装支援の精度を高める目的で利用する。  
ただし、本プロジェクトのビルド、テスト、実行は Windows Development Skills に依存しない。
