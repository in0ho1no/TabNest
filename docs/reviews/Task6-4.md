# Task6-4 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Sonnet 4.6
- レビュー対象: `git diff dev-re...Task6-4`
- 実施日: 2026-06-13

## 対象範囲

SPEC.md「Task 6-4: お気に入りのリネーム・並び替え」および「主要機能 > お気に入り（保存済みタブグループ）」節（同名衝突・連番付与の規則）。

差分ファイル:
- `src/TabNest.App/MainPage.xaml`（FavoritesListView に D&D 属性追加・`RenameFavoriteMenuItem` 追加・TextBlock/TextBox 切り替え UI）
- `src/TabNest.App/MainPage.xaml.cs`（`RenameFavorite_Click` / `FavoriteNameEditBox_KeyDown` / `FavoriteNameEditBox_LostFocus` / `FavoritesListView_DragItemsCompleted` / `FindFavoriteEditBox` / `FindDescendantByName` / `VisibleWhen` / `VisibleWhenNot` 追加）
- `src/TabNest.Core/Services/FavoritesService.cs`（`RenameFavorite` / `ReorderFavorites` 追加・`ResolveUniqueName` に `excludeId` パラメータ追加）
- `src/TabNest.ViewModels/FavoriteItemViewModel.cs`（`ViewModelBase` 継承へ変更・`IsEditingName` / `EditingName` / `BeginRename` / `CommitRename` / `CancelRename` / `RefreshName` 追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`CreateFavoriteItem` / `RenameFavorite` / `ReorderFavorites` 追加・`FavoriteItemViewModel` 生成箇所をファクトリ経由に変更）
- `tests/TabNest.Core.Tests/FavoritesServiceTests.cs`（リネーム4件・並び替え2件 新規追加）
- `tests/TabNest.ViewModels.Tests/FavoritesViewModelTests.cs`（リネーム3件・並び替え1件 新規追加）

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `FavoritesListView_DragItemsCompleted` は D&D 完了後に `ViewModel.Favorites`（ListView が ObservableCollection を直接並べ替え済み）の順序を読んで `ReorderFavorites` を呼ぶ。`ReorderFavorites` 内部で `_favorites.ReorderFavorites(orderedIds)` と `Favorites.Move(...)` の両方を実行するが、`Favorites` はすでに正しい順序なので `Move` は全て `current == target` となりノーオペレーションになる。動作は正確だが、「View が先に並べ替えた後で ViewModel に同期する」というコールフローが直感に反するため、コメントで明示しておくと保守性が上がる。現状でも問題はない。 | （未対応） |
| 2 | Minor | `CommitRename()` は `IsEditingName = false` のガードを持つため、Enter→LostFocus の二重呼び出し時に二度目は早期リターンされる。Esc→LostFocus の場合も `CancelRename()` が `IsEditingName = false` にした後、`LostFocus` での `CommitRename()` は早期リターンする。正確であるが、この保護メカニズムがコードコメントに記述されていない。ドキュメントとして明示しておくと将来の変更時に誤った修正を防ぎやすい。動作上の問題なし。 | （未対応） |
| 3 | 情報 | `FindDescendantByName` で `x:Name="FavoriteNameEditBox"` を VisualTree 探索しているが、複数の `FavoriteItemViewModel` が ListView に並ぶ場合、各アイテムのコンテナ内に同名の `FavoriteNameEditBox` が複数存在する。`FindFavoriteEditBox` は対象アイテムの `ContainerFromItem(favorite)` 内だけを探索するため、コンテナを跨いだ誤マッチは発生しない。設計上問題なし（既存の `DeleteFavorite_Click` と同じパターン）。 | （未対応） |
| 4 | 情報 | `ResolveUniqueName` の連番採番アルゴリズムは、既存名セット（自分自身 excludeId を除く）に対して `name (2)`、`name (3)`... と順次試す実装。SPEC「連番 n は 2 から始め、「S」「S (k)」全体の集合に対して未使用の最小値を使う」と一致している。ただし `name` 自体（`S`）は `existing` に含まれる場合のみ連番付与（excludeId で自分を除外した後の集合で判定）という挙動を確認。自分の名前をそのまま使う場合に連番が付かないことも Core テスト「リネーム_自分自身の現在名に変更しても連番は付かない」で検証済み。 | （未対応） |

Critical / Major の指摘なし。

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **右クリックメニュー「名前の変更」**: `MainPage.xaml` の `MenuFlyout` に `RenameFavoriteMenuItem`（Text="名前の変更"、AutomationId 付与）を追加。SPEC「お気に入りの右クリックメニューに『名前の変更』を追加」を充足。
- **SavedTabGroup.Id をキーにリネーム**: `FavoritesService.RenameFavorite(string id, ...)` が `_savedGroups.FirstOrDefault(f => f.Id == id)` で対象を特定し、名前を更新。SPEC「SavedTabGroup.Id をキーにリネームする」を充足。
- **同名衝突は保存時と同じ規則**: `ResolveUniqueName(newName, excludeId: id)` を使用。完全一致で連番 `(n)`（n=2 から未使用の最小値）を付与し、ベース名抽出なし。自分自身の名前は `excludeId` で除外するため同名へのリネームは連番なしで通る。Core テスト3件（衝突なし・別名衝突→連番・自分自身→連番なし）で実値検証済み。SPEC「同名衝突は保存時と同じ規則に従う」を完全に充足。
- **行内 D&D 並び替え**: `FavoritesListView` に `CanReorderItems="True"` / `AllowDrop="True"` / `CanDragItems="True"` を付与。SPEC「ListView の CanReorderItems / AllowDrop による D&D」を充足。
- **並び順の settings.json 保存・次回復元**: `DragItemsCompleted` 完了時に `ReorderFavorites(orderedIds)` → `FavoritesService.ReorderFavorites` → `_savedGroups` の順序が更新される。`CreateAppSettings` で `SavedGroups` の順序が保存され、次回 `RestoreSavedGroups` で復元される。ViewModel テスト「お気に入りを並び替えると表示順とセッションへ反映され復元される」で再起動相当の3段階（並び替え→設定保存→新 ViewModel 復元）を実値で検証。SPEC「並び替え後の順序を settings.json に保存し、次回起動時に復元する」を充足。
- **リネームの再起動後保持**: ViewModel テスト「お気に入りをリネームでき表示名とセッションへ反映される」で `CreateAppSettings` の `SavedGroups[0].Name` が変更後の名前であることを確認。SPEC「お気に入りをリネームでき、再起動後も保持される」を充足。

### MVVM 層分離

- `TabNest.ViewModels` に WinUI 依存コード（`using Microsoft.UI`）なし。
- `TabNest.Core` に WinUI 依存コードなし。
- View 層（`MainPage.xaml.cs`）に `VisualTreeHelper` / `DispatcherQueue` / `FocusState` などの WinUI API が集約されており、ViewModel が View に依存しない設計を維持。
- `FavoriteItemViewModel` の `_rename` コールバックは `Func<string, bool>` 型であり、WinUI 非依存の純粋デリゲート。
- `tests/TabNest.ViewModels.Tests/*.csproj` / `tests/TabNest.Core.Tests/*.csproj` に `TabNest.App` への `ProjectReference` なし（CLAUDE.md の禁止参照ルールを遵守）。

### テストの実効性

- **Core 層リネームテスト（4件）**:
  - 「衝突しない名前ならそのまま変更される」: `result.Value!.Name` と `service.SavedGroups[0].Name` の両方を実値で確認。
  - 「既存の別お気に入りと同名なら連番が付く」: `"作業A (2)"` を実値アサート。
  - 「自分自身の現在名に変更しても連番は付かない」: `"作業A"` のまま（連番なし）を確認。
  - 「存在しないIdは失敗する」: `result.Error == FavoriteNotFound` を確認。
- **Core 層並び替えテスト（2件）**:
  - 「指定したId順に並び順が変わる」: 3要素を逆順にした結果を配列等値比較。
  - 「orderedに含まれない項目は末尾に元の順序で残る」: 部分指定時の残余要素の相対順を確認。
- **ViewModel 層テスト（4件）**:
  - 「お気に入りをリネームでき表示名とセッションへ反映される」: 前後空白トリムを含む実際の入力（`"  資料セット  "`）で `favorite.Name` と `SavedGroups[0].Name` を確認。
  - 「リネーム時の同名衝突は連番が付く」: `"作業1 (2)"` を実値アサート。
  - 「空白のみへのリネームは無視され元の名前を維持する」: `RenameFavorite` の戻り値 `false` と名前不変を確認。
  - 「お気に入りを並び替えると表示順とセッションへ反映され復元される」: ViewModel の `Favorites` 配列・設定の `SavedGroups` 配列・新 ViewModel での復元後配列の3点を実値で検証。形式的アサートなし。

### 状態整合性

- `CommitRename()` は `IsEditingName` ガードを持つため Enter→LostFocus の二重呼び出しでリネームが2回実行されない。Esc→LostFocus の場合も `IsEditingName = false` 後に `CommitRename()` が早期リターンし、キャンセル後に誤確定されない。
- `ReorderFavorites` の ViewModel 実装は `_favorites.ReorderFavorites(orderedIds)` でサービス側を先に更新し、その後 `Favorites.Move()` で ViewModel コレクションを整合させる選択的移動（既に正位置の要素はスキップ）。D&D 完了時は ListView が先にコレクションを並べ替えているため `Move` はノーオペレーションになるが、ViewModel テストから直接 `ReorderFavorites` を呼ぶ場合は正しく `Move` が発動する。両方のコンテキストで正確に動作する設計。
- リネーム失敗時（Id 不存在）は `OperationError` を設定してリターンし、`Name` は変わらない。

### AutomationId の付与

- `RenameFavoriteMenuItem` に `AutomationProperties.AutomationId="RenameFavoriteMenuItem"` を付与。
- `FavoriteNameEditBox` に `AutomationProperties.AutomationId="FavoriteNameEditBox"` を付与。
- UI テストからリネームの開始・編集を識別可能。

### エラー処理

- 存在しない Id へのリネームは `TabOperationError.FavoriteNotFound` を返し、`OperationError` に表示。クラッシュしない。
- 空白のみ入力は ViewModel 層でガードし `false` を返して元の名前を維持。Core 層に空白が渡らない設計。
- `FindFavoriteEditBox` が `null` を返す場合（コンテナが仮想化で存在しない等）は フォーカス処理をスキップし、リネームモードのみ開始される（テキストボックスは非表示のまま）。実際には `ContainerFromItem` は可視領域の項目に対して有効であり、右クリック操作が成立している時点でコンテナは存在するはず。ただし、超稀ケースでフォーカスが移らない可能性は残る（Minor 相当だが、クラッシュなし）。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 2件・情報 2件）。

SPEC Task 6-4 の作業内容（Id キーリネーム・同名衝突連番付与・自分自身を衝突から除外・行内 D&D 並び替え・並び順の settings.json 保存・次回復元）・完了条件（リネーム再起動後保持・並び替え再起動後保持）・テスト要件（リネーム単体・並び替え結合）をすべて満たしている。MVVM 層分離・禁止参照なし・AutomationId 付与いずれも適正。
