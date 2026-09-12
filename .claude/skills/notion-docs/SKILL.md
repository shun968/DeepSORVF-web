---
name: notion-docs
description: 要件定義書・基本設計書をNotionで作成・更新するときに使う。Notion公式CLI `ntn` によるページ作成/更新コマンド、indexページを起点にしたツリー構造テンプレート、対象チームスペースの指定方法を扱う。「要件定義をNotionに書く」「基本設計をNotionにまとめる」「Notionのドキュメントを更新する」といった作業が該当する。
---

# 要件定義・基本設計のNotion管理

要件定義書・基本設計書はNotionで管理する。ページIDや構造はリポジトリに保存しない。検索には頼らず、
**対象チームスペースの先頭に置いたindexページ** を唯一の入口にして辿る。
Notion公式CLI [`ntn`](https://ntn.dev) を使ってページを作成・更新する。

## 前提条件

- `ntn` コマンドが導入済みであること。
- `ntn login` はユーザーが実行しておく。
- `jq` が導入済み。

## スペース構成

テンプレートファイルのツリーに、対応するNotion上の見え方をカッコで添える。1ファイル = Notion上の1ページ。

```
templates/index.md（indexページ・チームスペース先頭）
├─ templates/requirements/00-root.md（要件定義・ルートページ）
│   ├─ templates/requirements/01-background-purpose.md（背景・目的）
│   ├─ templates/requirements/02-scope.md（スコープ）
│   ├─ templates/requirements/03-stakeholders.md（利用者・ステークホルダー）
│   ├─ templates/requirements/04-functional-requirements.md（機能要件）
│   ├─ templates/requirements/05-non-functional-requirements.md（非機能要件）
│   ├─ templates/requirements/06-constraints.md（制約条件）
│   ├─ templates/requirements/07-glossary.md（用語集）
│   └─ templates/requirements/08-open-issues.md（未決事項）
└─ templates/basic-design/00-root.md（基本設計・ルートページ）
    ├─ templates/basic-design/01-overview.md（概要）
    ├─ templates/basic-design/02-system-architecture.md（システム構成）
    ├─ templates/basic-design/03-functional-design.md（機能設計）
    ├─ templates/basic-design/04-data-design.md（データ設計）
    ├─ templates/basic-design/05-external-interfaces.md（外部インターフェース）
    ├─ templates/basic-design/06-non-functional-design.md（非機能設計）
    ├─ templates/basic-design/07-migration-release-plan.md（移行・リリース計画）
    └─ templates/basic-design/08-open-issues.md（未決事項）
```

各ディレクトリ内は `00-root.md` がルートページ、それ以外がその子ページ（ファイル名の数字が
作成順）。各ファイルの内容はシンプルに保つ — 先頭の `# 見出し` がページタイトル、それ以降が本文。
見出し構成を変える場合はテンプレート自体を更新する。

## 対象チームスペースの指定

使うたびに、対象チームスペースとそのindexページ（IDまたはURL）をユーザーに確認する。

## 初回セットアップ（indexページ・ツリーがまだ無い場合）

```sh
.claude/skills/notion-docs/scripts/setup-space.sh <チームスペースのトップページID または URL>
```

indexページ・要件定義ツリー・基本設計ツリーを一括作成する。**冪等ではない** — 既にあるスペースに
実行すると重複作成される。

## 2回目以降（indexページがある場合）

1. indexページを取得し、ルートページのリンクを得る（`<page url="...">タイトル</page>`の形で載っている）。

   ```sh
   ntn pages get <indexページID> | grep -oE '<page url="[^"]+">[^<]+</page>'
   ```

2. 対象ルートページ配下の子ページ一覧から目的のページを探す（子ページは`type: child_page`の
   ブロックとして並ぶ）。

   ```sh
   ntn api v1/blocks/<ルートページID>/children \
     | jq -r '.results[] | select(.type=="child_page") | "\(.id) \(.child_page.title)"'
   ```

3. 見つかれば更新へ。無ければ対応するテンプレートファイルの内容で新規作成する。

   ```sh
   ntn api v1/pages -X POST \
     parent[page_id]="<親ページID>" \
     properties[title][title][0][text][content]="<ページタイトル>"
   # 応答JSONの .id に対して本文を書き込む
   ntn pages edit "<上で得たページID>" --content "<セクション本文>"
   ```

## 更新

`ntn pages edit` は`--content`の全文でページ内容を置き換える（部分パッチではない）。

```sh
ntn pages get <page-id> > /tmp/current.md
# 編集して /tmp/updated.md を作る
ntn pages edit <page-id> --content "$(cat /tmp/updated.md)"
```

`--allow-deleting-content` は子ページ削除を伴う変更のときだけ付ける。

**子ページを持つページを更新するときは要注意。** 全文置換の中に既存の子ページへの参照を含めないと
「子ページを削除しようとしている」400エラーになる。Markdownリンクや素のURLでは参照と認識されない
ため、次のタグで明示的に含める（URLは`app.notion.com/p/<id>`、開始・終了タグでタイトルを挟む）。

```
<page url="https://app.notion.com/p/<ハイフン無しページID>">ページタイトル</page>
```

## 参照

```sh
ntn pages get <page-id> --json
ntn datasources query <data-source-id> --limit 25
```

## 判断が要る部分

- **Notion vs リポジトリ。** 技術決定はADR(`docs/adr/`)、プロダクト仕様はNotion。
- **要件定義⇔基本設計の対応付け。** 基本設計の機能設計ページに対応する要件ID（`REQ-XXX`）を書く。
- **page-idを保存しない。** indexページ起点で毎回確認し、古いIDを使い続けるリスクを避ける。
- **ページ作成はタイトルと本文を分離する。** `ntn pages create`の出力形式が不明なため、
  タイトルは`ntn api v1/pages -X POST`のJSON応答から`jq`でIDを取り、本文は`ntn pages edit`で書く。
- **`setup-space.sh`は冪等ではない。** 二重実行しないよう、実行前にindexページの有無を確認する。
- **子ページ持ちページの全文置換は`<page url="...">`タグ必須。** 実運用で踏んだ罠(「更新」参照)。
  忘れると`ntn pages edit`が400 (子ページ削除の確認要求)を返す。
