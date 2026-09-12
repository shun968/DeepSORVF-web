# Contributing

このリポジトリへの変更手順・命名規則などのプロジェクトルールをまとめる。
DeepSORVF自体のアーキテクチャ決定（融合パイプライン・追跡アルゴリズムなど）は対象外で、
それらは [Nygard形式のADR](docs/adr/) に記録する（`.claude/skills/managing-adrs/SKILL.md` 参照）。

## ブランチ

`<type>/<issue-number>-<slug>`（`feature`, `fix`, `docs`, `chore`, `refactor`, `test`）の形式で作る。
例: `feature/7-sandbox-dev-environment`。

lefthookの`pre-commit`フックで強制される。チェックアウトごとに一度 `lefthook install` を実行すること
（devcontainer内では`postCreateCommand`により自動実行される）。

`gh issue develop <number> --name <type>/<number>-<slug> --checkout` でブランチを作ることを推奨する。

## コミットメッセージ

[Conventional Commits](https://www.conventionalcommits.org/) に従い、**タイトルのみ**とする
（本文・フッターは付けない。このリポジトリでは末尾に`Co-Authored-By`/`Claude-Session`フッターを
付ける運用は採用していない）。Issueへの参照を必須とする。

例: `feat(devcontainer): 説明 (#7)`

commitlintがlefthookの`commit-msg`フックで強制する。チェックアウトごとに一度 `npm install` を
実行すること。

## プルリクエスト

このリポジトリは[GitHub Flow](https://docs.github.com/en/get-started/using-github/github-flow)に
従う。`main`は保護ブランチであり（PR必須、force-push/削除禁止、linear history必須）、
`scripts/setup-github-flow-branch-protection.sh`で設定する。

PRはsquash-mergeし、`--subject`/`--body ""`を明示する。

```sh
gh pr merge <number> --squash --delete-branch --subject "type(scope): 説明 (#issue番号)" --body ""
```
