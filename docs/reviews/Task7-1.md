# Task7-1 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8（別系統セッション）
- レビュー対象: `git diff dev-re...Task7-1`（2コミット）
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 7-1: ドラッグ&ドロップ基盤とタブ並び替え（グループ内）」

差分ファイル:
- `src/TabNest.App/Controls/TabGroupRow.xaml`（タブを左右の挿入位置インジケータで挟む StackPanel 化・CanDrag/AllowDrop・D&D イベント配線）
- `src/TabNest.App/Controls/TabGroupRow.xaml.cs`（`_draggingTab` 保持・DragStarting/DragOver/DragLeave/Drop/DropCompleted 実装）
- `src/TabNest.App/MainPage.xaml.cs`（スコープ外: フォルダツリー非表示時の Debug 起動クラッシュ修正）
- `src/TabNest.Core/Services/TabManagerService.cs`（`ReorderTabs(groupId, orderedIds)` 追加）
- `src/TabNest.ViewModels/FolderTabViewModel.cs`（`IsDropBefore`/`IsDropAfter` プロパティ追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`ReorderTabsInGroup` 追加・GroupViewModel 生成に reorder コールバック配線）
- `src/TabNest.ViewModels/TabGroupViewModel.cs`（`MoveTab`/`SetDropIndicator`/`ClearDropIndicators` 追加）
- `tests/TabNest.Core.Tests/TabManagerServiceTests.cs`（ReorderTabs 単体テスト 4件追加）
- `tests/TabNest.ViewModels.Tests/TabGroupViewModelTests.cs`（MoveTab/インジケータ 単体テスト 6件追加）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `TabGroupRow` の `_draggingTab` はインスタンスごとに独立しているため「同一グループ内のみ」を実質的に担保しているが、これは「各グループ行が別 UserControl である」という現在の構造に暗黙依存している。SPEC の意図（7-1 は同一グループ内のみ）は満たすが、`TabItem_Drop` / `TabItem_DragOver` に「ドロップ先タブが `ViewModel.Tabs` に属するか」の明示チェックがあれば、将来 7-2/7-3 で構造が変わっても安全側に倒れる。現状 `ViewModel.Tabs.IndexOf(target)` が `targetIndex >= 0` を確認しているため実害はない。 | （未対応） |
| 2 | Minor | `TabItem_DragOver` で `_draggingTab is null`（他グループ起点のドラッグ）の場合に早期 return し `e.AcceptedOperation` を設定しないため OS 側で拒否される（正しい挙動）。ただしこの分岐は単体テスト・UI テストで直接検証されておらず、回帰時に気付きにくい。7-2 実装時にクロスグループ拒否/受理のテストを追加することが望ましい。 | （未対応） |
| 3 | 情報 | `MoveTab` 冒頭の `ClearDropIndicators()` と `TabItem_Drop` 末尾の `ViewModel.ClearDropIndicators()` でインジケータ消去が二重に走る（MoveTab 内→Drop 末尾）。冪等なので実害はない。 | （対応不要） |
| 4 | 情報 | スコープ外の `MainPage.xaml.cs` 修正は妥当（下記「スコープ外修正の評価」参照）。 | （対応不要） |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **D&D 基盤（CanDrag/DragStarting/DragOver/Drop）**: XAML で `CanDrag="True"` / `AllowDrop="True"` と 5イベント（DragStarting/DragOver/DragLeave/Drop/DropCompleted）を配線。`DragStarting` で `args.Data.SetText(tab.Id)` と `RequestedOperation = Move` を設定しており、7-2 のグループ間移動でデータ参照する布石も入っている。SPEC 作業内容を充足。
- **同一グループ内の並べ替え**: `_draggingTab` がグループ行インスタンスごとに独立しているため、他グループからのドロップは `_draggingTab is null` で `TabItem_DragOver`/`TabItem_Drop` ともに早期 return し受理されない。同一グループ内のみ並べ替わる。完了条件1を充足。
- **挿入位置インジケータ**: 各タブを左右 2本の `Border`（幅3・AccentFill）で挟み、`IsDropBefore`/`IsDropAfter` に `VisibleWhen` でバインド。`SetDropIndicator` が全タブのフラグをクリアしつつ対象1か所のみ立てるため、常に1本だけ表示される。SPEC「ドロップ位置インジケータ表示」を充足。
- **TabGroup.Tabs 順序更新 + settings.json 反映**: `MoveTab` → `_reorderTabs?.Invoke(Tabs.Select(t => t.Id))` → `MainViewModel.ReorderTabsInGroup` → `TabManagerService.ReorderTabs` で `group.Tabs` を in-place 並べ替え。`CreateAppSettings` が `TabGroups = _tabManager.Groups.ToList()` を保存するため、並べ替え結果は次回起動時に復元される。完了条件3（再起動後の順序保持）を充足。
- **アクティブ・選択状態の保持**: 並べ替えはタブの同一性（FolderTab/FolderTabViewModel インスタンス・Id）を変えず順序のみ変更する。`ActiveTabId`/`SelectedTabId` は Id 参照のため不変。完了条件2を充足。Core/ViewModels 両層のテストで検証済み。

### MoveTab 挿入位置補正ロジックの off-by-one 検証

`MoveTab(source, insertIndex)` の補正 `newIndex = insertIndex > oldIndex ? insertIndex - 1 : insertIndex` を `[t1,t2,t3]` の全代表ケースでトレースし、いずれも期待結果と一致することを確認した:

- t1 を t3 左半分へ（insertIndex=2, old=0）→ newIndex=1 → `[t2,t1,t3]` ✓
- t1 を t3 右半分へ（insertIndex=3, old=0）→ newIndex=2(clamp) → `[t2,t3,t1]` ✓
- t3 を t1 左半分へ（insertIndex=0, old=2）→ newIndex=0 → `[t3,t1,t2]` ✓
- t3 を t1 右半分へ（insertIndex=1, old=2）→ newIndex=1 → `[t1,t3,t2]` ✓
- t1 を自分の左（insertIndex=0, old=0）→ newIndex=0 == old → `false`・通知なし ✓

`Drop` ハンドラは挿入位置を「移動前リスト座標」（target の現 index、右半分なら +1）で渡し、`MoveTab` 側で remove 後座標へ -1 補正する契約が一貫している。`Math.Clamp(newIndex, 0, Tabs.Count - 1)` は末尾右半分ドロップ（insertIndex==Count）を `Count-1` へ正しく丸める。off-by-one なし。

### D&D 状態管理

- `_draggingTab` は `DragStarting` で設定、`Drop` 成功時・`DropCompleted`（成否問わず）でクリア。ドロップが起きずドラッグがキャンセルされた場合も `DropCompleted` で確実にクリアされる。リーク経路なし。
- インジケータは `DragLeave`（対象タブ単位）・`MoveTab` 冒頭・`Drop` 末尾・`DropCompleted` の各所でクリアされ、`SetDropIndicator` も毎回全クリアしてから1本立てるため、消し忘れ・複数同時表示は起きない。
- `TabItem_DragOver` で自分自身の上（`ReferenceEquals(target, _draggingTab)`）はインジケータを出さず `ClearDropIndicators()` する。移動が起きない位置での誤表示を防いでいる。

### 同一グループ限定の実装

各 `TabGroupRow` が独立 UserControl で `_draggingTab` を個別保持するため、ドラッグ元グループ以外の行では `_draggingTab is null` となりドロップが受理されない。同一グループ内のみ受け付ける要件を満たす（指摘#1 のとおり明示チェックがあればより堅牢だが現状で実害なし）。

### ReorderTabs（Core）の防御的設計

- `orderedIds` に含まれない既存タブは元の相対順で末尾に残し、未知の Id は無視する。Id ベースの並べ替えでタブ同一性を保持するため、`ActiveTabId`/`SelectedTabId` は不変。
- 存在しないグループでは `false`。これらは単体テスト 4件（指定順反映・アクティブ/選択維持・未知Id無視+未指定末尾残し・グループ不在false）で網羅的に検証されている。

### settings.json 反映経路

`CreateAppSettings` は `_tabManager.Groups`（=`ReorderTabs` が in-place 変更する同一インスタンス）を直接シリアライズ対象にしている。`ReorderTabs` が `group.Tabs.Clear()` + `AddRange(reordered)` で同一 `TabGroup` の `Tabs` リストを書き換えるため、保存経路に追加配線は不要で確実に反映される。経路は正しい。

### スコープ外修正の評価（MainPage.xaml.cs）

- 旧コードは `FolderTreeView_Loaded` で `TreeViewList` が見つからない場合に無条件 `Debug.Assert(innerList is not null)` していた。Task 6-5 のフォルダツリー表示トグルで `Collapsed` の場合、内部 `TreeViewList` が未実体化のため Debug ビルドでアサート失敗→起動クラッシュする既存不具合。
- 修正後は「`Visibility == Collapsed` のときは正常として return、表示中なのに見つからない場合のみアサート」に変更。表示中の内部構造変化検知（開発中の早期検出）は維持しつつ、非表示時の誤クラッシュを解消している。アサート条件が「非表示なら許容」と意味的に正しく、Release 挙動（アサートは Debug のみ）への影響もない。スコープ外だが修正として妥当。

### MVVM 層分離

- `TabGroupViewModel`/`FolderTabViewModel`/`MainViewModel`/`TabManagerService` の変更に WinUI 依存の using 追加なし（`Microsoft.UI` 系なし）。`MoveTab`/`SetDropIndicator`/`ReorderTabs` は純粋なコレクション操作。WinUI 依存は `TabGroupRow.xaml.cs`（App 層）に閉じている。
- 追加テストの `ProjectReference` は `TabNest.ViewModels` / `TabNest.Core` のみで `TabNest.App` 参照なし。CLAUDE.md 禁止参照ルールを遵守。

### AutomationId

- D&D は座標ベースの UI 操作のため新規 AutomationId は不要。既存の `FolderTabItem`（TextBlock）・`DuplicateTabMenuItem` は StackPanel 化後も保持されており、付与漏れ・喪失なし。

### テストの実効性

- **Core（TabManagerServiceTests.cs）**: `ReorderTabs_指定順にグループ内タブが並べ替わる` は `group.Tabs.Select(t => t.Id)` の実順を確認。`_並べ替えてもアクティブタブと選択タブが維持される` は `ActiveTabId`/`SelectedTabId`/`ActiveGroupId` の3点を実値検証。`_未知のIdは無視し未指定タブは末尾に元の順で残す`・`_存在しないグループはfalseを返す` も実質検証。形式的アサートなし。
- **ViewModel（TabGroupViewModelTests.cs）**: `MoveTab_先頭タブを末尾へ`・`末尾タブを先頭へ` は表示順と reorder 通知ペイロードの両方を検証。`同じ位置への移動は何もせず通知しない` は `false`・順序不変・通知空を検証（off-by-one の境界）。`並べ替えてもアクティブタブが維持される` は IsActive が同一タブ(t2)に残ることを検証。`SetDropIndicator_指定タブの片側だけ`・`ClearDropIndicators` でインジケータ状態を検証。
- D&D イベントハンドラ自体（DragOver/Drop の座標判定・クロスグループ拒否）は UI 層のため単体テスト対象外。SPEC のテスト要件は「タブ順序変更（モデル操作）」「並び替え後アクティブ維持」「GUI 評価」であり、前2者は単体テストで充足、GUI 評価は手動確認に委ねる構成。要件と整合。

### ビルド・テスト結果

- `dotnet build TabNest.slnx` : 成功（0 警告）
- `dotnet test TabNest.slnx` : Core 63 / Integration 16 / ViewModels 195 すべて合格（計274）、UiTests は環境スキップ（CI 既定）

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 2件・情報 2件）。

SPEC Task 7-1 の作業内容（D&D 基盤・同一グループ内並べ替え・挿入位置インジケータ・TabGroup.Tabs 順序更新と settings.json 反映・アクティブ/選択保持）・完了条件（並べ替え可能・アクティブ/選択不壊・再起動後順序保持）・テスト要件（モデル操作の単体・アクティブ維持の単体・GUI 評価対応）をすべて満たしている。MoveTab の off-by-one 補正は全代表ケースで正しく、D&D 状態管理にクリア漏れ・インジケータ残留なし、同一グループ限定も成立。MVVM 層分離・禁止参照なし・AutomationId 喪失なし。スコープ外の MainPage 修正は既存クラッシュ不具合の妥当な解消。Minor 指摘（クロスグループ明示チェック・クロスグループ拒否テスト）は 7-2 実装時の堅牢化提案であり、本タスクの機能・完了条件には影響しない。

## 指摘対応の判断（実装側）

- **Minor 1: クロスグループ判定の明示チェック追加** → 未対応（Task 7-2 で対応）。
  現状は `_draggingTab` が TabGroupRow インスタンスごとに独立し、他グループ起点のドロップは
  `_draggingTab is null` で DragOver/Drop ともに拒否されるため、7-1 の同一グループ限定は成立済み。
  グループ間移動は Task 7-2 のスコープであり、その実装時に明示的な所属チェックを導入する。
- **Minor 2: クロスグループ拒否のテスト追加** → 未対応（Task 7-2 で対応）。
  クロスグループの受け入れ/拒否は 7-2 で機能追加されるため、テストも 7-2 で追加するのが適切。
- 情報 2件（堅牢化メモ）も同様に 7-2 実装時の参考とする。本タスクでは対応不要。
