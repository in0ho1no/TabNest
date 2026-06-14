# Task8-3 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8（別系統セッション・クロスモデルレビュア）
- レビュー対象: `git diff dev-re...Task8-3`
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 8-3: お気に入り起動操作のホイールクリック対応」（行 2172）／「主要機能 > お気に入り」「初期リリース範囲 > v0.3」

差分ファイル:
- `src/TabNest.App/MainPage.xaml`（お気に入り項目 Grid に `FavoriteBackground(IsSelected)` ハイライトと `PointerPressed="FavoriteItem_PointerPressed"` を追加）
- `src/TabNest.App/MainPage.xaml.cs`（`FavoriteBackground` 追加、`OnPagePointerPressed` をグループ＋お気に入り両対応に拡張、`IsWithinTabGroups`→汎用 `IsWithin` 化、`FavoritesListView_ItemClick` を「開く」から「選択」へ変更、`FavoriteItem_PointerPressed`（中クリック=開く）追加）
- `src/TabNest.ViewModels/FavoriteItemViewModel.cs`（`IsSelected` プロパティ追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`SelectedFavorite`・`SelectFavorite`・`ClearFavoriteSelection` 追加、`RemoveFavorite` で選択中削除時に選択解除）
- `tests/TabNest.ViewModels.Tests/FavoriteSelectionTests.cs`（新規・状態遷移 9 ケース）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | お気に入り名のインライン編集中（`IsEditingName=true`、Task 6-4/8-1 系）でも項目 Grid の `FavoriteItem_PointerPressed` が生きており、編集中に中クリックすると `OpenFavorite` が走って新しい段が開く。SPEC は中クリック=開くを定義しており禁止挙動ではないが、編集セッションを中断して段が増えるのは想定外操作になりうる。編集中は中クリックを無視する（`favorite.IsEditingName` ガード追加）方が安全。状態整合は壊れない（OpenFavorite は既存の正常経路）ため必須ではない。 | |
| 2 | Minor | `FavoriteBackground` は未選択時に毎回 `new SolidColorBrush(Transparent)` を生成する（OneWay バインドで各項目・各 IsSelected 変化ごとに評価）。Task 8-2 の `GroupNameBackground` と同形のパターンで実害は軽微だが、静的な Transparent ブラシをキャッシュ再利用する方が望ましい。 | |
| 3 | 情報 | `RemoveFavorite` は削除前に `itemVm.IsSelected = false; SelectedFavorite = null;` を実行してから `Favorites.Remove` する。Task 8-2 の RemoveGroup レビュー指摘#1（削除対象 VM の IsSelected を戻さない）を踏まえた整合的な実装になっており、stale フラグ残存もない。妥当。 | |
| 4 | 情報 | `IsWithinTabGroups` を汎用 `IsWithin(source, ancestor)` へリファクタし、グループ解除・お気に入り解除の両方で再利用。`static` 化も適切。リファクタ範囲は最小で Task 8-2 の挙動を変えない。 | |
| 5 | 情報 | `OnPagePointerPressed` は `handledEventsToo: true` 登録のため、左クリック選択（ItemClick）・中クリック（PointerPressed で `e.Handled=true`）のいずれでもページ側ハンドラが走るが、クリック位置は `FavoritesListView` 配下のため `IsWithin` が true を返し、お気に入り解除は発火しない。選択経路と解除経路が競合しない。整合済み。 | |
| 6 | 情報 | お気に入り項目 Grid は `PointerPressed` のみで AutomationId 追加要素なし。既存の `FavoritesListView`／`FavoriteItem`（ItemContainerStyle）AutomationId を維持。付与漏れなし。 | |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **左クリック=選択のみ・開かない（作業内容1・完了条件1）**: `FavoritesListView_ItemClick` を `OpenFavorite` 呼び出しから `ViewModel?.SelectFavorite(favorite.Id)` へ変更。`SelectFavorite` はタブグループを開かず `IsSelected`/`SelectedFavorite` のみ更新。テスト `SelectFavorite_左クリックでは選択状態になるだけで開かない` が `Groups.Count` 不変を検証。充足。
- **ホイールクリックで1回で開く（作業内容2・完了条件2）**: `FavoriteItem_PointerPressed` が `e.GetCurrentPoint(element).Properties.IsMiddleButtonPressed` を判定して `OpenFavorite` を呼び `e.Handled=true`。SPEC 実装ノート「PointerPressed + IsMiddleButtonPressed」に準拠。`Tapped`/`ItemClick` ではなく `PointerPressed` を使う点も SPEC 指示どおり。テスト `OpenFavorite_ホイールクリック相当で1回開くと新しい段が増える` で段増加を検証。充足。
- **既存の開く制御を流用（作業内容3・完了条件3）**: 中クリック経路は既存 `OpenFavorite`（5段上限・存在しないパス処理を内包）をそのまま呼ぶ。開くロジックの再実装・改変はない。充足。

### リグレッション（左クリック開く経路の残存有無）

- ソース全体検索（`OpenFavorite`）の結果、`OpenFavorite` の呼び出しは `FavoriteItem_PointerPressed`（中クリック）の1箇所のみ。左クリック/ItemClick から `OpenFavorite` を呼ぶ箇所は残っていない。SPEC「左クリックで開く挙動を選択と起動に分離」を満たし、二重起動・旧挙動残存のリグレッションなし。

### 状態整合性（選択の単一性・解除・削除連動）

- **単一性**: `SelectFavorite` は `foreach` で全 `Favorites` の `IsSelected` を `ReferenceEquals(f, target)` で更新し、常に最大1件のみ true。テスト `別お気に入り選択で前の選択が解除され常に1つだけ選択される`（`Assert.Single`）で検証。Task 8-2 のグループ選択と同一パターン。
- **不正対象の無視**: `Favorites.FirstOrDefault(...) is null` で表示中一覧外の Id は状態を変えず return。テスト `Favoritesに含まれないIdは無視される` で検証。
- **お気に入り一覧外クリックで解除**: `OnPagePointerPressed` が `SelectedFavorite is not null && !IsWithin(source, FavoritesListView)` のときのみ `ClearFavoriteSelection`。グループ解除（`TabGroupsList`）とお気に入り解除（`FavoritesListView`）を独立判定し、両方を1回のクリックで適切に処理。
- **削除連動**: `RemoveFavorite` は選択中項目削除時のみ `IsSelected=false`＋`SelectedFavorite=null`、非選択削除では選択維持。テスト `RemoveFavorite_選択中のお気に入りを削除すると選択も解除される`／`非選択のお気に入り削除では選択が維持される` の両ケースで検証。
- **Task 8-2 との整合**: グループ選択（`SelectedGroup`）とお気に入り選択（`SelectedFavorite`）は別フィールドで独立保持。`OnPagePointerPressed` は両解除を別 `if` で処理し、一方の操作が他方の選択を壊さない。

### テストの実効性

- `FavoriteSelectionTests`（9件）は `SelectedFavorite`・`IsSelected`・単一性・解除・不正無視・PropertyChanged・選択vs開く分岐・削除連動（選択/非選択）を実値で検証しており形式的でない。
- 「左クリック=開かない」を `Groups.Count` 不変、「中クリック=開く」を `Groups.Count+1`・戻り値 `true` で対比検証しており、Task 8-3 中核の分岐を直接突いている。
- 実環境で `dotnet test`（ViewModels.Tests・`FavoriteSelectionTests` フィルタ）9件すべて成功（失敗 0）。App プロジェクトも x64 ビルド成功（警告 0・エラー 0）で XAML の新規 `x:Bind` 静的メソッド参照を含めビルド健全。
- 中クリック検出自体（PointerPressed）は SPEC 指定どおり GUI 手動評価に委ねる旨をテストクラス doc に明記。妥当な切り分け。

### MVVM 層分離・禁止参照

- `SelectFavorite`/`ClearFavoriteSelection`/`SelectedFavorite` は `MainViewModel`（WinUI 非依存）に集約。`FavoriteItemViewModel.IsSelected` の setter は public だが値更新は親が一元管理する旨をコメント明記（Task 8-2 と同方針）。
- WinUI 型（`Brush` 等）は `MainPage.xaml.cs`（App 層）の `FavoriteBackground` 内に閉じており、ViewModels へ漏れていない。
- 追加テストは `TabNest.ViewModels.Tests → TabNest.ViewModels` 参照のみ。`TabNest.App` 参照の追加なし。CLAUDE.md 禁止参照に抵触しない。

### x:Bind の正しさ

- `Background="{x:Bind local:MainPage.FavoriteBackground(IsSelected), Mode=OneWay}"`：`IsSelected` は `FavoriteItemViewModel`（DataTemplate の `x:DataType`）のプロパティで `SetProperty` による PropertyChanged を発火するため OneWay 更新が成立。静的関数バインドの記法も適切。x64 ビルド成功で生成コードの妥当性も確認済み。

### 仕様外変更の有無

差分は (1) お気に入り選択状態の ViewModel 保持・操作、(2) 左クリック=選択／中クリック=開くの分離、(3) 選択ハイライト表示、(4) お気に入り一覧外クリックでの解除、(5) 削除との整合、(6) `IsWithin` リファクタ、(7) 単体テスト追加、に限られ、いずれも SPEC Task 8-3（および Task 8-2 で導入済みの解除基盤の自然な拡張）に直接対応。仕様外の機能追加・挙動変更は含まれない。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 2件・情報 4件）。

SPEC Task 8-3 の作業内容（左クリック=選択のみ・中クリック=開く・既存の開く制御流用）と完了条件3項目をすべて満たす。`OpenFavorite` の呼び出しは中クリック経路の1箇所のみで、左クリック開くの旧挙動は完全に分離・除去されておりリグレッションなし。選択の単一性・一覧外クリック解除・削除連動の状態整合が取れ、Task 8-2 のグループ選択パターン（コールバック/集中管理・`handledEventsToo`+視覚ツリー判定）と整合する。ViewModel 状態遷移テスト9件グリーン・App x64 ビルド成功。Minor #1（編集中の中クリックで開く）・#2（Transparent ブラシ再生成）はいずれも実害が軽微で必須対応ではない。View 層の最終確認は SPEC 指定どおり GUI 手動評価（左クリックで開かない・中クリックで開く）に委ねる。

## 対応判断（実装モデル記入欄）

- 判定:
- Minor #1（編集中の中クリックで開く）:
- Minor #2（Transparent ブラシ再生成）:
- 情報指摘（4件）:

## レビュー対応（実装側・2026-06-14）

- [Minor 対応] お気に入りのインライン編集中でも中クリックで開いてしまう件:
  `FavoriteItem_PointerPressed` に `!favorite.IsEditingName` ガードを追加し、
  編集中の中クリックでは開かないようにした（`IsRenameInProgress` はグループ名編集のみを見ており
  お気に入りの編集中はカバーしていなかったため）。
- [Minor 未対応] `FavoriteBackground` 未選択時の `SolidColorBrush(Transparent)` 毎回生成:
  Task 8-2 の `GroupNameBackground` と同形で実害軽微。既存パターンとの一貫性を優先し据え置く
  （将来 8-2 と併せてキャッシュ化を検討）。
- 判定: approve（対応必須なし）。上記ガード追加後も build/test グリーン（ViewModels 229 件含む全 319 合格）。
