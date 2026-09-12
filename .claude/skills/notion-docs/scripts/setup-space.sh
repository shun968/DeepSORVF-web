#!/usr/bin/env bash
set -euo pipefail

# 対象スペースのトップページIDを1つ渡すだけで、
#   indexページ ── 要件定義（ルート＋8ページ）
#              └─ 基本設計（ルート＋8ページ）
# を空のボイラーテンプレートとして一括作成する。SKILL.mdの「初回セットアップ」の手動手順を
# スクリプト化したもの。既存のindex/ツリーがある場合は使わない（重複作成にしかならない）。
#
# 使い方:
#   setup-space.sh <チームスペースのトップページID または そのURL>
#
# 前提: ntn導入済み・ntn login済み・jq導入済み。
#
# テンプレートは ../templates/ 配下のディレクトリ階層そのものがページ階層に対応する

if [ "$#" -ne 1 ]; then
  echo "usage: $0 <parent-page-id-or-url>" >&2
  exit 1
fi

command -v ntn >/dev/null 2>&1 || {
  echo "ntn command not found. Install it and run 'ntn login' first." >&2
  exit 1
}
command -v jq >/dev/null 2>&1 || {
  echo "jq command not found. Install jq first." >&2
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILL_DIR="$(dirname "$SCRIPT_DIR")"
TEMPLATES_DIR="$SKILL_DIR/templates"

# Notion URLでもID単体でも受け付ける（URL末尾等に現れる32桁hexを拾う）
raw_parent="$1"
parent_id="$(echo "$raw_parent" | grep -oE '[0-9a-f]{32}' | tail -1 || true)"
if [ -z "$parent_id" ]; then
  parent_id="$raw_parent"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/notion-setup.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

create_titled_page() {
  local parent="$1" title="$2"
  ntn api v1/pages -X POST \
    parent[page_id]="$parent" \
    properties[title][title][0][text][content]="$title" \
    < /dev/null | jq -r '.id'
}

fill_body() {
  local page_id="$1" body_file="$2"
  ntn pages edit "$page_id" --content "$(cat "$body_file")" < /dev/null >/dev/null
}

# 子ページへのリンクをページ本文に書く際のタグ。`ntn pages edit --content` は本文の全文置換な
# ので、既存の子ページをこのタグで明示的に含めないと「子ページを削除しようとしている」扱いに
# なり400エラーになる（Markdownリンクや素のURLでは参照として認識されない）。
page_link_tag() {
  local page_id="$1" title="$2"
  echo "<page url=\"https://app.notion.com/p/${page_id//-/}\">${title}</page>"
}

extract_title() {
  grep -m1 '^# ' "$1" | sed 's/^# //'
}

# テンプレートディレクトリ1つ分のツリーを作る。ファイル名の昇順で、最初のファイルをルート
# ページ、残りをその子ページとして作る。ルートページのid/titleをroot_id_file/root_title_file
# に書き出す（indexページからのリンク付けに使う）。
setup_tree() {
  local template_dir="$1" root_id_file="$2" root_title_file="$3"
  local files=("$template_dir"/*.md)

  local root_file="${files[0]}"
  local root_title
  root_title="$(extract_title "$root_file")"
  local root_id
  root_id="$(create_titled_page "$index_id" "$root_title")"
  fill_body "$root_id" "$root_file"
  echo "root ($root_title): $root_id"
  echo "$root_id" > "$root_id_file"
  echo "$root_title" > "$root_title_file"

  local leaf_file leaf_title leaf_id
  for leaf_file in "${files[@]:1}"; do
    leaf_title="$(extract_title "$leaf_file")"
    leaf_id="$(create_titled_page "$root_id" "$leaf_title")"
    fill_body "$leaf_id" "$leaf_file"
    echo "  leaf ($leaf_title): $leaf_id"
  done
}

echo "== index page =="
index_id="$(create_titled_page "$parent_id" "Index")"
fill_body "$index_id" "$TEMPLATES_DIR/index.md"
echo "index: $index_id"

echo "== requirements tree =="
setup_tree "$TEMPLATES_DIR/requirements" "$work_dir/req-root-id" "$work_dir/req-root-title"
req_root_id="$(cat "$work_dir/req-root-id")"
req_root_title="$(cat "$work_dir/req-root-title")"

echo "== basic design tree =="
setup_tree "$TEMPLATES_DIR/basic-design" "$work_dir/design-root-id" "$work_dir/design-root-title"
design_root_id="$(cat "$work_dir/design-root-id")"
design_root_title="$(cat "$work_dir/design-root-title")"

echo "== updating index links =="
cat > "$work_dir/index-updated.md" <<EOF
# Index

要件定義・基本設計ドキュメントツリーの入口。

## 要件定義

$(page_link_tag "$req_root_id" "$req_root_title")

## 基本設計

$(page_link_tag "$design_root_id" "$design_root_title")
EOF
fill_body "$index_id" "$work_dir/index-updated.md"

echo
echo "done."
echo "index page id: $index_id"
echo "requirements root id: $req_root_id"
echo "basic design root id: $design_root_id"
