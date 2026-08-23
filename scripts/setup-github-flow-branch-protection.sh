#!/usr/bin/env bash
#
# GitHub Flow に準拠するための、GitHubリポジトリ側の設定・保護ルールを一括セットアップする。
#
# GitHub Flow の要件:
#   - main は常にデプロイ可能な状態を保つ → mainへの直接push・force push・削除を禁止する
#   - 変更はすべてfeatureブランチ + Pull Requestを経由する → mainに pull_request ルールを課す
#   - マージ後はfeatureブランチを残さない → delete_branch_on_merge を有効化する
#
# 事前条件:
#   - GitHub CLI (`gh`) がインストール済みで、対象リポジトリへのadmin権限を持つ
#     アカウントで認証済みであること (`gh auth status`)。
#   - 対象リポジトリがpublicであること。private repoでは一部ルールがGitHub Free/Pro
#     プランで利用できない場合がある（要:実機確認。本スクリプトはpublic repoで検証済み）。
#
# 使い方:
#   ./scripts/setup-github-flow-branch-protection.sh [owner/repo] [enforcement] [required_approvals]
#
#   owner/repo           省略時は現在のディレクトリのgit remoteから解決する
#   enforcement          "active"（デフォルト） | "disabled"
#                         ("evaluate" はGitHub Enterprise専用のため使えない。実機確認済み)
#   required_approvals   PRのマージに必要な承認数。デフォルト0（ソロ開発を想定し、
#                         「PR経由を必須にする」ことのみ強制し、他者レビューは必須にしない）
#
# 注意:
#   このスクリプトを実行すると、リポジトリオーナー自身も含めて誰も main へ直接
#   git push できなくなる（bypass_actorsを設定していないため）。GitHub Flowの原則
#   通りだが、緊急時に直接pushしたい運用の場合は事前に認識しておくこと。

set -euo pipefail

REPO="${1:-}"
ENFORCEMENT="${2:-active}"
REQUIRED_APPROVALS="${3:-0}"
RULESET_NAME="github-flow-main-protection"
MAIN_REF="refs/heads/main"

if [[ "$ENFORCEMENT" != "active" && "$ENFORCEMENT" != "disabled" ]]; then
  echo "エラー: enforcement は active / disabled のいずれかで指定してください（evaluateはEnterprise専用のため不可）" >&2
  exit 1
fi

if [[ -z "$REPO" ]]; then
  REPO="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
fi

echo "対象リポジトリ: $REPO"
echo "enforcement: $ENFORCEMENT"
echo "PR必須承認数: $REQUIRED_APPROVALS"

echo ""
echo "== リポジトリ設定: delete_branch_on_merge を有効化 =="
gh api "repos/${REPO}" --method PATCH -f delete_branch_on_merge=true >/dev/null
echo "完了"

echo ""
echo "== main ブランチの保護ルールセットを作成/更新 =="

PAYLOAD="$(
  jq -n \
    --arg name "$RULESET_NAME" \
    --arg enforcement "$ENFORCEMENT" \
    --arg main_ref "$MAIN_REF" \
    --argjson approvals "$REQUIRED_APPROVALS" \
    '{
      name: $name,
      target: "branch",
      enforcement: $enforcement,
      conditions: {
        ref_name: {
          include: [$main_ref],
          exclude: []
        }
      },
      rules: [
        { type: "deletion" },
        { type: "non_fast_forward" },
        { type: "required_linear_history" },
        {
          type: "pull_request",
          parameters: {
            required_approving_review_count: $approvals,
            dismiss_stale_reviews_on_push: false,
            require_code_owner_review: false,
            require_last_push_approval: false,
            required_review_thread_resolution: false
          }
        }
      ]
    }'
)"

EXISTING_ID="$(
  gh api "repos/${REPO}/rulesets" --jq \
    ".[] | select(.name == \"${RULESET_NAME}\") | .id" \
    2>/dev/null || true
)"

if [[ -n "$EXISTING_ID" ]]; then
  echo "既存の ruleset (id=$EXISTING_ID) を更新します"
  echo "$PAYLOAD" | gh api "repos/${REPO}/rulesets/${EXISTING_ID}" \
    --method PUT \
    --input -
else
  echo "新規に ruleset を作成します"
  echo "$PAYLOAD" | gh api "repos/${REPO}/rulesets" \
    --method POST \
    --input -
fi

echo ""
echo "完了しました。'gh api repos/${REPO}/rulesets' で確認できます。"
