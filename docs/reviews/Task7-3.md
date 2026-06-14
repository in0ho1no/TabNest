# Task7-3 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8（別系統セッション・クロスモデルレビュア）
- レビュー対象: `git diff dev-re...Task7-3`（1コミット `864aeff`）
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 7-3: タブグループの並び替え（段の入れ替え）」（行 2052 付近）／「初期リリース範囲 > v0.2」

差分ファイル:
- `src/TabNest.Core/Services/TabManagerService.cs`（`ReorderGroups` 追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`MoveGroup` 追加・`CreateGroupViewModel` に `moveGroupHere` コールバック配線）
- `src/TabNest.ViewModels/TabGroupViewModel.cs`（`IsDropAbove`/`IsDropBelow`・`SetGroupDropIndicator`/`ClearGroupDropIndicator`・`MoveGroupHere`・`_moveGroupHere` コールバック追加）
- `src/TabNest.App/Controls/TabGroupRow.xaml`（段ルート Grid をドロップ先化・上下インジケータ Border・グループ名 Grid にドラッグハンドル属性）
- `src/TabNest.App/Controls/TabGroupRow.xaml.cs`（`s_draggingGroup`/`s_groupDropTarget` 静的追加・`GroupName_DragStarting`/`Row_DragOver`/`Row_DragLeave`/`Row_Drop`/`GroupName_DropCompleted`・インジケータヘルパー）
- `tests/TabNest.Core.Tests/TabManagerServiceTests.cs`（`ReorderGroups` 単体テスト 3件）
- `tests/TabNest.ViewModels.Tests/TabGroupReorderTests.cs`（新規・7件）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `MainViewModel.MoveGroup` は表示順 `Groups` のみで挿入位置を計算し no-op 判定後に `_tabManager.ReorderGroups(Groups.Select(...))` を呼ぶ。`Groups` と `_tabManager.Groups` が既に同順である前提に依存している（実際は全変更経路で同期維持されており現状破綻しない）。ReorderGroups 側は orderedIds に無い既存グループを末尾に残す防御を持つため、万一不整合があっても消失はしないが順序が崩れうる。防衛として `MoveGroup` 内で `Groups` と `_tabManager.Groups` の対応を前提にしている旨はコメント済みであり実害なし。 | |
| 2 | Minor | グループ名 Grid に `CanDrag="True"` を付与したことで、ダブルクリック・インライン編集（既存機能）やコンテキストフライアウト（お気に入り保存）とドラッグ操作が同一要素上で共存する。短いポインタ移動でドラッグ判定に入ると編集開始が阻害される可能性がある（WinUI のドラッグ閾値内なら問題にならない）。手動 GUI 評価でリネーム・右クリック保存が従来どおり動作することの確認を推奨。 | |
| 3 | Minor | `Row_DragLeave` は `ReferenceEquals(ViewModel, s_groupDropTarget)` のときのみインジケータを消す。段間をまたぐ移動では `Row_DragOver`→`SetGroupDropIndicator` が旧段を消してから新段へ付け替えるため消し残りは起きず、全段を外れたときも最後の段の DragLeave で消える。設計上の消し残し経路は確認できなかったが、DragOver と DragLeave の発火順序は WinUI 実装依存のため、GUI 評価でインジケータの残留が無いことの確認を推奨。 | |
| 4 | 情報 | グループ段並べ替えの D&D 経路は座標ベースで、段ルート Grist・グループ名ドラッグハンドル・上下インジケータ Border のいずれにも AutomationId は付与されていない。Task 7-1/7-2 と同様、ドロップターゲットは UIA から操作しない方針と整合する。UI 自動テストは ViewModel 経路（`MoveGroup`/`MoveGroupHere`）の単体テストで代替されており、SPEC のテスト要件（モデル操作・アクティブ維持の単体テスト）は満たす。 | |
| 5 | 情報 | `TabManagerService` に `ReorderGroups` を公開したが、最小1段・最大5段の制約はそもそも段数を変えない並べ替えでは影響しない。空グループ段の並べ替えも Id ベースで内容非依存のため安全。エッジは網羅されている。 | |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **段順をドラッグで入れ替えられる**: グループ名 Grid を `CanDrag` ハンドルにして `GroupName_DragStarting` で `s_draggingGroup` を設定、段ルート Grid を `AllowDrop` のドロップ先にして `Row_Drop` で `MoveGroupHere`→`MainViewModel.MoveGroup`→`Groups.Move` + `_tabManager.ReorderGroups` を呼ぶ経路が実装されている。SPEC 作業内容1・完了条件1を充足。
- **挿入位置インジケータ**: `Row_DragOver` でポインタ Y が段の上半分/下半分かにより `IsDropAbove`/`IsDropBelow` を切り替え、段ルート Grid の Row 0/Row 2 の Border（`VisibleWhen` バインド）で上端/下端に表示する。自分自身の上では出さない。SPEC 作業内容2を充足。
- **settings.json への反映**: `MoveGroup` が `_tabManager.ReorderGroups(Groups.Select(g => g.Id).ToList())` でモデル順を同期し、`CreateAppSettings` は `TabGroups = _tabManager.Groups.ToList()` を保存対象とする。表示順とモデル順が一致するため再起動後に保持される。ViewModel テスト `MoveGroup_並べ替え後の段順がセッション保存に反映される` で `CreateAppSettings` の段順が `Groups` と一致することを検証済み。SPEC 作業内容3・完了条件3を充足。
- **アクティブ追従・各グループ内容保持**: 並べ替えは `TabGroup` インスタンスの並び替えのみで Id・Tabs・アクティブ状態を変更しない。`ActiveGroupId`/`ActiveTabId` は Core で不変、ViewModel 側も `Groups.Move` でインスタンスを保持。Core テスト `ReorderGroups_並べ替えてもアクティブグループとアクティブタブが維持される`・ViewModel テスト `MoveGroup_並べ替えてもアクティブ状態と各グループ内容が保持される` で検証済み。SPEC 作業内容4・完了条件2を充足。

### MoveGroup 挿入位置補正の整合性（オフバイワン検証）

`insertIndex = below ? targetIndex+1 : targetIndex` → `newIndex = insertIndex > oldIndex ? insertIndex-1 : insertIndex` → `Clamp(newIndex, 0, Count-1)` → `newIndex == oldIndex なら no-op`。MoveTab（Task 7-1）と同一規則。代表ケースで検証:

- [g1,g2,g3] で g3(old=2) を g1(target=0) の直前(below=false): insert=0, 0>2=false→new=0, Move(2,0)→[g3,g1,g2]。✓
- g1(old=0) を g3(target=2) の直後(below=true): insert=3, 3>0=true→new=2, Clamp→2, Move(0,2)→[g2,g3,g1]。✓
- g1(old=0) を g2(target=1) の直前(below=false): insert=1, 1>0=true→new=0, ==old→false（no-op）。✓ g1 は既に g2 の直前にあり正しい。
- 自分自身を target に指定: `ReferenceEquals(source, target)` で false を即返す。✓

source 除去後座標への `-1` 補正条件（`insertIndex > oldIndex`）は ObservableCollection.Move のセマンティクス（Move は内部で remove→insert し new は除去後の最終位置）と一致する。Clamp 上限は Move 用に `Count-1`（MoveTab の Insert 用 `Count` と異なるのは正しい）。オフバイワンは検出されなかった。

### 既存 D&D 基盤との整合・排他制御

- **静的状態の排他**: `s_draggingTab`（7-1/7-2）と `s_draggingGroup`（7-3）は相互に排他。`TabItem_DragStarting` で `s_draggingGroup=null`、`GroupName_DragStarting` で `s_draggingTab=null`/`s_draggingSourceGroup=null` を設定し、開始時に一方を必ずクリアする。
- **イベントのバブリングと e.Handled**:
  - グループ段ドラッグ中（`s_draggingTab is null`）: タブ Border の `TabItem_DragOver`・ScrollViewer の `Group_DragOver` は冒頭 `if (s_draggingTab is null ...) return;` で早期リターンし `e.Handled` を立てない。イベントは段ルート Grid までバブリングし `Row_DragOver` が `AcceptedOperation=Move` と `e.Handled=true` を設定する。ドロップ受理に必要な AcceptedOperation が確実にセットされる。
  - タブドラッグ中（`s_draggingGroup is null`）: `Row_DragOver`/`Row_Drop` は冒頭 `if (s_draggingGroup is null ...) return;` で早期リターン。タブ系ハンドラが従来どおり処理する。
  - 双方向で誤受理・二重インジケータの経路は確認できなかった。
- **インジケータの単一表示保証**: `SetGroupDropIndicator(target,...)` は `s_groupDropTarget != target` のとき旧 target を `ClearGroupDropIndicator` してから付け替え、同時に1段だけ表示する。タブ系インジケータ（`IsDropBefore`/`IsDropAfter`）とはプロパティが別（`IsDropAbove`/`IsDropBelow`）で衝突しない。

### D&D 静的状態のライフサイクル

- **設定**: `GroupName_DragStarting` のみ（`s_draggingGroup = ViewModel`）。
- **クリア**: `GroupName_DropCompleted`（成否問わず開始元段で発火）で `s_draggingGroup = null` と `ClearGroupDropIndicator()`。ESC・ウィンドウ外ドロップなどキャンセル時も DropCompleted が発火するためリーク経路なし。`Row_Drop` 末尾でも `ClearGroupDropIndicator()` を呼ぶ。`s_groupDropTarget` は Drop と DropCompleted の双方でクリアされ、次セッションへ残留しない。

### エッジケース

- **同一段ドロップ**: `Row_Drop`/`Row_DragOver` で `ReferenceEquals(ViewModel, s_draggingGroup)` のときインジケータを出さず移動しない。ViewModel `MoveGroup` も `ReferenceEquals(source, target)` と no-op 判定で二重に防御。
- **未知 Id**: `MoveGroup` は `Groups.FirstOrDefault(g => g.Id == targetGroupId)` が null なら false。`ReorderGroups` は orderedIds 中の未知 Id を `FirstOrDefault`→null で無視し、orderedIds に無い既存グループは元の相対順で末尾に残す（消失なし）。Core テスト `ReorderGroups_未知のIdは無視し未指定グループは末尾に元の順で残す` で検証済み。
- **段数制約（最小1・最大5）**: 並べ替えは段数を変えないため無関係。
- **空グループ段の並べ替え**: `ReorderGroups` は Id ベースで Tabs に非依存。空段でも安全。

### MVVM 層分離

- `TabManagerService`・`MainViewModel`・`TabGroupViewModel` の追加コードに `Microsoft.UI`/WinUI 系 namespace なし。WinUI 依存は `TabGroupRow.xaml(.cs)`（App 層）に閉じている。
- 追加テストは `TabNest.Core.Tests → TabNest.Core`・`TabNest.ViewModels.Tests → TabNest.ViewModels` の参照のみで `TabNest.App` を参照しない（CLAUDE.md 禁止参照ルール遵守）。

### テストの実効性・回帰リスク

- **Core（3件）**: 段順の実値（Id 列）・アクティブ Group/Tab Id の維持・Tabs インスタンス同一性（`Assert.Same`）・未知 Id 無視と末尾残しを実値で検証。形式的アサートなし。
- **ViewModel（7件）**: `Groups` の実順序・インスタンス同一性、アクティブ追従（`activeTab.IsActive` 維持）、`CreateAppSettings` への反映、同一位置 no-op（false 返却＋順序不変）、自分自身 target で false、`MoveGroupHere` 経由（View が呼ぶドロップ先 VM 経路）の配線を検証。実装の正しさを実際に検証しており形だけではない。
- **回帰**: 既存テストへの破壊的変更なし（コンストラクタ引数はデフォルト null の追加のみで後方互換）。実環境で新規 10 件すべてグリーンを確認済み（ViewModels 7・Core 3）。

### ビルド・テスト結果

実環境で `dotnet test` を対象テスト（`TabGroupReorder`・`ReorderGroups`）に絞って実行し、ViewModels 7件・Core 3件すべて成功を確認。シグネチャ・新規 using・XAML バインド（`VisibleWhen` 既存ヘルパー流用）に問題なし。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 3件・情報 2件）。

SPEC Task 7-3 の作業内容（段の D&D 入れ替え・挿入位置インジケータ・settings.json 反映・アクティブ追従/内容保持）・完了条件（3条件すべて）・テスト要件（モデル操作の単体テスト・アクティブ維持の単体テスト）をすべて満たしている。`MoveGroup` の挿入位置補正は MoveTab と同一規則でオフバイワンなし、no-op・自己ドロップ・未知 Id を多重に防御。グループ段 D&D（`s_draggingGroup`）とタブ D&D（`s_draggingTab`）は開始時の相互クリアと各ハンドラ冒頭の null ガードで排他され、バブリングは外側 Grid の `Row_DragOver` が AcceptedOperation+Handled を引き受ける形で整合する。静的状態は `DropCompleted` で確実にクリアされリークなし。MVVM 層分離・禁止参照なし。Minor はいずれも GUI 手動評価での確認推奨事項（リネーム/右クリックとドラッグの共存・インジケータ残留）であり、状態整合性・クラッシュ安全性・仕様充足への影響はない。残る GUI 評価（SPEC のテスト要件「GUI評価: グループ段の並び替えを確認する」）はユーザー手動確認に委ねる。

## 対応判断（実装モデル: Claude Opus 4.8）

approve・Critical/Major なし。Minor 3件・情報 2件はいずれもコード修正不要と判断。根拠は以下。

- **#1（MoveGroup が Groups と _tabManager.Groups の同順を前提）**: 現状すべての段変更経路（AddGroup/RemoveGroup/OpenFavorite/CloseTab の自動クローズ/本 MoveGroup）で両者は同順に維持されており破綻しない。`ReorderGroups` は orderedIds に無い既存グループを末尾に残す防御を持ち、万一の不整合でも段の消失は起きない。対応不要（既存コメントで前提を明示済み）。
- **#2（グループ名のドラッグハンドルとリネーム/右クリックの共存）**: GUI 評価（コミット前）で、グループ名ダブルクリックによるインライン編集（`GroupNameEditBox` 出現・値表示・ESC キャンセル）が従来どおり動作することを確認済み。WinUI のドラッグ閾値内ではドラッグ判定に入らないため編集・右クリックは阻害されない。対応不要。
- **#3（インジケータ残留）**: 設計上、段間移動は `SetGroupDropIndicator` が旧段を消してから付け替え、全段離脱時は最後の段の `Row_DragLeave` と `GroupName_DropCompleted` で消える。GUI 評価では D&D 自体を外部自動操作で駆動できなかったため（WinUI 3 の D&D は低レベル入力注入では発火しない／Task 7-1・7-2 と同じ既知制約）残留の目視確認は未達。残留経路はコード上確認できず、ドロップ完了時の二重クリア（`Row_Drop` 末尾 + `DropCompleted`）で担保。**段の D&D 入れ替え動作とインジケータ表示・残留無しの最終確認はユーザー手動確認に委ねる**（SPEC テスト要件「GUI評価: グループ段の並び替えを確認する」）。
- **#4・#5（情報）**: 仕様・方針と整合。対応不要。
