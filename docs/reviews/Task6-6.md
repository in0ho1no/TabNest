# Task6-6 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Sonnet 4.6
- レビュー対象: `git diff dev-re...Task6-6`
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 6-6: グループ内の全タブを閉じたときのグループ自動クローズ」

差分ファイル:
- `src/TabNest.Core/Services/TabManagerService.cs`（CloseTab に最後の1タブガード追加・空グループ自動クローズ追加・RemoveGroupCore プライベートメソッドへ抽出）
- `src/TabNest.ViewModels/MainViewModel.cs`（CloseTab で Core 自動クローズ後の GroupVm 除去追加）
- `tests/TabNest.Core.Tests/TabManagerServiceTests.cs`（既存テスト更新・Task 6-6 単体テスト 5件追加）
- `tests/TabNest.Core.Tests/ClosedTabTests.cs`（最後の1タブガード回避用 keep タブ追加・RestoreClosedTab_TabIndexが範囲外 テスト再設計）
- `tests/TabNest.ViewModels.Tests/TabCloseTests.cs`（既存テスト更新・グループ自動クローズ検証テスト追加）
- `tests/TabNest.ViewModels.Tests/ShortcutCloseActiveTabTests.cs`（既存テスト更新）
- `tests/TabNest.UiTests/Tests/TabOperationTests.cs`（UI テスト 2件追加）

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | UI テスト `MiddleClick_App_Last_Tab_Should_Be_Rejected` 内で `Assert.Equal(1, UiActions.FindTabs(session).Count)` と `Assert.Equal(1, UiActions.FindGroupRows(session).Count)` を使用している。xUnit アナライザが `xUnit2013: Do not use Assert.Equal() to check for collection size. Use Assert.Single instead.` を 2件報告している（ビルド警告）。実害はないが他テストコードとの一貫性のために `Assert.Single(...)` に変更が望ましい。 | （未対応） |
| 2 | Minor | `CloseTab_複数グループでも全体最後の1タブは閉じられない` テストは「D&D 等で生じ得る空グループが存在する状況」を検証しているが、実際には AddGroup が常に初期タブ1個付きで追加する（SPEC「新規グループには %UserProfile% を開く初期タブを1個作成する」）。そのため `AddGroup` を呼び出した後にタブを全て削除するか、または RestoreSession 経由で空グループを注入しない限りこの状態は発生しない。テストは直接 `new TabGroup {}` を使わず AddGroup ヘルパで空グループを作っているため、テスト手法（D&D 後の状態を AddGroup+CloseTab で模擬）と状態の実際の発生経路の間に齟齬がある。ただし `_groups.Sum(g => g.Tabs.Count) <= 1` ガードはそのシナリオで正しく機能するため、ロジック自体は問題ない。 | （対応不要） |
| 3 | 情報 | `RemoveGroupCore` の「削除したグループがアクティブだった場合は先頭の残存グループ（`_groups[0]`）の選択タブをアクティブにする」という挙動は、SPEC「グループ削除（Task 6-1 / RemoveGroup）と同じ規則に従う」を満たしている。ただし RemoveGroup は「隣接グループへ追従する」と説明を読み得るが、実装は `_groups[0]`（先頭固定）である。これは元の RemoveGroup が抽出前から先頭グループ選択であり、Task 6-6 の自動クローズ時も同じ挙動が踏襲されていることを確認した。回帰なし。 | （対応不要） |
| 4 | 情報 | `MainViewModel.CloseTab` で、Core の `CloseTab` 成功後にまず `groupVm?.Tabs.Remove(tab)` を呼び、その後 `_tabManager.Groups.All(g => g.Id != groupVm.Id)` で Core 側にグループが残っているかを確認する設計になっている。この順序（先に tab を除去してから空かどうかを Core で判定）は正しい。Core が空グループを自動クローズした場合にのみ ViewModel 側もグループ VM を除去するため、二重除去は起きない。 | （対応不要） |
| 5 | 情報 | `ClosedTabTests.cs` の `RestoreClosedTab_TabIndexが範囲外なら末尾に追加される` テストを `RestoreSession` 経由で空グループ＋閉じたタブ履歴を直接注入する形に再設計している。Task 6-6 以降 `CloseTab` では空グループが作れなくなったために必要な変更であり、コメントにも理由が明記されている。適切な対応。 | （対応不要） |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **グループ内の最後のタブを閉じると空グループも自動クローズ（完了条件1）**: `CloseTab` でタブ除去後 `group.Tabs.Count == 0 && _groups.Count > MinGroups` のとき `RemoveGroupCore(group)` を呼ぶ実装を確認。SPEC「空になったグループも自動的に閉じる」を充足。`MainViewModel.CloseTab` でも Core 状態を確認して対応グループ VM を除去しており、表示側の整合性も維持される。
- **最後の1タブは閉じられない（完了条件2）**: `_groups.Sum(g => g.Tabs.Count) <= 1` ガードを `CloseTab` の先頭で確認、`false` を返して状態を変更しない。タブ除去前にガードがかかるため ClosedTab 履歴への積み込みも発生しない（SPEC「拒否した閉じる操作は ClosedTab へ積まない」を充足）。
- **アクティブグループの最後のタブを閉じたとき別グループへフォーカス移動（完了条件3）**: `RemoveGroupCore` 内で `ActiveGroupId == group.Id` の場合 `_groups[0]` の `SelectedTabId` をアクティブに切り替える。Core テスト `CloseTab_アクティブグループの最後のタブを閉じると別グループへフォーカスが移る` で動作を検証済み。
- **D&D で空になったグループは対象外（完了条件4）**: 自動クローズは `CloseTab` メソッド内でのみ発動し、将来実装の `MoveTab`（Task 7-2）は別メソッドになるため干渉しない。コメントにも「タブを閉じる操作に限定。D&D 移動は対象外」と明記されている。

### 自動クローズ後の ClosedTab 履歴

- SPEC「自動クローズで消える最後のタブも ClosedTab へ積む（グループ明示削除とは異なる）」を確認。`CloseTab` ではタブを `_closedTabs` に追加してから空グループ判定をするため、自動クローズされるタブも必ず履歴に積まれる。`CloseTab_自動クローズで消える最後のタブもClosedTabへ積む` テストで検証済み。

### RemoveGroupCore 抽出によるリグレッション検証

- 既存の `RemoveGroup` は抽出前「`_groups.Remove(group)` → アクティブ追従」の 2ステップを持っていた。抽出後は同じロジックが `RemoveGroupCore` に委譲されており、`RemoveGroup` の動作は変化していない。
- `RemoveGroupCore` は `private` で `CloseTab` と `RemoveGroup` からのみ呼ばれ、呼び出し側でそれぞれ下限チェック（`MinGroups`）を実施してからコアを呼ぶ設計になっており、不正な呼び出しパスは存在しない。
- コメントに「呼び出し側で下限(MinGroups)を確認してから使う」と記載があり、契約が明示されている。

### エッジケース

- **非アクティブグループの最後のタブを閉じた場合**: `RemoveGroupCore` の `ActiveGroupId == group.Id` 分岐では非アクティブグループを削除しても `ActiveGroupId` / `ActiveTabId` を変更しない。アクティブ追従の規則（アクティブでなければ現状維持）は RemoveGroup と一致する。`CloseTab_グループ内の最後のタブを閉じるとそのグループも閉じる` テストは group2 の最後のタブを閉じるシナリオで group1 がアクティブ継続となることを暗黙に検証（`service.Groups` が `[group1.Id]` のみになることを確認）。ただし、明示的に「非アクティブグループの最後のタブを閉じたときアクティブは変わらない」テストはない（Minor レベルの網羅性不足だが完了条件範囲外）。
- **複数グループが同時に空になるケース**: 通常 `CloseTab` はタブを 1 個ずつ閉じるため、1 回の呼び出しで複数グループが空になることはなく、問題なし。

### ViewModel と Core の状態同期

- `MainViewModel.CloseTab` は `_tabManager.CloseTab` の結果を先に受け取り、失敗時は VM 側を一切変更しない。Core が成功した場合にのみ VM の `groupVm.Tabs.Remove(tab)` → グループ VM 除去 → `ApplyActiveStates()` の順で状態を更新する設計。ロールバックが不要な単方向の更新フローになっており、整合性を保つ設計として適切。

### MVVM 層分離

- `src/TabNest.ViewModels/MainViewModel.cs` の変更は既存 using のみ（`Microsoft.UI` 系追加なし）。WinUI 非依存を確認。
- `tests/TabNest.ViewModels.Tests/TabCloseTests.cs` の `ProjectReference` は `TabNest.ViewModels` のみ。`TabNest.App` への参照なし。CLAUDE.md 禁止参照ルールを遵守。

### テストの実効性

- **Core 単体テスト（TabManagerServiceTests.cs）**:
  - `CloseTab_グループ内の最後のタブを閉じるとそのグループも閉じる`: `service.Groups.Select(g => g.Id).ToArray()` で残存グループを実値確認。形式的アサートなし。
  - `CloseTab_自動クローズで消える最後のタブもClosedTabへ積む`: `service.ClosedTabs` にパスが含まれることを `Assert.Contains` で確認。
  - `CloseTab_アクティブグループの最後のタブを閉じると別グループへフォーカスが移る`: `ActiveGroupId`・`ActiveTabId` の両方を実値で確認。
  - `CloseTab_グループ1つタブ1つのときは閉じられず状態が変わらない`: `false` 返却・タブ維持・グループ維持・ClosedTabs 空の 4点を確認。
  - `CloseTab_複数グループでも全体最後の1タブは閉じられない`: 空グループ共存シナリオで `Sum(Tabs.Count) <= 1` ガードが正しく機能することを確認。
- **ViewModel 単体テスト（TabCloseTests.cs・ShortcutCloseActiveTabTests.cs）**:
  - 既存テスト名を「最後のタブを閉じてもクラッシュしない」から「アプリ内最後の1タブは閉じられない」へ適切にリネーム・期待値を `true`→`false` に修正。仕様変更が正しく反映されている。
  - `CloseTab_グループの最後のタブを閉じると空グループも閉じる`: `Assert.DoesNotContain(group2, vm.Groups)` で ViewModel から除去されたことを確認。
- **UI テスト（TabOperationTests.cs）**:
  - `MiddleClick_Last_Tab_Of_Group_Should_Close_Group`: `WaitForGroupCount(session, 1)` / `WaitForTabCount(session, 1)` でポーリング検証。実際の UI 操作を通した統合確認として適切。
  - `MiddleClick_App_Last_Tab_Should_Be_Rejected`: for ループ 5回の繰り返し検証は実質的なポーリングではなく、操作後に状態が変わらないことの多重確認。意図は明確だが、`Thread.Sleep` 等の遅延なしのループは連続呼び出しになるため、非同期更新が生じる UI では偽陽性リスクがある。ただし「閉じる操作を拒否」は同期的に即座に確定するため実害はない。

### ビルド・テスト結果

- `dotnet build TabNest.slnx` : 成功（警告 2件。xUnit2013: `Assert.Equal(1, ...)` を `Assert.Single(...)` に変更推奨）
- `dotnet test TabNest.slnx` : 単体テスト 42件すべて合格（Core 26件・ViewModels 16件）

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 2件・情報 3件）。

SPEC Task 6-6 の作業内容（最後の1タブガード・空グループ自動クローズ・RemoveGroupCore 抽出による規則共有・ClosedTab 履歴への積み込み）・完了条件（空グループ非残存・最後の1タブ拒否・アクティブ追従・D&D 対象外）・テスト要件（単体テスト各ケース・GUI 評価対応の UI テスト）をすべて満たしている。状態整合性・MVVM 層分離・禁止参照なし・回帰リスクなし、いずれも適正。Minor 指摘（xUnit 警告・エッジケーステストの齟齬）は機能に影響しない。

## レビュー指摘への対応（実装者: Claude Opus 4.8）

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | UI テスト `MiddleClick_App_Last_Tab_Should_Be_Rejected` の `Assert.Equal(1, ...)` が xUnit2013 警告 | **対応済み**。`Assert.Single(...)` へ変更（検証内容は同一、警告解消）。 |
| 2 | Minor | `CloseTab_複数グループでも全体最後の1タブは閉じられない` で `AddGroup` が初期タブ付きで空グループ生成経路に齟齬 | **未対応（指摘は誤認）**。Core の `TabManagerService.AddGroup` は初期タブを作らず空グループを生成する（初期タブを付けるのは ViewModel 層の `AddGroupWithDefaultTab`）。本テストの「タブ0個の空グループ」設定は正しい。 |

Minor #1 修正後、Core/ViewModels 単体テスト全件グリーン・UI テスト TabOperationTests 8件グリーンを再確認。
