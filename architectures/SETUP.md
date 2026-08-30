# セットアップ

`architectures/` 配下のサンプルは ASP.NET Core（C#、`net8.0`）で実装している。
`.devcontainer/devcontainer.json` の `features` に `ghcr.io/devcontainers/features/dotnet:2`
（`version: "8.0"`）を追加しており、devcontainerのビルド時に.NET SDKが導入される。
