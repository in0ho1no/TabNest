# Task4-2 クロスモデルレビュー記録

- 実装モデル: Claude Fable 5
- レビューモデル: Claude Opus 4.8
- レビュー対象: `git diff dev-re...Task4-2`
- 実施日: 2026-06-12

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 低 | AppWindow.Size は物理ピクセル、LeftPaneColumn.ActualWidth は DIP で単位が混在。保存自体は問題ないが、Task 4-3 の復元で単位を取り違えると高 DPI 環境でズレる | **対応済み**: MainWindow_Closed に保存単位と復元方法(AppWindow.Resize / ColumnDefinition.Width)のコメントを追記。復元は Task 4-3 で同単位を遵守する |
| 2 | 低 | MainWindow が具象 SettingsService を直接 new(DI 不在)。保存トリガが View ライフサイクル(Closed)に紐づくため View が握るのは合理的でもある | 対応不要(現状維持)。DI 一貫性が必要になった時点で App 起点の注入を検討 |
| 3 | 情報 | CreateAppSettings の ToList() は浅いコピーで TabManagerService 内のライブ参照を共有。終了時1回・同期 Save のため実害なし | 対応不要。非同期保存等に拡張する場合のみディープコピーを検討 |
| 4 | 情報 | マルチウィンドウ非考慮(最後に閉じたウィンドウの状態のみ残る)。SPEC は単一ウィンドウ前提のため仕様逸脱ではない | 対応不要(記録のみ) |

Critical・Major の指摘なし。

## 確認済み項目(レビュアー所見)

- MVVM 層分離が正しい: WinUI 依存値(ウィンドウサイズ・左カラム幅)を View 側で取得し
  `MainViewModel.CreateAppSettings(double, double, double)` に注入。Core/ViewModels は WinUI 非依存を維持
- テストは実装の正しさを実際に検証: フォルダ移動後の Path/Title 反映、タブ・グループ追加、
  閉じたタブ履歴(GroupId/TabIndex 含む)、アクティブタブ追従まで状態整合をカバー
- エラー処理: SettingsService.Save は例外を握って bool を返すため終了時クラッシュなし。
  左カラム幅のレイアウト未確定フォールバックも適切
- 仕様外拡張なし: SavedGroups は空のまま(Task 4-4 範囲)、戻る・進む履歴も保存対象外を遵守。
  UI 要素の追加なし(AutomationId 対象外)。依存追加・テストからの App 参照なし
- 保存対象6項目(タブ状態・ウィンドウサイズ・左カラム幅・タブグループ・アクティブタブ・閉じたタブ履歴)すべて充足

## 実機確認

アプリ起動 → ウィンドウを正常終了(WM_CLOSE)→ settings.json が生成され、
タブグループ・アクティブタブ・ウィンドウサイズ・左カラム幅が保存されることを確認した。
パッケージ実行のため実ファイルは
`%LocalAppData%\Packages\<PFN>\LocalCache\Roaming\TabNest\settings.json` に仮想化される
(コード上の保存先は SPEC どおり `%AppData%\TabNest\settings.json`)。

## 結論

**マージ可(approve)。** 指摘1は対応済み。
