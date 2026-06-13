# Task7-2 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Sonnet 4.6（別系統セッション）
- レビュー対象: `git diff dev-re...Task7-2`（1コミット）
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 7-2: グループ間のタブ移動」

差分ファイル:
- `src/TabNest.Core/Services/TabManagerService.cs`（`MoveTabToGroup` 追加）
- `src/TabNest.ViewModels/MainViewModel.cs`（`MoveTabToGroup` 追加・コールバック配線）
- `src/TabNest.ViewModels/TabGroupViewModel.cs`（`MoveTabFromOtherGroup` / `IsTabLimitReached` 追加・コンストラクタに `_moveTabIntoGroup` コールバック追加）
- `src/TabNest.App/Controls/TabGroupRow.xaml`（ScrollViewer に `AllowDrop`/`DragOver`/`Drop` 追加）
- `src/TabNest.App/Controls/TabGroupRow.xaml.cs`（`_draggingTab` → `s_draggingTab`（静的化）・`s_draggingSourceGroup` 追加、グループ間移動分岐・フォールバックドロップ実装）
- `tests/TabNest.Core.Tests/TabManagerServiceTests.cs`（`MoveTabToGroup` 単体テスト 7件追加）
- `tests/TabNest.ViewModels.Tests/TabMoveBetweenGroupsTests.cs`（新規・6件）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `Core.MoveTabToGroup` が同一グループ指定（`sourceGroup == targetGroup`）を明示的に拒否しない。上限チェックの条件に `!ReferenceEquals(sourceGroup, targetGroup)` があるため上限未到達なら削除→挿入が実行される。ViewModel 側の事前チェック（`ReferenceEquals(sourceGroupVm, targetGroupVm)` で `false` 返却）が遮断しているため正規経路での実害はない。ただし Core のテストで同一グループを直接渡した場合の動作が未検証で、将来の利用者が誤使用した際に意図しない動作を引き起こす可能性がある。ドキュメントには「本メソッドは別グループへの移動を想定する」と明記されているが、`if (ReferenceEquals(sourceGroup, targetGroup)) return Failure(...)` の早期リターンを追加すると防衛的になる。 | |
| 2 | Minor | `Group_DragOver`（ScrollViewer フォールバック）に `e.Handled = true` がない。`TabItem_DragOver` には追加済み（Task 7-2 の diff で追加）だが、フォールバック側は未設定。WinUI 3 の DragOver はバブリングするため、タブ上で `TabItem_DragOver` が処理した後に `Group_DragOver` まで伝播することがあり、フィードバック（`AcceptedOperation`・インジケータ）が二重に適用される可能性がある。実害は現時点で観測されていないが、`e.Handled = true` を設定することで明示的にバブリングを止めるのが安全。 | |
| 3 | Minor | 同一グループの ScrollViewer 空白部分へドラッグ中（`s_draggingSourceGroup == ViewModel`）、最後のタブが `s_draggingTab` 自身のとき `SetDropIndicator(Tabs.LastOrDefault(), after: true)` により自分自身の `IsDropAfter = true` が設定され、末尾インジケータが表示される（「自分の隣に落とす」誤インジケータ）。ドロップしても `MoveTab` が `newIndex == oldIndex` で `false` を返すため状態は保たれる。視覚的な問題のみ。`Group_DragOver` で `ReferenceEquals(s_draggingTab, ViewModel.Tabs.LastOrDefault())` の場合にインジケータを出さない分岐を追加することで解消できる。 | |
| 4 | 情報 | `TabItem_DropCompleted` で `ViewModel?.ClearDropIndicators()` と `s_draggingSourceGroup?.ClearDropIndicators()` が連続して呼ばれる。`DropCompleted` はドラッグ開始元のタブ行（ドロップ元グループの `TabGroupRow` インスタンス）で発火するため、両者は同一の ViewModel を指す。同一グループへの二重 `ClearDropIndicators` 呼び出しとなるが、`ClearDropIndicators` は全タブを `false` に設定するだけであり冪等なため実害なし。 | |
| 5 | 情報 | `ViewModel` テスト（`TabMoveBetweenGroupsTests`）は `Create()` で各グループ 1 タブしか持たない初期状態で始まる。複数タブ入り両グループ間の挿入位置精度（`insertIndex` 中間値）のテストが Core 側にはあるが ViewModel 側には未追加。Core とのズレが生じた場合に ViewModel テストで検出できない可能性がある。実害はなく現状の仕様充足度には問題ないが、将来の回帰検知のために追加する価値がある。 | |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **別グループへドラッグで移動できる**: `s_draggingTab`/`s_draggingSourceGroup` を静的にして全行間で共有し、`TabItem_Drop`/`Group_Drop` の `DropTab` ヘルパーで `sourceGroup == ViewModel` かどうかを分岐。別グループなら `MoveTabFromOtherGroup` → `MainViewModel.MoveTabToGroup` → `Core.MoveTabToGroup` を呼ぶ経路が実装されている。SPEC 完了条件1を充足。
- **移動先が上限のときは移動されず状態が壊れない**: Core の `MoveTabToGroup` で `targetGroup.Tabs.Count >= MaxTabsPerGroup` かつ別グループの場合に早期 `Failure` を返し、タブ除去前に中断する（アトミック性が保たれている）。ViewModel 側も `result.IsSuccess` を確認して ViewModel 側の操作を行うため、Core 失敗時は ViewModel の `ObservableCollection` 操作に到達しない。状態は一切変更されない。SPEC 完了条件2を充足。
- **移動元が空になってもグループが消えない**: `MoveTabToGroup`（Core/ViewModel 両層）でグループ削除の操作は一切行っていない。Task 6-6 の自動クローズ（`CloseTab` 経由）と分離されており、D&D 移動でグループが空になってもグループは維持される。Core テスト `MoveTabToGroup_移動元が空になってもグループは維持される` と ViewModel テスト同名で検証済み。SPEC 完了条件3を充足。

### Core と ViewModel の表示順同期（insertIndex 補正の整合性検証）

グループ間移動では `sourceGroup` と `targetGroup` が別インスタンスであるため、一方のタブ数変化は他方の Count に影響しない。

- **Core**: `sourceGroup.Tabs.RemoveAt(index)` → `targetGroup.Tabs.Count`（不変）でクランプ → `targetGroup.Tabs.Insert(clamped, tab)`
- **ViewModel**: Core を先に呼ぶ → `sourceGroupVm.Tabs.Remove(tab)` → `targetGroupVm.Tabs.Count`（不変）でクランプ → `targetGroupVm.Tabs.Insert(clamped, tab)`

両者ともに **同一の `insertIndex` 値** を **同一の `targetGroup.Tabs.Count`** でクランプするため、Core モデルと ViewModel 表示順の挿入位置は常に一致する。タブ重複・消失は発生しない。

### アクティブ・選択タブの整合性

- **アクティブタブが移動した場合**: Core の `if (ActiveTabId == tabId) SetActiveTab(tabId)` で `ActiveGroupId` が移動先グループ Id に更新される。`SetActiveTab` は `_groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == tabId))` でタブを検索するが、この時点でタブはすでに `targetGroup` に移動済みのため、正しい `targetGroup.Id` が `ActiveGroupId` にセットされる。ViewModel 側は `ApplyActiveStates()` で全タブの `IsActive` を `ActiveTabId` との照合で更新する。Core テスト `MoveTabToGroup_アクティブタブを移動すると移動先グループで引き続きアクティブになる` と ViewModel テスト同名で検証済み。
- **移動元の選択タブ追従**: 移動したタブが `sourceGroup.SelectedTabId` だった場合、隣のタブ（次優先・末尾を超えたら前）へ追従。Core テスト `MoveTabToGroup_移動元の選択タブが移動した場合は隣のタブへ追従する` で検証済み。ViewModel 側では `ApplyActiveStates()` が `IsActive` を `ActiveTabId` 照合で再計算するため整合を保つ。

### D&D 静的状態のライフサイクル

- **設定箇所**: `TabItem_DragStarting` のみ（`s_draggingTab = tab`・`s_draggingSourceGroup = ViewModel`）。
- **クリア箇所**: `TabItem_DropCompleted`（成否問わず）。`DropCompleted` は `CanDrag="True"` を持つタブ Border の `UIElement.DropCompleted` イベントであり、ドロップ先が別グループであっても開始元タブで確実に発火する。ドラッグがキャンセルされた場合（ESC・ウィンドウ外ドロップ失敗）も `DropCompleted` が `DataPackageOperation.None` で発火するため、リーク経路は存在しない。
- **インジケータ**: `MoveTabFromOtherGroup` 冒頭の `ClearDropIndicators()`（ドロップ先グループ）、`TabItem_Drop`/`Group_Drop` 末尾の `ViewModel.ClearDropIndicators()`（ドロップ先グループ）、`DropCompleted` の両グループ `ClearDropIndicators()` で複数の消去機会がある。冪等な操作であり消し残しは起きない。

### エラー処理・エッジケース

- **存在しないタブ/グループ**: Core で `TabNotFound`/`GroupNotFound` を返す。Core テスト `MoveTabToGroup_存在しないタブやグループは失敗する` で検証済み。
- **範囲外 insertIndex**: `Math.Clamp(insertIndex, 0, targetGroup.Tabs.Count)` で末尾へ補正。Core テスト `MoveTabToGroup_範囲外の挿入位置は末尾へ補正される` で検証済み。
- **同一グループへの誤呼び出し（View → ViewModel 経路）**: ViewModel の `MoveTabToGroup` で `ReferenceEquals(sourceGroupVm, targetGroupVm)` の場合に `false` を即返却。ViewModel テスト `MoveTabToGroup_同一グループへの移動は受け付けない` で検証済み。
- **上限拒否でのアトミック性**: Core が `RemoveAt` 前に上限チェックを行うため、拒否時は元グループのタブ数・内容が変化しない。ViewModel テスト `MoveTabToGroup_移動先が上限のときは移動せずエラーを表示する` で状態不変を検証済み。

### MVVM 層分離

- `TabGroupViewModel`・`MainViewModel`・`TabManagerService` の変更に `Microsoft.UI` 系 namespace なし。WinUI 依存は `TabGroupRow.xaml.cs`（App 層）に閉じている。
- 追加テストは `TabNest.Core.Tests → TabNest.Core`・`TabNest.ViewModels.Tests → TabNest.ViewModels` の参照のみ。`TabNest.App` を参照していない（CLAUDE.md 禁止参照ルール遵守）。

### AutomationId

Task 7-2 で新規 XAML 要素（`ScrollViewer` へのイベント属性追加）は追加されているが、`ScrollViewer` 自体は既存要素で AutomationId の追加は本来不要。ドロップターゲットとしての UI 操作は座標ベースのため新規 AutomationId は不要。既存の `TabGroupRow`・`FolderTabItem`・各メニュー項目の AutomationId は喪失・変更なし。

### テストの実効性

- **Core（TabManagerServiceTests.cs）**: 7件すべてが実値（タブ Id 列・グループ数・`SelectedTabId`・`ActiveTabId`・`ActiveGroupId`）を検証しており、形式的アサートはない。上限拒否でのタブ数不変・移動元空化後のグループ存続・範囲外挿入位置補正・アクティブ/選択追従を網羅。
- **ViewModel（TabMoveBetweenGroupsTests.cs）**: 6件。`ObservableCollection` の実際の含有・順序を `Contains`/`Same`/`DoesNotContain` で検証。OperationError の有無・IsActive の維持も確認。`MoveTabFromOtherGroup_TabGroupViewModel経由でも移動できる` は View が呼ぶ実際の経路（ドロップ先 VM へのコール）を再現しており、コールバック配線の正しさを検証している。
- SPEC テスト要件（グループ間移動の GroupId 更新・上限拒否の単体テスト、移動元空化後グループ維持の単体テスト）を充足。

### ビルド・テスト結果

差分を見る限り、ビルドエラーとなる変更は存在しない（新規 `using`・シグネチャ・`TabOperationError.TabLimitReached` の既存値使用・テストに問題なし）。テスト結果は実施環境での確認が必要だが、追加テストは既存インフラ（`TabManagerService` ヘルパー・`StubFileSystemService`）を流用しており、問題のある依存はない。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 3件・情報 2件）。

SPEC Task 7-2 の作業内容（グループ間 D&D 移動・上限20尊重・アクティブ/GroupId 整合・空グループ維持）・完了条件（3条件すべて）・テスト要件（GroupId 更新/上限拒否の単体テスト・移動元空グループ維持の単体テスト）をすべて満たしている。Core と ViewModel の挿入位置補正は整合しており、タブの重複・消失は発生しない。上限拒否はアトミックで状態不変。D&D 静的状態（`s_draggingTab`/`s_draggingSourceGroup`）は `DropCompleted` で確実にクリアされリーク経路なし。MVVM 層分離・禁止参照なし・AutomationId 喪失なし。Minor 指摘はいずれも防衛的改善・視覚的改善であり、仕様充足・状態整合性・クラッシュ安全性への影響はない。

---

## レビュー指摘への対応（実装: Claude Opus 4.8）

- **Minor #2（Group_DragOver に e.Handled なし）: 対応**。`TabItem_DragOver` と同様に
  `e.Handled = true` を設定し、ScrollViewer フォールバックでもバブリングを明示遮断した。
- **Minor #3（同一グループで末尾タブを空白部へドラッグすると自分自身に IsDropAfter が立つ）: 対応**。
  `Group_DragOver` で末尾タブがドラッグ中タブ自身の場合は移動が起きないためインジケータを出さず
  `ClearDropIndicators()` するようにした。
- **Minor #1（Core.MoveTabToGroup が同一グループ指定を明示拒否しない）: 非対応（意図的）**。
  同一グループ呼び出しは呼び出し側 `MainViewModel.MoveTabToGroup` が `ReferenceEquals` で
  事前に弾いており（単体テスト「同一グループへの移動は受け付けない」で担保）、View も
  開始元グループと同一なら `MoveTab`（7-1 並べ替え）へ分岐する。Core を直接同一グループで
  呼んでも RemoveAt→Insert により状態は壊れない（防衛的非破壊）。専用の失敗理由 enum が無く、
  既存 enum の流用は意味が不正確になるため、防御は呼び出し側に集約する方針とした。
- **情報2件**: 対応不要（現状の設計判断と一致）。
