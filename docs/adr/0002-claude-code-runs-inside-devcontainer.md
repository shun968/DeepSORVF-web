# ADR-0002: Claude Codeプロセス自体をdevcontainer内で実行する

- ステータス: 採用
- 日付: 2026-08-23
- 関連: issue #7

## コンテキスト

issue #7は、agentが実行するコマンド・アクセス可能なホスト・秘密情報へのアクセスを制御した、安全な開発環境（sandbox/devContainer）の構築を求めている。制御は組み込みツールの許可設定だけでなく、`python -c "..."` のような回避策にも対応し、OSレベル（bubblewrap等）での制御を検討する必要がある。

Claude Code自体に、bubblewrap（Linux/WSL2）またはSeatbelt（macOS）を使ったBashサンドボックス機能が標準搭載されていることが分かった。ファイルシステム・ネットワーク・秘密情報（env var/ファイル）のdeny/maskを、Bashコマンドとその子プロセス全体に対してOSレベルで強制できる。

### 検討した選択肢

| 案 | 却下理由（または留保） |
|---|---|
| ホストで`claude`本体を実行し、Bashコマンド呼び出しだけを個別に`docker exec`でコンテナへ転送する | Read/Edit/Write、MCPサーバー、hooksはホスト上で無制限に動き続けるため、アクセス制御の境界として機能しない（公式ドキュメントで明示されている設計上の制約） |
| Claude Code組み込みのBashサンドボックスのみで、コンテナを使わない | Bashコマンド以外（Read/Edit/Write、MCP、hooks）は素のホスト上で動く。フルの隔離にはならない |

## 決定

**`claude`プロセスの実体をdevcontainer（Dockerコンテナ）のnamespace内で実行する。** コンテナ内の統合ターミナル、または `docker exec -it <container> claude` から起動する（実行主体がどこのnamespaceにあるかが境界を決め、操作の発信元がホストかどうかは無関係）。

- `.devcontainer/Dockerfile` は `python:3.14-slim`（ML実行環境自体は別途、README記載のPython 3.7 pinで対応するため、ここはツール実行専用として最新安定版を使う）
- 公式の `ghcr.io/anthropics/devcontainer-features/claude-code` featureでClaude Code CLIを導入。ベースイメージにNode.jsが無いため `ghcr.io/devcontainers/features/node:1` も追加が必要（実機検証で判明）
- `~/.claude` を named volume で永続化し、rebuildのたびの再ログインを避ける。ボリュームは初回マウント時にDockerfile側で作成したディレクトリの所有権を引き継ぐため、`vscode` ユーザーで事前に作成しておく必要がある（そうしないとroot所有になり書き込み不可になることを実機で確認）

## 結果

- コンテナ内で `claude` を起動する限り、Bashツール・Read/Edit/Write・MCP・hooksすべてがコンテナの境界内に収まる
- Claude Code組み込みのBashサンドボックス（`/sandbox`、bubblewrapベース）は、コンテナ内でさらに重ねて有効化できる想定だが、**本ADRの時点では未設定**（コンテナ内でbubblewrapを動かすには `enableWeakerNestedSandbox` が必要になるケースがある）
- ネットワーク許可リスト（`init-firewall.sh`、featureが同梱するが未有効化）と `sandbox.credentials` による秘密情報保護も、issue #7の残タスクとして未着手
- devcontainerを使わずホストで直接 `claude --dangerously-skip-permissions` 等を使う運用は、この境界の外に出るため避ける
