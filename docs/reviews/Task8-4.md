# Task8-4 クロスモデルレビュー記録

- 実装モデル: Claude Opus 4.8
- レビューモデル: Claude Opus 4.8（別系統セッション・クロスモデルレビュア）
- レビュー対象: `git diff dev-re...Task8-4`
- 実施日: 2026-06-14

## 対象範囲

SPEC.md「Task 8-4: フォルダのホイールクリックで新規タブを開く」（行 2205）／「主要機能 > ファイル一覧」「初期リリース範囲 > v0.3」

差分ファイル:
- `src/TabNest.ViewModels/MainViewModel.cs`（`OpenFolderInNewTab(FileItemViewModel)` 追加）
- `src/TabNest.App/MainPage.xaml`（ファイル行 Grid に `Background="Transparent"` と `PointerPressed="FileItem_PointerPressed"` を追加）
- `src/TabNest.App/MainPage.xaml.cs`（`FileItem_PointerPressed` ハンドラ追加）
- `tests/TabNest.ViewModels.Tests/OpenFolderInNewTabTests.cs`（新規・4 ケース）

## 外部コンテンツの扱い

`git diff` / `git log` の出力にインジェクション様パターンは検出されなかった。

## 指摘一覧

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | Minor | `OpenFolderInNewTab` はグループ名のインライン編集中（`IsRenameInProgress=true`）でも実行され、新規タブを追加してしまう。`AddTabToActiveGroup` / `AddGroupWithDefaultTab` は先頭で `IsRenameInProgress` ガードを置くが本メソッドは置いていない。ただし中クリック対象はファイル一覧の行（フォーカス外のグループ名編集を中クリック1回で抜けてからの追加になりうる）であり、SPEC は編集中抑制を Task 8-4 の要件に挙げていないため必須ではない。一貫性を取るなら先頭に `if (IsRenameInProgress) return false;` を追加する案がある。 | |
| 2 | 情報 | View 側ハンドラ `FileItem_PointerPressed` は `item.IsDirectory` を判定したうえ、さらに VM 側 `OpenFolderInNewTab` も `IsDirectory` を再判定する二重ガード。doc コメントに意図を明記済み。MVVM 観点で VM 側が単体で正しく振る舞う（View を信頼しない）設計として妥当。 | |
| 3 | 情報 | ファイル行 Grid は `PointerPressed` のみ追加で AutomationId 追加要素なし。既存の `FileListView` AutomationId を維持。行自体は従来から個別 AutomationId 非付与（UIA は ListViewItem 経由）で、本変更で付与漏れは発生しない。 | |
| 4 | 情報 | `Background="Transparent"` の追加は中クリックの当たり判定確保のため（透明領域は既定でヒットテスト対象外）。Task 8-3 のお気に入り行は別途背景ブラシを持つため不要だったが、ファイル行は背景未設定のため本追加は妥当かつ最小。 | |

## 確認済み項目（レビュアー所見）

### SPEC 作業内容・完了条件

- **フォルダ中クリックで新規タブで開く（作業内容1・完了条件1）**: `FileItem_PointerPressed` が `e.GetCurrentPoint(element).Properties.IsMiddleButtonPressed` を判定し、フォルダのとき `OpenFolderInNewTab` を呼び `e.Handled=true`。SPEC 実装ノート「PointerPressed + IsMiddleButtonPressed」に準拠。Task 8-3 の `FavoriteItem_PointerPressed` と同一パターン。テスト `フォルダの中クリックでアクティブグループ末尾に新規タブが開く` が `Tabs.Count==2`・末尾タブの `Path`/`Title`/`IsActive`・`Folder.CurrentPath` を検証。充足。
- **アクティブグループ末尾に追加しそのフォルダを表示（作業内容2）**: `OpenFolderInNewTab` は `_tabManager.ActiveGroupId` を取得し既存 `AddTab(groupId, item.FullPath)` を呼ぶ。`AddTab` は `groupVm.Tabs.Add` で末尾追加、`ApplyActiveStates` で追加タブをアクティブ化、`Folder.AttachHistory`＋`Folder.ShowFolder(tabVm.Path)` で内容表示。テスト `新規タブは現在アクティブなグループへ追加される` が2段目アクティブ時に2段目末尾へ入り1段目が不変であることを検証。充足。
- **タブ上限20を尊重・到達時はエラー表示（作業内容2かっこ書き・完了条件2）**: 上限判定は既存 `_tabManager.AddTab`（`MaxTabsPerGroup`）が担い、失敗時 `AddTab` 内で `OperationError = result.ErrorMessage` を設定し `null` を返す→`OpenFolderInNewTab` は `false`。テスト `タブ20個到達時は追加されず状態が壊れない` が `Tabs.Count==MaxTabsPerGroup` 維持・対象パス未追加・`OperationError` 非 null を検証。充足。
- **ファイル中クリックは何もしない（作業内容3）**: `OpenFolderInNewTab` 先頭で `!item.IsDirectory` なら即 `false`（`OperationError` も触らない）。View 側も `item.IsDirectory` 条件で `OpenFolderInNewTab` 自体を呼ばない。テスト `ファイルの中クリックでは何もしない` が `Tabs` 単一維持・`OperationError` null を検証。充足。
- **既存ダブルクリック移動は従来どおり（完了条件3）**: ダブルクリック経路 `FileListView_DoubleTapped → Folder.OpenItemCommand` は無改変。`FileItem_PointerPressed` は中ボタン押下時のみ `e.Handled=true` とし、左クリック/ダブルクリック/選択は `Handled` 未設定で従来どおり ListView へ伝播。リグレッションなし。

### 状態整合性

- 成功経路は既存 `AddTab` を流用し、表示中パス（`Folder.ShowFolder`）・タブ一覧（`Tabs.Add`）・アクティブ状態（`ApplyActiveStates`）・履歴（`AttachHistory`）・`OperationError=null` を一括更新。表示中パスとアクティブタブが常に一致する。
- 失敗経路（上限・グループ未取得）は `Tabs` に追加されず、上限時は `OperationError` がセットされ巻き戻り済みの状態が露出する。テストで状態非破壊を確認済み。
- フォルダ存在しない/アクセス拒否のケース: `AddTab` はタブを追加し `Folder.ShowFolder` で内容表示を試みるが、存在しないパスの扱いは既存 `ShowFolder`/`FolderListingResult` のエラー表示経路に委譲され、本タスクで新たなクラッシュ経路は導入していない（ダブルクリック移動・お気に入り起動と同じ表示ロジック）。

### テストの実効性

- `OpenFolderInNewTabTests`（4件）はフォルダ成功（パス・タイトル・アクティブ・表示中パス・エラーなし）、ファイル無視、アクティブグループ選択（多段）、上限拒否（状態非破壊・エラー設定）を実値で検証しており形式的でない。SPEC テスト指定「パス・挿入位置・上限拒否」を直接カバー。
- 上限テストは `TabManagerService.MaxTabsPerGroup` 定数を参照し、マジックナンバー 20 のハードコードを避けている。
- 実環境で `dotnet test`（ViewModels.Tests・`OpenFolderInNewTab` フィルタ）4件すべて成功（失敗 0）。
- 中クリック検出自体（PointerPressed）は SPEC 指定どおり GUI 手動評価に委ねる旨をテストクラス/ハンドラ doc に明記。妥当な切り分け。

### MVVM 層分離・禁止参照

- `OpenFolderInNewTab` は `MainViewModel`（WinUI 非依存）に集約。引数の `FileItemViewModel`・`FolderTabViewModel` も WinUI 非依存。WinUI 型（`PointerRoutedEventArgs` 等）は `MainPage.xaml.cs`（App 層）に閉じている。
- 追加テストは `TabNest.ViewModels.Tests → TabNest.ViewModels` 参照のみ。`TabNest.App` 参照の追加なし。CLAUDE.md 禁止参照に抵触しない。

### Task 8-3 との一貫性

- View 側ハンドラのシグネチャ・中ボタン判定・`e.Handled=true`・doc コメントの書き方が `FavoriteItem_PointerPressed` と揃っている。
- VM 側は既存の追加ロジック（お気に入りは `OpenFavorite`、本件は `AddTab`）を流用し再実装しない方針も一致。
- Task 8-3 は編集中（`IsEditingName`）ガードを持つが、ファイル行は v0.3 でインライン編集を持たない（`FileItemViewModel` に編集状態プロパティなし）ため、行レベルでの編集ガードは不要。指摘#1 はグループ名編集（`IsRenameInProgress`）との一貫性の観点のみ。

### 仕様外変更の有無

差分は (1) フォルダ中クリック=新規タブで開く VM メソッド追加、(2) ファイル行への中クリックハンドラ結線、(3) 当たり判定用の透明背景、(4) 単体テスト追加に限られ、いずれも SPEC Task 8-4 に直接対応。仕様外の機能追加・挙動変更は含まれない。

## 結論

**approve（マージ可）。** Critical / Major の指摘なし（Minor 1件・情報 3件）。

SPEC Task 8-4 の作業内容（フォルダ中クリック=新規タブで開く・アクティブグループ末尾に追加して表示・上限20尊重とエラー表示・ファイルは無視）と完了条件3項目（中クリックで開く・上限到達時に状態非破壊・既存ダブルクリック移動が従来どおり）をすべて満たす。成功経路は既存 `AddTab` を流用し表示中パス/タブ一覧/アクティブ/履歴/エラーの整合が取れ、失敗経路は状態非破壊。中ボタン時のみ `e.Handled=true` でダブルクリック移動・選択にリグレッションなし。MVVM 層分離・禁止参照に問題なし。ViewModel テスト4件グリーン。Minor #1（編集中の `IsRenameInProgress` ガード未配置）は SPEC 要件外かつ実害軽微で必須ではないが、他の Add 系メソッドとの一貫性として検討余地あり。View 層（中クリック検出・透明背景の当たり判定）は SPEC 指定どおり GUI 手動評価に委ねる。

## 対応判断（実装モデル記入欄）

- 判定:
- Minor #1（`IsRenameInProgress` ガード未配置）:
- 情報指摘（3件）:

---

## 指摘対応の判断（実装者: Claude Opus 4.8）

- [Minor] `OpenFolderInNewTab` の `IsRenameInProgress` ガード追加 → **未対応（意図的）**。
  AddTabToActiveGroup / AddGroupWithDefaultTab の同ガードは、グループ名インライン編集中に
  キーボードショートカット(Ctrl+T/Ctrl+G)がキー入力を奪わないための抑制であり、
  本件はファイル一覧上の中クリックという独立したポインタ操作のため趣旨が異なる。
  編集中に別領域を中クリックした場合は LostFocus で編集が確定する自然な挙動になり、
  SPEC Task 8-4 の要件外でもあるため、スコープを広げず現状維持とする。
