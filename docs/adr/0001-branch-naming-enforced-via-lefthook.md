# ADR-0001: ブランチ命名規則をGitHub rulesetではなくlefthookで強制する

- ステータス: 採用
- 日付: 2026-08-23
- 関連: issue #7

## コンテキスト

issue #7（sandbox開発環境整備）の一環として、ブランチ名を `<type>/<issue番号>-<slug>` 形式に統一し、issueとの対応を機械的に追跡できるようにしたい。GitHubのrepository ruleset機能には `branch_name_pattern` という、まさにこの形式を強制できるルール種別が存在する。

### 検討した選択肢

| 案 | 却下理由 |
|---|---|
| GitHub ruleset (`branch_name_pattern`) | 実機検証の結果、GitHub Free（個人アカウント）では作成できない。`enforcement: evaluate` も明示的にEnterprise専用と拒否される。`deletion` 等の基本ルールは同条件で問題なく作成できたため、プラン制限と判断した |
| 規約をドキュメント化するのみ（機械検査なし） | 強制力がなく、issue番号の付け忘れ等が発生しやすい |

## 決定

**ローカルのgit hook管理ツール [lefthook](https://github.com/evilmartians/lefthook) の `pre-commit` フックで強制する。**

- `scripts/check-branch-name.sh` が現在のブランチ名を正規表現 `^(feature|fix|docs|chore|refactor|test)/[0-9]+-[a-z0-9-]+$` と照合する（`main`/`master` は除外）
- `lefthook.yml` に `pre-commit` ジョブとして登録する
- `.devcontainer/` でコンテナ作成時に自動で `lefthook install` される（`postCreateCommand`）

## 結果

- devcontainer内で `git commit` する限り、規約違反のブランチ名でのコミットは拒否される
- **fail-open である点が既知の弱点**: `.git/hooks/pre-commit` のシムは `lefthook` バイナリが `PATH` 上に見つからない場合、エラーで拒否せず `exit 0` で黙って通過する。素のホストシェルで `lefthook` を一度もインストールしていない状態から直接commitすると、チェックは効かない
- GitHub側での強制（`branch_name_pattern`）は現行プランでは実現できないため、サーバーサイドの保証は無い。リポジトリがGitHub Team/Enterpriseへ移行した場合は再検討する
- 運用上、`git commit` は devcontainer内（またはホストに `lefthook` をインストール済みの環境）から行うことが前提になる
