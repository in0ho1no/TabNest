# Task6-1 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8
- レビュー対象: `git diff dev-re...Task6-1`
- 実施日: 2026-06-13

## 対象範囲

SPEC.md「Task 6-1: タブグループの削除」（Step 6）および関連節「主要機能 > タブグループ」。

差分ファイル:
- `src/TabNest.App/Controls/TabGroupRow.xaml` / `.xaml.cs`（メニュー項目・確認ダイアログ）
- `src/TabNest.ViewModels/MainViewModel.cs`（`RemoveGroup(groupId)` 追加・ファクトリ配線）
- `src/TabNest.ViewModels/TabGroupViewModel.cs`（`RemoveGroup()` 委譲・`HasTabs`）
- `tests/TabNest.ViewModels.Tests/GroupRemoveTests.cs`（単体テスト6件・新規）
- `tests/TabNest.UiTests/Tests/GroupOperationTests.cs`（UIテスト1件追加・既存1件改修）
- `tests/TabNest.UiTests/Infrastructure/UiActions.cs`（MenuFlyout 操作を Root セッション方式へ）

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 情報 | SPEC は「隣接グループを新しいアクティブにする」と記載。サービス `RemoveGroup` のフォールバックは常に `_groups[0]`（先頭）で、厳密な「隣接」ではない。ただし当該サービスは v0.1 実装済みで本タスクの差分外。本タスク（ViewModel/UI）は「最後の1グループ拒否・アクティブ削除時に別グループへフォーカス移動」という完了条件を満たすため、機能上の問題はない | （未対応） |
| 2 | 情報 | タブ有の「最後の1グループ」を削除しようとすると、先に確認ダイアログを出し「削除」押下後にサービスが下限拒否する（ダイアログ→拒否の二段）。UX としてはダイアログ前に拒否したほうが自然だが、SPEC は確認ダイアログの提示条件を「タブを1個以上持つグループ」と規定しており、下限拒否を ViewModel 側で `OperationError` 提示する現挙動は仕様の範囲内。`RemoveGroup` 単体テストで「下限拒否時に状態が壊れない」ことは検証済み | （未対応） |
| 3 | 情報 | 確認ダイアログ Content の件数表示 `ViewModel.Tabs.Count` は View 層で `TabGroupViewModel` のプロパティを参照しており、層分離は保たれている。問題なし | （未対応） |

Critical / Major / Minor の指摘なし。

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件
- `TabManagerService.RemoveGroup(groupId)`：v0.1 実装済みを利用（下限1拒否 `LastGroupProtected`・アクティブグループ削除時のフォールバック・グループ未存在時 `GroupNotFound`）。本タスクで重複実装していない。
- グループ名右クリックメニュー（MenuFlyout）に「グループを削除」を追加。`AutomationProperties.AutomationId="RemoveGroupMenuItem"` 付与済み。
- タブを1個以上持つグループの削除時に確認 `ContentDialog` を表示（`HasTabs` で判定）。`AutomationProperties.SetAutomationId(dialog, "RemoveGroupConfirmDialog")` 付与済み。
- 削除タブを ClosedTab 履歴へ積まない：サービスの `RemoveGroup` は `CloseTab` 経路を通らずグループごと除去するため履歴非積み。単体テスト `RemoveGroup_削除したグループのタブはClosedTab履歴へ積まれない` で `RestoreClosedTab()==false` を検証。SPEC 既定・ユーザー確認済みと一致。
- 完了条件3点（右クリック削除・最後の1グループ削除拒否で状態不変・アクティブグループ削除時のフォーカス移動）を、ViewModel 単体テストおよび UI テストで充足。

### 状態整合性
- `MainViewModel.RemoveGroup`：サービス成功後に対応 `TabGroupViewModel` を `Groups` から除去 → `ApplyActiveStates()` で IsActive を再計算 → アクティブグループ削除時のみ新アクティブタブの履歴接続・フォルダ表示。既存 `CloseTab` と同型のパターンで一貫。
- 失敗時（下限拒否・未存在）は `OperationError` を設定して early return、`Groups` は不変。テスト `最後の1グループは削除できずエラーになる` / `存在しないグループの削除はエラーになる` で状態不変を検証。
- 非アクティブグループ削除時に表示・アクティブタブが変わらないことを `非アクティブグループを削除しても表示は変わらない` で検証（`wasActiveGroup` フラグで分岐）。
- アクティブグループ削除時に別グループのアクティブタブのフォルダ内容へ追従することを `アクティブグループを削除すると別グループの内容が表示される` で検証。

### MVVM 層分離
- `MainViewModel` / `TabGroupViewModel`（ViewModels）・サービス（Core）は WinUI 非依存を維持。確認ダイアログ（`ContentDialog`）は View 層 `TabGroupRow.xaml.cs` 内に限定。SPEC の指定どおり「確認ダイアログは View 側で表示してから ViewModel を呼ぶ」設計。
- テストは `TabNest.ViewModels.Tests → TabNest.ViewModels` 参照で、`TabNest.App` を参照していない。

### テストの実効性
- 単体6件は通常削除・下限拒否・アクティブ削除時の追従・非アクティブ削除時の不変・履歴非積み・未存在エラーを網羅し、`Groups` 件数・IsActive・`Folder.CurrentPath`・`OperationError`・`RestoreClosedTab` の実値を検証。形だけのアサートではない。
- UI テスト `RemoveGroup_With_Tabs_Should_Confirm_And_Delete` は Ctrl+G で2段にし、2段目を右クリック→`RemoveGroupMenuItem` 実行→`RemoveGroupConfirmDialog` の `PrimaryButton` クリック→グループ数1・残存名「作業1」を検証。確認ダイアログ経由の実削除を検証している。

### AutomationId
- `RemoveGroupMenuItem`・`RemoveGroupConfirmDialog` を付与。付与漏れなし。

### UI テスト基盤の Root セッション方式変更
- `InvokeFirstContextMenuItem`（↓+Enter のキーボード操作・先頭項目前提）を `InvokeContextMenuItem(session, element, menuItemAutomationId)` に置換。MenuFlyout が別 HWND ポップアップで appTopLevelWindow セッションのツリーに現れない問題に対し、短命のデスクトップ Root セッションを `using` で生成して AutomationId 検索→クリックする方式は妥当。
- Root セッションは `using` で確実に Dispose されリークなし。既存呼び出し箇所（`Favorite_Save_And_Open_Should_Restore_Group`）は `SaveToFavoritesMenuItem` 指定へ更新済みで、項目並び順への依存（先頭項目前提）が解消されデグレなし。`InvokeFirstContextMenuItem` の残存参照なし。

## 結論

**approve（マージ可）。** Critical / Major / Minor の指摘なし（情報のみ3件）。
SPEC Task 6-1 の作業内容・完了条件・指定テストをすべて満たし、状態整合性・層分離・AutomationId 付与・UIテスト基盤変更の妥当性に問題は認められない。
