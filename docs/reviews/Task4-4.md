# Task4-4 クロスモデルレビュー記録

- 実装モデル: Claude Fable 5
- レビューモデル: Claude Opus 4.8
- レビュー対象: `git diff dev-re...Task4-4`
- 実施日: 2026-06-12

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 情報 | 「存在しないパス」結合テストの配置先が Integration.Tests ではなく ViewModels.Tests。MainViewModel/FolderViewModel の検証が必要で、Integration.Tests は ViewModels 非参照のため物理的に配置不可。判断は妥当(確認事項) | 対応不要 |
| 2 | 情報 | Paths が空のお気に入り(破損 settings.json 由来)を開くとタブ0個の段が作られる。クラッシュせず実害は限定的 | 対応不要(SPEC 要件外)。Task 4-6 の UI 接続時に必要なら検討 |
| 3 | 情報 | MainViewModel.RemoveFavorite が成功時に OperationError をクリアせず、直前のエラーメッセージが残留しうる | **対応済み**: 削除成功時に OperationError = null を設定(他のお気に入り操作と整合) |
| 4 | 情報 | Favorites プロパティがライブ参照の passthrough のため変更通知がなく、UI が自動更新されない | 対応不要(お気に入りの UI 表示・バインディングは Task 4-6 の範囲。その際に ObservableCollection 化等を検討) |

Critical・Major・Minor の指摘なし。

## 確認済み項目(レビュアー所見)

- 連番アルゴリズム: 完全一致判定(StringComparer.Ordinal)・2始まり・「S」「S (k)」集合に対する
  未使用最小値・ベース名抽出なしを正しく実装。欠番再利用・「作業1 (2)」→「作業1 (2) (2)」もテストで実証
- スナップショット性: Paths は値コピー。元グループ改変後も不変なことをテストで実証
- 拒否ケースの状態整合性: タブ0個・上限50件・5段上限とも状態を変更せず失敗結果を返す
- 開く処理: お気に入り名の引き継ぎ・先頭タブアクティブ・再オープンは毎回別の段(新 Id)
- settings.json 連携: CreateAppSettings に SavedGroups を含め RestoreSavedGroups で復元。
  タブ状態の復元失敗(初期起動フォールバック)時もお気に入りは保持
- 層分離: FavoritesService は Core、VM 追加分は ViewModels で WinUI 非依存。
  テストから TabNest.App への参照なし
- 存在しないパス: 実 FileSystemService の結合テストで、欠落パスタブがアクティブ+ErrorMessage 表示、
  他タブは正常動作(クラッシュなし)を実証
- 新規テスト20件を含む全198件(当時)合格、回帰なし

## 結論

**マージ可(approve)。** 指摘3は対応済み。
