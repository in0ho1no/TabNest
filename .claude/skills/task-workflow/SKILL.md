---
name: task-workflow
description: dev-re トラックで SPEC.md の TaskX-Y を 1 件、最初から最後まで（ブランチ作成→実装→テスト→GUI評価→クロスモデルレビュー→マージ→push→メモリ更新）進めるときの標準手順チェックリスト。/clear+メモリ運用で各タスクを新セッションで進める前提。正本のルールは AGENTS.md。
---

# タスク標準フロー（dev-re トラック）

SPEC.md の Task を 1 件こなす標準手順。**コスト最適化（サブエージェント委譲・要約返し）を織り込み済み**。
ルールの正本は `AGENTS.md`、仕様の正本は `SPEC.md`。矛盾があれば止めてユーザーに確認。

## 手順

1. **着手準備（メインは痩せたまま）**
   - SPEC.md の該当 Task と関連節（画面レイアウト・主要機能・データモデル）を読む。
   - 関連メモリ（MEMORY.md・該当ファイル）を確認。重い探索が要れば `Explore`/Haiku サブに委譲。
   - `git checkout -b TaskX-Y dev-re` で作業ブランチを作成。

2. **実装**
   - SPEC に書かれた範囲のみ実装（勝手な拡張をしない）。
   - 新規機能に単体/結合テストを追加。UI 要素に AutomationId を付与（UIA 可視化は `winui-uitest` Skill）。

3. **ビルド/テスト** — `build-test-runner` サブ（Sonnet）に `dotnet build/test` を委譲し、**合否・件数・失敗要点だけ**受ける。
   失敗が状態整合・複雑なら自分で詳細を確認。失敗状態でコミットしない。

4. **GUI 評価**（GUI 要素を追加・変更した Task のみ） — `gui-evaluator` サブ（Sonnet）に委譲。判定文だけ受ける。

5. **実装コミット** — `git commit -F <tempfile>`（PowerShell の here-string 事故を避ける）。
   メッセージは日本語・先頭に Task 番号・本文に実装モデル名。意味のある単位で分けてよい。フック回避禁止。

6. **クロスモデルレビュー** — `cross-model-reviewer` サブ（Opus）に委譲。
   レビュー全文は `docs/reviews/TaskX-Y.md` に書かせ、**判定＋対応必須の指摘だけ**受ける。

7. **指摘修正** → 3 を再実行（`build-test-runner`）→ **レビュー対応コミット**（`git commit -F`）。
   `docs/reviews/TaskX-Y.md` に対応/未対応の判断も追記。

8. **dev-re へマージ** — `git checkout dev-re; git merge --no-ff TaskX-Y -m '...'; git push origin dev-re`。
   （push は恒久許可済み。force push・ブランチ削除・履歴改変はユーザー承認なしに行わない。）

9. **メモリ更新** — 進捗・決定事項・新知見を MEMORY.md と該当ファイルに記録。

10. **`/clear`** して次タスクへ（コンパクションは使わない。状態はメモリで持ち越す）。

## 委譲の指針（コスト）
- 重い・隔離できる作業（探索・GUI 評価・レビュー・大量出力のビルド/テスト）はサブへ。メインは委譲と統合に徹する。
- サブの戻り値は要約、成果物はファイル（レビュー→docs/reviews、スクショ→サブ内、ログ→要約）。
- `git`/`gh` など小出力コマンドは委譲せずインライン実行。
- 詳細は `knowledge.md` と `AGENTS.md`「ロングセッション運用とコスト最適化」。
