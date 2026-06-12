# Task5-4 クロスモデルレビュー記録

- 実装モデル: Claude Fable 5
- レビューモデル: Claude Opus 4.8
- レビュー対象: `git diff dev-re...Task5-4`
- 実施日: 2026-06-12

## 指摘一覧と対応

| # | 重要度 | 指摘 | 対応 |
|---|--------|------|------|
| 1 | 低 | NavigateTo の完了判定はドライブ直下(D:/ 等)を渡すとフォルダ名が空になり誤判定しうる。現状は SampleFolder 固定で実害なし | 対応不要(将来ドライブ直下を扱う場合にガード追加を検討) |
| 2 | 低 | MiddleClick / SendShortcut の固定 Sleep はフォアグラウンド化の反映待ちとして経験的に機能。遅い環境で理論上の余地あり | 対応不要(不安定化したらポーリングへ置換) |
| 3 | 情報 | 失敗メッセージ生成時の FindTabs 再評価は失敗パスのみで実害なし | 対応不要 |
| 4 | 情報 | PathTextBox 参照のタブ切替後再利用は、要素が再生成されずバインド更新のみのため有効(設計確認) | 対応不要 |

Critical・Major の指摘なし。

## 実装上の判断(レビュアー承認済み)

- 中クリックは Win32(SetCursorPos + mouse_event)の物理クリックで実装
  (WinAppDriver に中クリック API がないため)。**WinAppDriver の要素座標は
  ウィンドウ可視矩形(見えないリサイズ枠を除く)からの相対値**であることを REST + UIA の
  実測で確認し、DWM 拡張フレーム境界(DWMWA_EXTENDED_FRAME_BOUNDS)を起点に絶対座標へ変換
- JIS 配列対策: SendKeys のパスは「\」→「/」変換し、入力結果を検証して最大3回リトライ
- ショートカットは旧 Keyboard API でなく Actions を使用。修飾キーはトグルのため
  modifier + key + modifier 形式(複合修飾 Ctrl+Shift も同様)
- ナビゲーション完了はタブタイトル(移動先フォルダ名 = MainViewModel.GetTabTitle と同一ロジック)の
  更新で判定(アドレスバーは入力値と移動後表示が同一のため完了シグナルにならない)
- Tab_Select_Should_Show_Selected_Tab_Folder は作業内容「タブ選択をGUI操作で検証する」に対応する追加
- サンドボックス遵守: 実フォルダ参照は TestFixtures/SampleFolder(ビルド出力コピー)のみ。
  ファイル削除・コピー・移動の操作なし

## 実機確認

WinAppDriver + Appium でタブ操作テスト5件すべて合格:
- AddTab_Should_Create_New_Tab(ボタンで追加・%UserProfile% 表示)
- CtrlT_Should_Add_Tab_With_UserProfile
- Tab_Select_Should_Show_Selected_Tab_Folder(2タブ間の切替で表示フォルダが追従)
- MiddleClick_Tab_Should_Close_Tab
- CtrlShiftT_Should_Restore_Closed_Tab(復元タブがアクティブになり元のパスを表示)

UI テスト全10件(54秒)・全スイート 227 件グリーン。

## 結論

**マージ可(approve)。** 対応必須の指摘なし。
