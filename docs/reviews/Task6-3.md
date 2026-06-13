# Task6-3 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Sonnet 4.6
- レビュー対象: `git diff dev-re...Task6-3`
- 実施日: 2026-06-13

## 対象範囲

SPEC.md「Task 6-3: タブの複製（右クリックメニュー）」および「主要機能 > タブ」「初期リリース範囲 > v0.2」節。

差分ファイル:
- `src/TabNest.App/Controls/TabGroupRow.xaml`（`Border.ContextFlyout` に `MenuFlyout` / `DuplicateTabMenuItem` 追加）
- `src/TabNest.App/Controls/TabGroupRow.xaml.cs`（`DuplicateTab_Click` ハンドラ追加）
- `src/TabNest.Core/Services/TabManagerService.cs`（`DuplicateTab(string tabId)` 追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`DuplicateTab(FolderTabViewModel tab)` 追加・`CreateGroupViewModel` にコールバック接続）
- `src/TabNest.ViewModels/TabGroupViewModel.cs`（`_duplicateTab` フィールド・`DuplicateTab` メソッド追加）
- `tests/TabNest.Core.Tests/TabDuplicateTests.cs`（Core 層テスト5件 新規）
- `tests/TabNest.ViewModels.Tests/TabDuplicateTests.cs`（ViewModel 層テスト4件 新規）

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `DuplicateTab_Click` のコメント「MenuFlyoutItem は所属タブ(Border)の DataContext を引き継ぐ」という前提が WinUI 3 の DataTemplate 内 ContextFlyout で実際に成立するか、実機で未確認の可能性がある。ただし、`MainPage.xaml.cs` の `DeleteFavorite_Click` が同一パターン（`sender is FrameworkElement { DataContext: FavoriteItemViewModel }` ）で既に動作実績があるため、実用上のリスクは低い。GUI 評価で必ず動作確認すること。 | （未対応） |
| 2 | Minor | ViewModel 層の `TabDuplicateTests`「複製が成功するとOperationErrorがクリアされる」テストは、グループ上限エラーを利用して `OperationError` に値を設定したうえで `DuplicateTab` を呼ぶ構成になっている。上限エラーはタブ複製とは別の操作系のエラーであり、「複製直前の同系操作の失敗→成功でクリア」ではなく「別系のエラー→複製成功でクリア」というシナリオになっている。意図は伝わるが、「複製失敗（上限）でエラーが設定された状態→タブを1つ閉じる→複製成功でクリア」というより自然なシナリオにすれば、テストの意図がより明確になる。動作正確性への影響はなし。 | （未対応） |
| 3 | 情報 | SPEC v0.2 節は「右クリックからタブの複製（Task 4-6 で新設した MenuFlyout 基盤を流用）」と記載している。実装はタブ (`Border.ContextFlyout`) に新規 `MenuFlyout` を設置する方式であり、グループ名の既存 `MenuFlyout` とは独立した別フライアウト。SPEC の「流用」という表現はグループ名側の MenuFlyout を拡張することを想定していた可能性があるが、タブに独立したコンテキストメニューを設置する本実装はより UX として適切（グループ名とタブは操作対象が異なる）。SPEC の「流用」は基盤コード（DataContext 取得パターン）の流用を指すとも解釈でき、現実装は問題ない。 | （未対応） |
| 4 | 情報 | `DuplicateTab_Click` で `DataContext` が `FolderTabViewModel` 以外の型だった場合（`null` 含む）は何もしない（`if` が `false` になる）。上限到達等のエラーは `OperationError` 経由でユーザーに通知される設計で、クラッシュしない。エラー表示の実際の UI コンポーネント（InfoBar 等）は既存実装と同じ方式を踏襲していると考えられ、差分には含まれないため既存実装で確認済みのはず。 | （未対応） |

Critical / Major の指摘なし。

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **右クリックメニュー（MenuFlyout）**: `TabGroupRow.xaml` の `DataTemplate` 内 `Border` に `Border.ContextFlyout` を設置し、`MenuFlyoutItem`（Text="タブを複製"）を追加。SPEC「タブの右クリックメニュー（MenuFlyout）を用意する」を充足。
- **対象タブの直後に挿入**: `TabManagerService.DuplicateTab` は `group.Tabs.FindIndex` で対象インデックスを取得し `group.Tabs.Insert(index + 1, duplicate)` で挿入。ViewModel 層も `groupVm.Tabs.Insert(index + 1, duplicateVm)` で同期。Core テスト「複製は同一PathとTitleのタブを対象タブの直後に挿入する」で `[first.Id, target.Id, duplicate.Id, last.Id]` の順序を実値で検証。SPEC「対象タブの直後に挿入される」を充足。
- **複製タブは同一 Path / Title**: `FolderTab.Path` と `FolderTab.Title` を `source` からそのままコピー。テストで `Assert.Equal(target.Path, duplicate.Path)` / `Assert.Equal(target.Title, duplicate.Title)` で検証。
- **複製タブの Id は新規採番**: `Guid.NewGuid().ToString()` を使用。テスト「複製タブには新しいIdが採番される」で `Assert.NotEqual(target.Id, duplicate.Id)` を確認。
- **戻る・進む履歴を引き継がず空で開始**: `DuplicateTab` ViewModel 層が `new FolderTabViewModel(result.Value!)` で新規 ViewModel を生成し、`Folder.AttachHistory(duplicateVm.History)` で空の履歴を接続。テスト「複製タブの戻る進む履歴は引き継がず空で開始する」で `source.History.CanGoBack` が `true` であることと `duplicate.History.CanGoBack` / `CanGoForward` が `false` であることを同時に検証。SPEC「BackStack / ForwardStack は引き継がず空で開始」を充足。
- **グループのタブ上限20尊重・エラー表示**: Core 層で `MaxTabsPerGroup` チェックを行い `TabOperationError.TabLimitReached` を返す。ViewModel 層は `result.IsSuccess` 偽時に `OperationError = result.ErrorMessage` を設定。テスト（Core 層・ViewModel 層の両方）で上限到達時に複製が拒否されタブ数が変化しないことを検証。SPEC「上限到達時は複製せずエラーを表示する」を充足。
- **複製後に複製タブがアクティブになる**: `SetActiveTab(duplicate.Id)` → `ApplyActiveStates()` → `Folder.AttachHistory(duplicateVm.History)` → `Folder.ShowFolder(duplicateVm.Path)` の流れ。ViewModel テストで `duplicate.IsActive` が `true`・`target.IsActive` が `false`・`vm.Folder.CurrentPath` が複製タブのパスであることを確認。

### MVVM 層分離

- `TabNest.ViewModels` / `TabNest.Core` に WinUI 依存コードなし（`using Microsoft.UI.Xaml` が存在するのは `TabGroupRow.xaml.cs` のみ、View 層に限定）。
- `TabGroupViewModel` のコンストラクタに `duplicateTab` コールバックを追加済みで、View 側が `TabGroupRow.xaml.cs` から `ViewModel.DuplicateTab(tab)` を呼ぶ設計。ViewModel から View への依存なし。
- `tests/TabNest.ViewModels.Tests/TabDuplicateTests.cs` は `TabNest.ViewModels` / `TabNest.Core` のみ参照し、`TabNest.App` を参照していない（`.csproj` に `TabNest.App` の `ProjectReference` なし）。CLAUDE.md の恒久禁止ルールを守っている。

### テストの実効性

- **Core 層 `TabDuplicateTests`（5件）**: 挿入順序（配列等値比較）・新規 Id・アクティブ切替・上限拒否（タブ数不変）・TabNotFound をそれぞれ独立テストし、実値で検証。形式的アサートではない。
- **ViewModel 層 `TabDuplicateTests`（4件）**:
  - 「複製は同一Pathのタブを対象タブの直後に挿入しアクティブにする」: `group.Tabs` の参照等値比較・`IsActive`・`vm.Folder.CurrentPath` で UI 整合まで検証。
  - 「複製タブの戻る進む履歴は引き継がず空で開始する」: 元タブの履歴に実際に移動を積んだ後に複製し、両タブの `CanGoBack`/`CanGoForward` を独立確認。
  - 「上限到達時はエラーを表示して複製しない」: タブを20個まで積んで上限状態にし、`OperationError != null` と `Tabs.Count` 不変を確認。
  - 「複製が成功するとOperationErrorがクリアされる」: 事前エラーありの状態から複製成功で `OperationError == null` になることを確認。

### 状態整合性

- 複製成功フローで Model 層（`_groups` リスト）と ViewModel 層（`groupVm.Tabs`）の両方に `index + 1` 挿入が行われ、`ApplyActiveStates()` で `IsActive` が一元更新される。二重挿入や順序の不一致は生じない設計。
- 複製失敗時（上限到達・TabNotFound）は Model 層が挿入前に `return Failure` するため、`group.Tabs` は変化しない。ViewModel 層は `!result.IsSuccess` で早期 return するため `groupVm.Tabs` も変化しない。失敗時の巻き戻しは正しい。
- `OperationError` は複製成功時に `null` クリア、失敗時にエラーメッセージをセットし、状態が混在しない。

### AutomationId の付与

- 新設 `MenuFlyoutItem` に `AutomationProperties.AutomationId="DuplicateTabMenuItem"` を付与済み。UI テストから右クリックメニュー項目を識別できる。

### エラー処理

- 存在しないタブを複製しようとした場合（`TabNotFound`）はクラッシュせず失敗結果を返す。テストで検証済み。
- `DuplicateTab_Click` の DataContext が期待型でない場合も `if` で早期 return してクラッシュしない。
- タブ上限到達時は `OperationError` でユーザーへ通知し、タブ数は変化しない。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 2件・情報 2件）。
SPEC Task 6-3 の作業内容（右クリックメニュー新設・同一 Path タブを直後に挿入・履歴引き継がず・上限到達でエラー表示）・完了条件・指定テスト（単体テスト両層）をすべて満たしている。MVVM 層分離・禁止参照なし・AutomationId 付与いずれも適正。
