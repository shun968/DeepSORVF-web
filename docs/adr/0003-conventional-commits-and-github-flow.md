# ADR-0003: Conventional Commits（タイトルのみ）とGitHub Flowを採用する

- ステータス: 採用
- 日付: 2026-08-23
- 関連: issue #7, [shun968/marketing-data-pipeline](https://github.com/shun968/marketing-data-pipeline)

## コンテキスト

commitメッセージの書式とブランチ運用について、個人の別リポジトリ `shun968/marketing-data-pipeline` で既に運用実績のある規約（Conventional Commits + commitlintによるtitle only強制、ADRスキル、GitHub Flow）を本リポジトリにも導入したい。

### 検討した選択肢

| 案 | 却下理由 |
|---|---|
| commitメッセージの書式を規約化せず自由記述にする | 参照先リポジトリでの運用実績（一覧性・自動化のしやすさ）を活かせない |
| GitHub rulesetでmainブランチ保護を行わない | GitHub Flowの前提（mainは常にデプロイ可能・変更は全てPR経由）を機械的に保証できない |

## 決定

- commitメッセージは **Conventional Commits形式・タイトルのみ**（body/footer禁止、issue参照必須）とし、`commitlint.config.js` + lefthookの `commit-msg` フックで強制する
- ブランチ運用は **GitHub Flow** に統一し、`scripts/setup-github-flow-branch-protection.sh` でmainブランチに `pull_request`（PR必須）・`non_fast_forward`（force push禁止）・`deletion`（main削除禁止）・`required_linear_history` を適用する
- ADRは `docs/adr/` にNygard形式で記録する（`.claude/skills/adr/SKILL.md`、`scripts/check-adr-format.sh`で機械検査）

## 結果

- 個々のcommitはtitleのみでissueを参照するため、履歴からissueとの対応が追いやすくなった
- **副作用として、Anthropicの標準コミット運用（`Co-Authored-By`/`Claude-Session` フッター付与）はこのリポジトリでは行わない**（`footer-empty: always` と矛盾するため上書きされる）
- **squash-merge時の注意点が判明した**: GitHubの `gh pr merge --squash` はサーバーサイドで実行されlefthook/commitlintを一切通らないため、`--subject`/`--body` を明示しないと、複数commitのPRでは `<PRタイトル> (#PR番号)` ＋ squashされたcommit一覧のbodyがそのまま入ってしまう。実際にPR #8のマージコミット（`a8686dd`）で `feat: ... (#7) (#8)` という二重参照＋非空bodyが発生した。mainは保護済みでforce-pushが必要になるため、この1件は修正せず残し、以後は必ず `gh pr merge --squash --subject "type(scope): 説明 (#issue番号)" --body ""` を明示する運用に変更した
- mainへの直接pushはリポジトリオーナー自身も含めて不可になった（bypass_actors未設定のため）
