# Task5-2 クロスモデルレビュー記録

- 実装モデル: Claude Fable 5
- レビューモデル: Claude Opus 4.8
- レビュー対象: `git diff dev-re...Task5-2`
- 実施日: 2026-06-12

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 低 | FolderTreeView_Loaded で内部 TreeViewList が取れなかった場合に無言で AutomationId 未設定になる(タイミング・テンプレート変更で静かに壊れる) | **対応済み**: Debug.Assert を追加(回帰は ElementDiscoveryTests でも検出可能) |
| 2 | 低 | TabGroupRow / FolderTabItem / GroupNameText は複数インスタンスで同一 AutomationId になる。検索要件は先頭一致で満たせるが、特定の段・タブの操作には親子相対探索が必要 | 対応不要(本タスクの要件は充足)。後続の操作系 UI テストでは親 TabGroupRow → 子 で絞り込む方針とする |
| 3 | 低 | 前テストの Kill 失敗でゾンビプロセスが残る可能性(existingIds 除外により誤検出はしない。並列無効化済みで実害低) | 対応不要 |

Critical・Major の指摘なし。

## 実装上の判断(レビュアー承認済み)

- **RestoreClosedTabButton は付与しない**: SPEC の画面レイアウト(ナビゲーションバー)に
  復元ボタンは存在せず、Ctrl+Shift+T のみ。対象リストは「例」であり、ボタン新設は
  画面レイアウト仕様の変更になるためユーザー確認事項として報告する
- **MainWindow はウィンドウタイトルで識別**: WinUI 3 の Window は UIElement ではなく
  AutomationProperties を設定できない。UI テストで Driver.Title == "TabNest" を検証
- UIA に現れない要素への対処:
  - TabGroupRow: UserControl は既定でピアなし → OnCreateAutomationPeer で FrameworkElementAutomationPeer を生成
  - FolderTabItem: Border はピアなし → 内側の TextBlock に AutomationId を移動
  - FolderTreeView: TreeView 本体はピアなし → Loaded 時に内部 TreeViewList へ引き継ぎ
- UI テスト基盤の堅牢化(Task 5-1 フォローアップ):
  - 並列実行を無効化(同時セッション要求の競合防止)
  - AppSession を shell 活性化 + appTopLevelWindow アタッチ方式へ変更
    (app capability は無効 AUMID でエラーを返さず120秒ハングするため、
    起動失敗を明確な TimeoutException として切り分けられるようにした)

## トラブルシューティングの記録

UI テストが当初ハング・失敗した根本原因は、**旧トラックで設定された
ユーザー環境変数 TABNEST_UI_TEST_APP_ID に削除済みパッケージの AUMID が残留**していたこと。
WinAppDriver は無効な AUMID の活性化でエラーを返さずハングし、
「パラメーターが間違っています」ダイアログが表示される。
環境変数を削除し、README にトラブルシューティングとして追記した。

## 実機確認

- 直接 UIA(Inspect 相当)で主要12要素すべての AutomationId を検索できることを確認
- WinAppDriver(管理者起動)+ Appium で UI テスト3件すべて合格
  (空テスト・セッション確立・主要12要素の検索。実行時間 約4〜7秒)
- 全テストスイート 220 件グリーン

## 結論

**マージ可(approve)。** 指摘1は Debug.Assert 追加で対応済み。
