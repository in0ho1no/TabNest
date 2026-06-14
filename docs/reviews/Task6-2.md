# Task6-2 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Sonnet 4.6
- レビュー対象: `git diff dev-re...Task6-2`
- 実施日: 2026-06-13

## 対象範囲

SPEC.md「Task 6-2: ショートカット拡充（Ctrl+W・Alt+左/右/上）」および「ショートカットキー」節。

差分ファイル:
- `src/TabNest.App/MainWindow.xaml`（KeyboardAccelerator 4件追加）
- `src/TabNest.App/MainWindow.xaml.cs`（Accelerator ハンドラ4件追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`CloseActiveTab` / `NavigateBack` / `NavigateForward` / `NavigateUp` / `InvokeFolderNavigation` 追加）
- `tests/TabNest.UiTests/Tests/NavigationShortcutUiTests.cs`（UIテスト新規）
- `tests/TabNest.UiTests/Tests/TabOperationTests.cs`（Ctrl+W UIテスト追加）
- `tests/TabNest.ViewModels.Tests/ShortcutCloseActiveTabTests.cs`（単体テスト新規）
- `tests/TabNest.ViewModels.Tests/ShortcutNavigationTests.cs`（単体テスト新規）

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `ShortcutNavigationTests` の全テストは `vm.Folder.LoadFolder()` で直接 FolderViewModel を操作して履歴を積んでいる。これは MainViewModel の正規フロー（`SelectTab` 経由で `Folder.AttachHistory` が呼ばれたうえでのアクティブタブへの移動）ではなく、FolderViewModel のプロパティを単独でセットアップしているに過ぎない。テスト自体は `NavigateBack`/`NavigateForward`/`NavigateUp` の委譲先コマンドの canExecute チェックを確認しており、実装の意図を検証しているが、タブとの関係（アクティブタブの履歴が連動するか）は未検証。ただし既存テストが `FolderViewModel` の挙動を網羅しており、`InvokeFolderNavigation` はコマンド委譲のみで付加ロジックが薄いため、実用上のリスクは低い。 | （未対応） |
| 2 | Minor | `CtrlW_Should_Close_Active_Tab_And_Restore_With_CtrlShiftT` UIテストにて、タブを閉じた後（タブ数1に戻った後）の `addressBar` 要素の参照が stale 要素になるリスクがある。`FindElementByAccessibilityId("PathTextBox")` はタブ閉鎖後に再取得していないため、WinAppDriver が stale element 例外を投げる環境では `WaitUntil` の条件評価でクラッシュする可能性がある。既存の他テストでも同様のパターンが使われている場合は全体への波及はないが、実行時に不安定になるケースがあれば再取得が必要。 | （未対応） |
| 3 | 情報 | `CloseActiveTab` は `IsRenameInProgress` を確認してから `CloseTab` へ委譲する。SPEC「ショートカットキー」節では Ctrl+W への編集中抑制の記載がなく、本タスク仕様「グループ名インライン編集中・アドレスバー編集中の扱いは既存の「ショートカットキー」節のルールに準拠する」という指示を拡大解釈して Ctrl+W にも適用している。拡大適用は妥当（タブを意図せず閉じる誤操作を防ぐ）であり、単体テストでも検証済み。仕様外の追加動作ではあるが問題なし。 | （未対応） |
| 4 | 情報 | SPEC「ショートカットキー」節は「アドレスバーにフォーカスがある場合は、そのままショートカット動作を実行する」と規定している。`InvokeFolderNavigation` のコメントに「アドレスバー編集中の扱い」が言及されているが、実装はアドレスバーのフォーカス状態を確認していない（WinUI の KeyboardAccelerator が自動的にフォーカスを跨いで発火する設計に依存）。これは Ctrl+T・Ctrl+G・Ctrl+Shift+T と同様の方式で一貫しており、問題なし。 | （未対応） |
| 5 | 情報 | `NavigateBack`/`NavigateForward`/`NavigateUp` は SPEC 指定の `KeyboardAccelerator` 方式ではなく、XAML の `KeyboardAccelerator` が既存の `BackCommand` / `ForwardCommand` / `NavigateUpCommand` に直接バインドするのではなく、新設の ViewModel メソッドをコードビハインドから呼ぶ設計になっている。これは Alt 系ショートカットに対してもグループ名編集中の抑制（`IsRenameInProgress` チェック）を挿入するためであり、SPEC の「KeyboardAccelerator はウィンドウのルート要素に設定する」「グループ名インライン編集中の抑制はショートカットキー節のルールに準拠」を同時に満たす合理的実装。問題なし。 | （未対応） |

Critical / Major の指摘なし。

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **Ctrl+W**: XAML `Grid.KeyboardAccelerators` にルート要素として追加。ハンドラは `CloseActiveTab()` を呼び `args.Handled = true`。`CloseActiveTab` は `IsRenameInProgress` を確認してから `CloseTab(activeVm)` へ委譲し、中クリック（ホイールクリック）と同一の `CloseTab` 経路を通る。`CloseTab` 内で `TabManagerService.CloseTab` が ClosedTab 履歴へ積む設計（`TabManagerService.CloseTab` が `_closedTabs.Add` する既存実装を利用）。単体テスト `CloseActiveTab_アクティブタブが閉じてClosedTabsへ積まれ復元できる` と `CloseActiveTab_中クリックと同じ経路でClosedTabsに記録される` で ClosedTabs への積み付きと `RestoreClosedTab` による復元を検証。完了条件「Ctrl+W でアクティブタブが閉じ、Ctrl+Shift+T で復元できる」を充足。

- **Alt+左/右/上**: XAML に `Modifiers="Menu"` で `Key="Left"/"Right"/"Up"` を追加。`InvokeFolderNavigation` は `command.CanExecute(null)` を確認してから `command.Execute(null)` する設計で、コマンド不可時に何もしない（状態を壊さない）。単体テスト `NavigateBack_戻れない状態では何もしない` / `NavigateForward_進めない状態では何もしない` / `NavigateUp_ドライブルートでは移動しない` で完了条件「戻る・進む不可の状態で Alt+左/右 を押しても状態が壊れない」を検証。

- **KeyboardAccelerator のルート要素設置**: 既存 Ctrl+T・Ctrl+G・Ctrl+Shift+T と同じ `<Grid.KeyboardAccelerators>` に追加。SPEC 指示「KeyboardAccelerator はウィンドウのルート要素に設定する」を遵守。

- **グループ名編集中の抑制**: `CloseActiveTab` と `InvokeFolderNavigation` いずれも `if (IsRenameInProgress) return false`。単体テスト `CloseActiveTab_グループ名編集中は何も実行しない` と `NavigateBack_グループ名編集中は何もしない` で検証済み。編集状態が維持されることも `Assert.True(vm.Groups[0].IsEditingName)` で確認。

### 状態整合性

- 最後のタブを Ctrl+W で閉じた場合、`_tabManager.CloseTab` が成功（`TabManagerService.CloseTab` は最後のタブでも削除可）→ `groupVm.Tabs.Remove(tab)` → `ApplyActiveStates()` で `ActiveTabId` が null になる → `wasActive && newActiveId is string` が偽 → `Folder.AttachHistory`/`ShowFolder` は呼ばれない。単体テスト `CloseActiveTab_最後のタブを閉じてもクラッシュしない` でタブ数0・グループ数1を検証済み。
- 戻る・進む不可時に Alt+左/右 を押しても `!command.CanExecute(null)` で早期 return するため、`History` / `CurrentPath` は変化しない。テストで `CurrentPath` の不変を確認。

### MVVM 層分離

- `MainViewModel`・`FolderViewModel` は `TabNest.ViewModels`（WinUI 非依存）に存在。`InvokeFolderNavigation` が受け取る `RelayCommand` は `TabNest.ViewModels` 内の型。コードビハインド（`MainWindow.xaml.cs`）は WinUI 型（`KeyboardAccelerator`・`KeyboardAcceleratorInvokedEventArgs`）を使用するが、View 層に限定されており ViewModel に WinUI 依存は波及していない。
- `TabNest.ViewModels.Tests.csproj` は `TabNest.ViewModels` のみ参照し、`TabNest.App` を参照していない。`TabNest.UiTests.csproj` は `TabNest.Core` のみ参照し、`TabNest.App` を参照していない。CLAUDE.md・AGENTS.md の恒久禁止ルールを守っている。

### テストの実効性

- 単体テスト `ShortcutCloseActiveTabTests`（4件）: タブ除去・ClosedTabs 記録・復元・最後のタブ・編集中抑制をそれぞれ独立テストし、`Groups[0].Tabs.Count`・`RestoreClosedTab()`・`CreateAppSettings.ClosedTabs`・`IsEditingName` の実値を検証。形式的アサートではない。
- 単体テスト `ShortcutNavigationTests`（7件）: 戻る/進む/上へのできる状態・できない状態・グループ名編集中を網羅し、`CurrentPath` の実値で遷移の有無を確認。
- UIテスト `CtrlW_Should_Close_Active_Tab_And_Restore_With_CtrlShiftT`: タブ追加→パス移動→Ctrl+W→タブ数1確認→Ctrl+Shift+T→タブ数2確認→アドレスバー値検証という実操作フロー。実機動作の E2E 検証として機能。
- UIテスト `AltArrows_Should_Navigate_Back_Forward_Up`: SampleFolder→SubFolder 移動後に Alt+左/右/上 の各動作とタブタイトル変化を検証。アドレスバーフォーカス対策として `UiActions.FindTabs(session)[0].Click()` でコンテンツ側へフォーカスを移してから送信するコメントつき注記あり（Alt+矢印のみ WinAppDriver で起きる固有問題への対処）。

### AutomationId

- 本タスクは KeyboardAccelerator とショートカット動作の追加のみ。新規コントロール・メニュー項目は増えていないため AutomationId 付与対象なし。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 2件・情報 3件）。
SPEC Task 6-2 の作業内容（Ctrl+W・Alt+左/右/上 の追加）・完了条件（ClosedTabs 積み・Alt 系コマンド可否遵守・状態不変）・指定テスト（単体+UIテスト）をすべて満たしている。MVVM 層分離・禁止参照なし・KeyboardAccelerator のルート要素設置いずれも適正。

## 呼び出し元（実装モデル Opus 4.8）の対応判断

Major 以上の指摘なしのため、修正は行わない。Minor 2件はいずれも現状維持とする。

- **Minor 1（未対応・現状維持）**: `ShortcutNavigationTests` の履歴セットアップが `Folder.LoadFolder` 直接操作である件。
  `InvokeFolderNavigation` は既存コマンドへの委譲（`CanExecute` ガード＋`Execute`）のみで付加ロジックを持たず、
  アクティブタブと履歴の連動は既存の `TabSelection`/`FolderHistory` 系テストが網羅済み。本タスクの検証対象
  （コマンド可否遵守・編集中抑制）は満たしているため、二重検証は追加しない。
- **Minor 2（未対応・現状維持）**: Ctrl+W UI テストの `addressBar` 要素 stale 懸念。既存の
  `CtrlShiftT_Should_Restore_Closed_Tab`（green）が同一パターン（閉じる前に取得した `addressBar` を閉鎖後に参照）
  を採用しており、実行環境でも安定して green。既存テストと挙動・リスクが同一のため、再取得への変更は見送る。
