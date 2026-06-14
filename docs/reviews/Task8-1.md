# Task8-1 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8（別系統セッション・クロスモデルレビュア）
- レビュー対象: `git diff dev-re...Task8-1`（1コミット `000b844`）
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 8-1: タブグループ名のインライン編集修正と『名前の変更』メニュー」（行 2108）／「初期リリース範囲 > v0.3」

差分ファイル:
- `src/TabNest.App/Controls/TabGroupRow.xaml`（グループ名 Grid の `CanDrag` を編集中に false へ切替える x:Bind に変更・MenuFlyout に「名前の変更」項目を追加）
- `src/TabNest.App/Controls/TabGroupRow.xaml.cs`（`CanDragGroupName` ヘルパ追加・`BeginInlineRename` 共通メソッド抽出・`RenameGroup_Click` 追加）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 情報 | 本 Task の修正は View 層（XAML/コードビハインド）に閉じており新規単体テストは追加されていない。SPEC テスト要件「編集確定で Name 更新・キャンセルで元名維持を単体テスト」は既存 `TabGroupViewModelTests.cs`（BeginRename/CommitRename/CancelRename/空白維持/前後空白除去）で既に充足済みで、ViewModel ロジックに変更がないため追加テスト不要。不具合の主因（CanDrag によるフォーカス喪失）は WinUI ランタイム挙動でユニットテスト不能、SPEC も当該検証は「GUI評価」に委ねている。妥当。 | |
| 2 | 情報 | `CanDragGroupName(bool) => !isEditingName` は単純反転で `VisibleWhenNot` と実質同義だが、用途（CanDrag への bool 供給）と意図（編集中はドラッグ無効）が命名・XMLドキュメントコメントで明示されており可読性は高い。OneWay バインドかつ `IsEditingName` が `SetProperty` で PropertyChanged を発火するため、編集開始/終了で CanDrag が反応的に更新される。設計妥当。 | |
| 3 | 情報 | `RenameGroup_Click` は `e.Handled` を設定しない（`BeginInlineRename` のみ呼ぶ）。MenuFlyoutItem の Click はフライアウトを閉じる以外の不要なバブリングを伴わないため問題なし。`GroupNameText_DoubleTapped` 側は従来どおり `e.Handled=true` を維持しており、共通化で挙動が変わっていない。 | |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **ダブルクリックで編集状態を維持しキーボード入力できる（完了条件1）**: 不具合の主因を「Task 7-3 で親 Grid に付けた `CanDrag="True"` が TextBox のポインタ入力をドラッグに奪い、フォーカス/編集状態が即時解除される」と特定し、`CanDrag` を `CanDragGroupName(ViewModel.IsEditingName)`（編集中は false）に変更して解消している。原因特定と解消手段が SPEC の作業内容1（「ダブルクリック直後にフォーカス/編集状態が解除される原因を特定して解消する」）と一致。`BeginInlineRename` は従来どおり `DispatcherQueue.TryEnqueue` で Visibility 反映後に `GroupNameEditBox.Focus(Programmatic)` + `SelectAll()` を行い、TextBox へフォーカスと選択を確実に与える。
- **右クリック「名前の変更」から同じ編集に入れる（完了条件2）**: MenuFlyout 先頭に `RenameGroupMenuItem`（AutomationId 付与）を追加し、`RenameGroup_Click` → `BeginInlineRename` でダブルクリックと同一経路に入る。SPEC 作業内容2を充足。
- **Enter 確定・Esc キャンセル・フォーカス喪失で確定が従来どおり（完了条件3）**: `GroupNameEditBox_KeyDown`（Enter→CommitRename / Esc→CancelRename）・`GroupNameEditBox_LostFocus`（CommitRename）は本 Task で未変更。挙動は維持されている。

### 共通化（BeginInlineRename 抽出）の妥当性

`GroupNameText_DoubleTapped` のリネーム開始処理を `BeginInlineRename` に抽出し、ダブルクリックとメニューの双方から呼ぶ。抽出後も:
- ViewModel null ガード（`if (ViewModel is null) return;`）を保持。
- `ViewModel.BeginRename()` → `DispatcherQueue.TryEnqueue` でフォーカス/全選択、の順序を保持。
- ダブルクリック側は `e.Handled = true` を呼び出し元に残し、抽出メソッドからは除去（イベント引数に依存しない純粋なリネーム開始処理になった）。

挙動を変えずに重複を除いており、リファクタとして適切。

### CanDrag 反応性とリグレッション（段 D&D）

- `IsEditingName` は `SetProperty(ref _isEditingName, value)` で PropertyChanged を発火し、`CanDrag` の x:Bind は `Mode=OneWay`。`BeginRename`（IsEditingName=true）で CanDrag=false、`CommitRename`/`CancelRename`（IsEditingName=false）で CanDrag=true に戻る。編集の開始/終了に同期して段 D&D の可否が切り替わる。
- **編集中以外の段 D&D**: 非編集時は `IsEditingName=false` → CanDrag=true で従来どおり。`GroupName_DragStarting`/`GroupName_DropCompleted` および Task 7-3 の段 D&D 経路（`Row_DragOver`/`Row_Drop` 等）は未変更で、編集していない通常状態では従来の段並べ替えがそのまま機能する。
- **編集中の段 D&D 抑止**: 編集に入ると CanDrag=false になり、グループ名 Grid からのドラッグ開始が抑止される。これにより TextBox がフォーカスを保持でき、不具合（Task 7-3 で混入）が解消する。編集→確定/キャンセルで CanDrag が true に復帰し段 D&D が再び可能になるため、状態が一方向に固着しない。

### AutomationId 付与

- 新規 `RenameGroupMenuItem` に `AutomationProperties.AutomationId="RenameGroupMenuItem"` を付与済み。既存メニュー項目（`SaveToFavoritesMenuItem`/`RemoveGroupMenuItem`）と命名規約が整合。
- 既存の `GroupNameText`/`GroupNameEditBox` の AutomationId は維持。UI 自動テストから「名前の変更」メニューの起動・編集 TextBox の特定が可能。付与漏れなし。

### MVVM 層分離・禁止参照

- 変更は `TabNest.App`（View 層）の XAML とコードビハインドのみ。`CanDragGroupName` は `bool → bool` の静的純粋関数で WinUI 型に依存しない。ViewModels/Core への変更なし。
- テストプロジェクトからの `TabNest.App` 参照追加なし（差分はテストに触れていない）。CLAUDE.md の禁止参照ルールに抵触しない。

### 仕様外変更の有無

差分は (1) CanDrag の条件化、(2) リネームメニュー項目追加、(3) リネーム開始の共通メソッド抽出、の3点のみで、いずれも SPEC Task 8-1 の作業内容に直接対応。仕様外の機能追加・挙動変更は含まれない。

### ビルド結果

実環境で `dotnet build src/TabNest.App/TabNest.App.csproj -p:Platform=x64` を実行し成功（0 警告 / 0 エラー）。x:Bind 関数バインド `CanDragGroupName(ViewModel.IsEditingName)` は XAML コンパイル時に解決され、シグネチャ不一致・到達不能はない。

## 結論

**approve（マージ可）。** Critical / Major / Minor の指摘なし（情報 3件）。

SPEC Task 8-1 の作業内容（ダブルクリック編集の不具合解消・原因特定／右クリック「名前の変更」追加）・完了条件3項目（編集状態維持とキーボード入力／メニューからの編集／Enter・Esc・フォーカス喪失の維持）をすべて満たす。不具合の主因（Task 7-3 で混入した親 Grid の `CanDrag="True"` による TextBox フォーカス喪失）を正しく特定し、編集中のみ `CanDrag=false` にする最小限の修正で解消している。`IsEditingName` の PropertyChanged 発火と OneWay バインドにより CanDrag は編集の開始/終了に同期して反応し、編集中以外の段 D&D は従来どおり機能する（リグレッションなし）。`BeginInlineRename` の共通化は挙動を変えないリファクタ。AutomationId 付与漏れなし、MVVM 層分離・禁止参照に問題なし。ViewModel 層の状態遷移テストは既存テストで充足済みで、View 層に閉じた本修正の最終確認は SPEC 指定どおり GUI 手動評価（ダブルクリック・メニューからの編集と入力・確定）に委ねる。

## 対応判断（実装モデル記入欄）

（呼び出し元が記入）
