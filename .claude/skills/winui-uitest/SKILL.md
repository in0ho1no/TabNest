---
name: winui-uitest
description: TabNest（WinUI 3 / Windows App SDK）の WinAppDriver + Appium UI テストを書く・実行する・デバッグするときに使う。AUMID と環境変数の罠、セッション確立方式、座標系変換、MenuFlyout/SendKeys の癖、AutomationId の UIA 可視化、settings.json の仮想化など、ハマりどころの手順を含む。GUI 自動テストや GUI 評価を行う作業で参照する。
---

# WinUI 3 UI テスト（WinAppDriver + Appium）手順とハマりどころ

dev-re トラックの Task 5-1〜5-7 で確立した知見。UI テストを書く・直す・実機評価するときの正本。

## 前提
- 開発者モードを有効にする（設定 → システム → 開発者向け）。
- WinAppDriver は標準パス `C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe`。
  **管理者起動が必要な場合がある**（非管理者でも動くことはあるが活性化が不安定なケースを確認）。
  起動後 `http://127.0.0.1:4723/status` が 200 を返すか、TCP 4723 が listening かで確認。
- 依存: `Appium.WebDriver 4.3.1`（WinAppDriver 1.2 は旧 JSON Wire Protocol のみ。**W3C 専用の 5.x は不可**）
  ＋ `System.Drawing.Common 8.0.0`（Appium 推移的依存の脆弱性 GHSA-rxg9-xrhp-64gj を上書き）。

## ⚠️ AUMID と環境変数の罠（最重要・半日溶かした原因）
- dev-re の AUMID: `893E2482-2E47-47E9-A41F-73DAA6BE7D3A_1z32rh13vfry6!App`
  （`src/TabNest.App/Package.appxmanifest` の Identity から決まる）。`Get-StartApps` で確認できる。
- **WinAppDriver は無効な AUMID の活性化でエラーを返さず 120 秒ハングする**
  （「`...AppX\TabNest.App.exe` パラメーターが間違っています」ダイアログが出る）。
- 過去トラックが**ユーザー環境変数 `TABNEST_UI_TEST_APP_ID` に削除済みパッケージの AUMID を残していた**のが
  ハングの真因だった。レジストリからは削除済みでも、親プロセスが古い値を保持することがある。
  → **`dotnet test` の前に必ず `Remove-Item Env:TABNEST_UI_TEST_APP_ID -ErrorAction SilentlyContinue`**。
  恒久的に残っていないかは `[Environment]::GetEnvironmentVariable("TABNEST_UI_TEST_APP_ID","User")` で確認・削除。

## セッション確立はアタッチ方式（app capability は使わない）
- `app` capability に AUMID を渡す起動は、無効 AUMID で 120 秒ハングし切り分け不能。
- 採用方式: **`shell:AppsFolder\<AUMID>` でシェル活性化 → 新規プロセスのウィンドウ表示を待つ →
  `appTopLevelWindow` capability にウィンドウハンドル（hex）を渡してアタッチ**。
  起動失敗が明確な `TimeoutException` になる。実装は `tests/TabNest.UiTests/Infrastructure/AppSession.cs`。

## 直列実行は必須
- `[assembly: CollectionBehavior(DisableTestParallelization = true)]`（`AssemblyInfo.cs`）。
  並列だと WinAppDriver への同時セッション要求が競合し、タイムアウト/「パラメーター違い」になる。

## 座標系：要素座標は DWM 可視矩形からの相対値
- WinAppDriver の要素 `Location` はデスクトップ絶対座標ではなく、**ウィンドウの可視矩形
  （見えないリサイズ枠を除く = DWM 拡張フレーム境界）からの相対値**（実測: WAD 112,48 ↔ UIA 絶対 275,204、
  ウィンドウ原点 156,156、不可視枠 7px）。
- 物理クリック（中/右/ダブルクリックは WinAppDriver に API が無いので Win32 で送る）の座標は
  `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` を起点に絶対座標へ変換する。
  実装は `tests/TabNest.UiTests/Infrastructure/NativeMethods.cs` / `UiActions.cs`。

## ウィンドウサイズ検証は GetWindowRect
- WinAppDriver の `Window.Size` は不可視枠を除くため `AppWindow.Size/Resize`（物理px外接矩形）と **14px ずれる**。
- サイズ復元の検証は **`GetWindowRect`（物理px外接矩形）** を使う。AppWindow と同座標系で厳密一致する。

## MenuFlyout / コンテキストメニュー
- MenuFlyout は**別 HWND のポップアップ**として表示され、`appTopLevelWindow` でアタッチした
  セッションの要素ツリーに現れない。
- → 対象を**右クリック（Win32）してから ↓ + Enter** で先頭メニュー項目を実行する。
  項目が増えたらこの前提を見直すこと。

## SendKeys の文字化け（JIS 配列）
- JIS 配列では SendKeys の `\` が `]` に化ける → **パスは `/` 区切りで送る**（Windows API は等価に解釈）。
- 記号（`"` など）も化ける → **Win32 クリップボード（CF_UNICODETEXT）に設定して Ctrl+V 貼り付け**で入力。

## AutomationId を UIA に出す（最頻出の落とし穴）
- **Shape（Rectangle 等）/ Grid / Border / Panel / UserControl は AutomationPeer を持たず、
  付与した AutomationId が UIA ツリーに現れない**。検索・操作できない。
- 対処:
  - 操作要素は AutomationPeer を持つ型（`Button` / `TextBlock` / `ListView` / `TextBox`）に AutomationId を付ける。
    透明クリック領域が要るなら Rectangle ではなく**透明テンプレートの Button**にする。
  - `UserControl` は `OnCreateAutomationPeer()` で `FrameworkElementAutomationPeer` を返す。
  - `TreeView` 本体はピアなし。内部の `TreeViewList`（UIA 上は Tree）へ `Loaded` 時に AutomationId を引き継ぐ。
  - リスト項目の AutomationId は DataTemplate 内の Grid ではなく **`ItemContainerStyle`（ListViewItem）** に付与する。

## settings.json の仮想化
- パッケージアプリのため `%AppData%\TabNest\settings.json` は
  **`%LocalAppData%\Packages\<PFN>\LocalCache\Roaming\TabNest\settings.json`** に仮想化される。
- 起動状態を固定するテストは `tests/TabNest.UiTests/Infrastructure/SettingsFileScope.cs` で
  既存ファイルを退避 → テスト用 JSON 配置 → Dispose で復元（ユーザーの実データを壊さない）。

## 実行
- ビルド更新後は **フル `dotnet run`** でパッケージを再登録する（`--no-build` は AppX レイアウト未更新で
  新しい XAML/AutomationId が反映されない）。
- WinAppDriver 未起動時は `UiFact` 属性（TCP プローブ）が自動スキップ → `dotnet test TabNest.slnx` は
  WinAppDriver 無しでも成功する（CI も同様）。
- フル実行コマンド: `Remove-Item Env:TABNEST_UI_TEST_APP_ID -ErrorAction SilentlyContinue; dotnet test tests/TabNest.UiTests/TabNest.UiTests.csproj`

## サンドボックスポリシー（SPEC「Level 3」）
- ファイル削除・コピー・移動は GUI テストの対象から原則除外。
- ナビゲーションで参照する実フォルダは `tests/TabNest.UiTests/TestFixtures/` 以下のみ。
- アプリ自身の settings.json の退避/差し替え/復元はテストセットアップとして可（SettingsFileScope）。
