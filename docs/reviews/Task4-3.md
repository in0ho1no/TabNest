# Task4-3 クロスモデルレビュー記録

- 実装モデル: Claude Fable 5
- レビューモデル: Claude Opus 4.8
- レビュー対象: `git diff dev-re...Task4-3`
- 実施日: 2026-06-12

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 情報 | 復元の単位整合性(WindowWidth/Height=物理px、LeftPaneWidth=DIP)は Task 4-2 の保存と一致しており良好(確認事項) | 対応不要 |
| 2 | 情報 | 左カラム既定値 220 が AppSettings と MainViewModel.DefaultLeftPaneWidth の2箇所に定義されている。動作上の不具合はないが将来の不整合リスク | 対応不要(現状維持)。AppSettings は SPEC データモデルの定義どおりとし、VM 側の補正値は SPEC「画面レイアウト」由来の定数として保持。両者は同じ SPEC 値を参照している |
| 3 | 情報 | タブ0個のグループも復元される(テスト未カバーの edge case。クラッシュはせず LoadInitialFolder がフォールバックする) | **対応済み**: 意図的な許容であることを RestoreSession の doc コメントに明記(全タブを閉じたグループは実行時に存在しうる正規の状態のため、保存→復元で保持する) |
| 4 | 情報 | ウィンドウサイズの上限クランプなし(モニタ解像度超の保存値もそのまま Resize)。AppWindow.Resize は位置を変えないためオフスクリーン化はしない | 対応不要(記録のみ)。マルチモニタ対応は v0.1 範囲外 |

Critical・Major・Minor の指摘なし。

## 確認済み項目(レビュアー所見)

- 層分離: 復元ロジック本体は Core(RestoreSession)/ ViewModels(MainViewModel)にあり WinUI 非依存。
  View は Resize・GridLength の適用のみ。テストから TabNest.App への参照なし
- 状態整合性: アクティブ決定は「保存 ActiveTabId → アクティブグループの SelectedTabId → 先頭グループ」の
  段階フォールバック。SetActiveTab 経由で ActiveGroupId / ActiveTabId / SelectedTabId / IsActive が一貫
- 上限処理: グループ5・タブ20・閉じたタブ履歴20 の切り捨てを実装・テスト済み。
  切り捨て後の追加が上限エラーになることまで検証
- エラー処理: ファイル無し・破損 JSON は初期起動状態へフォールバック。存在しないパスのタブは
  ErrorMessage 表示のみでクラッシュしない
- テスト品質: 不正値(0/負/NaN)・左カラム幅補正(220/150)・閉じたタブ復元(Ctrl+Shift+T 連携)の境界をカバー
- 新規 UI 要素なし(AutomationId 対象外)。仕様外拡張なし

## 実機確認

識別用セッション(2グループ・ウィンドウ 1100x700・左カラム 280)を settings.json に配置して起動し、
ウィンドウサイズが 1100x700 で復元されることを GetWindowRect で確認。正常終了後の settings.json が
復元したタブ構成・アクティブタブ・サイズと完全一致(復元→保存のラウンドトリップ一致)することを確認した。

## 結論

**マージ可(approve)。** 指摘3はコメント追記で対応済み。
