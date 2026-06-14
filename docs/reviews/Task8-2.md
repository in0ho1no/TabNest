# Task8-2 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8（別系統セッション・クロスモデルレビュア）
- レビュー対象: `git diff dev-re...Task8-2`
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 8-2: タブグループの選択状態の導入と視覚表示」（行 2141）／「初期リリース範囲 > v0.3」

差分ファイル:
- `src/TabNest.ViewModels/TabGroupViewModel.cs`（`IsSelected` プロパティ・`Select()` メソッド・`selectGroup` コールバック引数を追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`SelectedGroup`・`SelectGroup`・`ClearGroupSelection` を追加、`RemoveGroup` で選択中削除時に選択解除、`CreateGroupViewModel` に `SelectGroup` を委譲）
- `src/TabNest.App/Controls/TabGroupRow.xaml` / `.xaml.cs`（名前左の縦線アクセント＋淡いハイライト、`Tapped` で `Select()` 呼び出し）
- `src/TabNest.App/MainPage.xaml` / `.xaml.cs`（`TabGroupsList` 命名、ページ全体の `PointerPressed` を `handledEventsToo: true` で捕捉し、グループ外クリックで選択解除）
- `tests/TabNest.ViewModels.Tests/TabGroupSelectionTests.cs`（新規・状態遷移 11 ケース）
- `tests/TabNest.ViewModels.Tests/TabGroupViewModelTests.cs`（`Select` 委譲・`IsSelected` PropertyChanged の 2 ケース追加）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `MainViewModel.RemoveGroup` は選択中グループ削除時に `SelectedGroup = null` のみ実行し、削除対象 `groupVm.IsSelected = false` を戻していない（`ClearGroupSelection`/`SelectGroup` は全グループの `IsSelected` を更新する）。実害なし（`Groups.Add` 経路はすべて `CreateGroupViewModel` で `IsSelected=false` の新規 VM を生成しており、削除済み VM が再投入されることはない＝stale フラグは GC される孤立オブジェクトに残るのみ）。ただし他経路との一貫性のため、削除前に `groupVm.IsSelected = false` を入れておくのが望ましい。テスト `RemoveGroup_選択中グループを削除すると選択も解除される` は残存グループの `IsSelected` しか検証せず、この不一致を検出しない。 | |
| 2 | 情報 | 選択視覚表示として「名前左の縦線アクセント（Border）」と「名前領域の淡いハイライト（SubtleFillColorSecondaryBrush）」の2種を併用。SPEC は「いずれか1つでよい」だが軽量手段の範囲内で過剰ではない。Border は装飾要素のため AutomationId 未付与で妥当。 | |
| 3 | 情報 | 未選択時の `GroupNameBackground` は `Transparent` を返し、グループ名 Grid 全体を左クリック/段 D&D の当たり判定として確保している（コメントに意図明記）。設計妥当。 | |
| 4 | 情報 | `GroupNameText_Tapped` は `e.Handled = true` を設定するが、ページ側ハンドラは `handledEventsToo: true` 登録のため捕捉可能。タップ位置は `TabGroupsList` 配下のため `IsWithinTabGroups` が true を返し選択は解除されない。`Tapped`（選択）と `PointerPressed`（解除判定）は別経路で競合しない。整合済み。 | |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **左クリックで選択状態（完了条件1）**: `TextBlock GroupNameText` の `Tapped` → `ViewModel.Select()` → `MainViewModel.SelectGroup`。選択中は縦線 Border が Visible・名前領域が淡くハイライトされ、選択が視覚的に判別できる。SPEC 作業内容1・完了条件1を充足。
- **アクティブグループとは別概念**: `IsSelected`（選択）と既存のアクティブ表示（`IsActive`/`ApplyActiveStates`）は独立。`SelectGroup`/`ClearGroupSelection` は `_tabManager` のアクティブ状態に一切触れず、テスト `選択切替でアクティブタブの状態は壊れない` で `activeTab.IsActive` 維持を検証。完了条件3を充足。
- **グループ外クリックで解除（完了条件2）**: `MainPage` コンストラクタで `AddHandler(PointerPressedEvent, …, handledEventsToo: true)` を登録し、`OnPagePointerPressed` が `OriginalSource` の視覚ツリーを `TabGroupsList` まで遡って判定（`IsWithinTabGroups`）。配下外かつ選択ありのときのみ `ClearGroupSelection`。完了条件2を充足。
- **ViewModel 層で保持・F2 リネームの対象特定に使える形（作業内容4）**: 選択状態は `MainViewModel.SelectedGroup`（private set・PropertyChanged 発火）に一元保持。Task 8-6 から `SelectedGroup` を参照すれば対象グループを特定できる。充足。

### 状態整合性（選択の単一性・解除・削除連動）

- **単一性**: `SelectGroup` は `foreach` で全グループの `IsSelected` を `ReferenceEquals(g, group)` で更新し、常に最大1グループのみ true。テスト `別グループ選択で前の選択が解除され常に1つだけ選択される`（`Assert.Single`）で検証。
- **不正対象の無視**: `Groups.Contains(group)` を満たさないグループは無視し状態を変えない。テスト `Groupsに含まれないグループは無視される` で検証。
- **削除連動**: 選択中グループ削除で `SelectedGroup=null`（指摘#1の stale `IsSelected` を除き）、非選択グループ削除で選択維持。両ケースをテスト済み。
- **並べ替え時の保持**: `MoveGroup` は `Groups.Move(oldIndex, newIndex)` で同一 VM インスタンスを保持するため、段 D&D 並べ替え後も選択（`IsSelected`/`SelectedGroup`）が維持される（リグレッションなし）。

### View 側クリック外し判定

- `handledEventsToo: true` 登録により、子要素（タブボタン・グループ名等）が `PointerPressed`/`Tapped` を `Handled` 済みでもページ側で捕捉できる。
- `IsWithinTabGroups` は `VisualTreeHelper.GetParent` で `OriginalSource` から祖先を辿り `TabGroupsList` 一致で true。タブグループ領域（グループ名・タブ）内クリックでは解除せず、領域外でのみ解除する。SPEC「グループ以外の領域をクリックしたら解除」と一致。
- ガード `SelectedGroup is null` で未選択時は早期 return し、無用な PropertyChanged を抑制。

### テストの実効性

- `TabGroupSelectionTests`（11 件）は `SelectedGroup`・`IsSelected`・単一性・解除・不正無視・PropertyChanged・アクティブ非破壊・削除連動（選択/非選択）を実値で検証しており形式的でない。
- `TabGroupViewModelTests` 追加2件で `Select()` のコールバック委譲と `IsSelected` の PropertyChanged を検証。
- 実環境で `dotnet test`（ViewModels.Tests・`TabGroupSelection`/`TabGroupViewModelTests` フィルタ）を実行し 26 件すべて成功（失敗 0）。View 層の最終確認は SPEC 指定どおり GUI 手動評価に委ねる。

### MVVM 層分離・禁止参照

- `TabGroupViewModel.Select()` は `Action<TabGroupViewModel>` コールバックを親 `MainViewModel` に委譲する既存パターン（`selectTab`/`moveGroupHere` 等と同形）に準拠。WinUI 型非依存。
- 選択状態管理（単一性・`SelectedGroup`）は `MainViewModel` に集約され、`TabGroupViewModel.IsSelected` の setter は public だが値更新は親が一元管理する旨をコメント明記。
- 追加テストは `TabNest.ViewModels.Tests → TabNest.ViewModels` 参照のみ。`TabNest.App` 参照の追加なし。CLAUDE.md 禁止参照ルールに抵触しない。

### AutomationId 付与

- 既存 `GroupNameText`/`GroupNameEditBox` の AutomationId は維持。`MainPage` の `ItemsControl` に `x:Name="TabGroupsList"`（AutomationId も既存）を付与し視覚ツリー判定に使用。新規 Border は装飾要素のため AutomationId 不要。付与漏れなし。

### 仕様外変更の有無

差分は (1) 選択状態の ViewModel 保持・操作、(2) 縦線＋ハイライトの視覚表示、(3) 名前左クリックでの選択、(4) グループ外クリックでの解除、(5) 削除/並べ替えとの整合、(6) 単体テスト追加、に限られ、いずれも SPEC Task 8-2 に直接対応。仕様外の機能追加・挙動変更は含まれない。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 1件・情報 3件）。

SPEC Task 8-2 の作業内容（選択状態の導入・視覚表示・グループ外クリック解除・ViewModel 層保持で F2 リネーム対象特定可能）と完了条件3項目をすべて満たす。選択はアクティブ表示と独立し、単一性・解除・削除連動・並べ替え保持の状態整合が取れている。コールバック委譲・`Groups.Move` 等の既存パターンに準拠し、View 側のクリック外し判定（`handledEventsToo` + 視覚ツリー判定）も妥当。Minor #1（`RemoveGroup` で削除対象 VM の `IsSelected` を戻さない）は孤立オブジェクトに残るのみで実害なく必須対応ではないが、他経路との一貫性のため修正が望ましい。ViewModel 層の状態遷移テストは充実しており実環境で 26 件グリーン。View 層の最終確認は SPEC 指定どおり GUI 手動評価（左クリックで選択表示・グループ外クリックで解除）に委ねる。

## 対応判断（実装モデル記入欄）

- 判定: approve（対応必須の指摘なし）。
- Minor #1（`RemoveGroup` で削除対象 VM の `IsSelected` を戻さない）: **対応**。
  一貫性のため `Groups.Remove` 後に `groupVm.IsSelected = false` を追加してから `SelectedGroup = null` とした。
  挙動は不変（削除済み VM は再利用されない）のためフル再レビューは省略し、build/test の再実行のみ行う。
- 情報指摘（3件）: いずれも任意・将来タスク（Task 8-6 の F2 連携等）の範囲であり、本タスクでは対応不要と判断。
