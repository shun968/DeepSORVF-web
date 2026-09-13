# クリーンアーキテクチャ（ASP.NET Core）

DeepSORVFのパイプライン（AIS処理→検出→追跡→融合）を、**依存性逆転のみ**を導入して実装した
場合の練習用実装（[issue #2](https://github.com/shun968/DeepSORVF-web/issues/2)）。
[layered](../layered) と同じ題材を別パターンで実装し、比較するためのもの。

## 層とプロジェクトの対応

| 層 | プロジェクト | 依存先 |
|---|---|---|
| Presentation | `src/Web` | Application, Infrastructure（DI登録のみ） |
| Application（UseCase） | `src/Application` | Domain **のみ** |
| Domain | `src/Domain` | なし |
| Infrastructure（アダプタ） | `src/Infrastructure` | Domain |

## パターン1（レイヤード）との違い

同じ4プロジェクト構成だが、**パイプラインの各段がすべてDomain側のインターフェース越しになる**点が違う。

| | パターン1（layered） | パターン2（clean） |
|---|---|---|
| 検出・追跡 | Applicationの具象クラス（インターフェース無し） | `IDetector` / `ITracker` を**Domainが定義**し、Infrastructureが実装 |
| 融合 | Applicationの具象クラス | `IFusionEngine` を同上 |
| ファイルI/O | `IAisRepository`（Domain）+ Infrastructure実装 | `IAisReader` / `ICameraParametersReader` で同じ形 |
| 連結 | `VesselTrackingPipeline` | `ProcessVideoFrameUseCase`（各ポートを直列に呼ぶ） |
| 差し替え | クラスを書き換える | **`Program.cs`のDI登録を1行変える** |

`tests/Architecture.Tests` が依存方向をテストで固定しており、特に
`Application_Should_Not_Depend_On_Infrastructure_Or_Web` と
`EveryPort_IsImplementedOnlyInInfrastructure` の2つがこのパターンの核になっている。

## 実装状況

issue #2のスコープ（最小フローが動けばよい、検出・追跡はダミーでよい）に沿った範囲で実装している。

| Pythonの対応箇所 | 移植先 | 状態 |
|---|---|---|
| `utils/AIS_utils.py`（CSV読み込み・粗選別・位置推算・座標変換） | `Domain/Services/AisSightingService.cs`, `Domain/Geometry/*`, `Infrastructure/Adapters/CsvAisReader.cs` | 移植済み |
| `utils/file_read.py`（カメラパラメータ読み込み） | `Infrastructure/Adapters/TextFileCameraParametersReader.cs` | 移植済み |
| `utils/VIS_utils.py`（YOLOX検出・DeepSORT追跡） | `Infrastructure/Mocks/*` | モック実装 |
| `utils/FUS_utils.py`（DTW軌跡類似度） | `Infrastructure/Adapters/NearestVesselFusionEngine.cs` | ピクセル距離の最近傍マッチングに簡略化 |
| `main.py`（フレームループ） | `Application/UseCases/*` | 移植済み |

`Domain/Geometry/` の測地線計算・カメラ投影はアーキテクチャパターンに依存しない純粋な数値計算
なので、[layered](../layered) と同一の実装を使っている（比較対象は構造であってアルゴリズムの
深さではないため）。逆にlayered側にあるDTW・ハンガリアン法・MOT形式出力は、issue #2のスコープ外
なので持ち込んでいない。

### モック検出器について

`MockDetector` は**AISを参照しない**。フレーム番号の関数としてbboxを画面上で移動させるだけなので、
どのトラックがどのMMSIに紐づくかは事実上任意になる。layered側のモックがAIS投影位置の上にbboxを
置いていた（＝検出と融合が循環していた）のに対し、こちらは`IDetector`を「フレームを見て答える」
という抽象のまま保っている。動作確認で見えるのは**各段が繋がっていること**であって、検出精度では
ない。

## 実行方法

```sh
task run
```

### 動作確認

`sample-data/` に合成サンプルデータを同梱している（内容はlayered側と同じ）。
`task run` で起動したうえで、別のシェルから:

```sh
curl -s -X POST http://localhost:5000/api/vessel-tracking/runs \
  -H 'Content-Type: application/json' \
  -d '{
    "aisDataDirectory": "'"$PWD"'/sample-data/ais",
    "cameraParametersPath": "'"$PWD"'/sample-data/camera.txt",
    "startTime": "2021-01-01T12:00:00Z",
    "frameCount": 3,
    "frameIntervalSeconds": 60
  }' | jq .
```

AISの投影座標・視野判定・距離ゲート・位置推算はlayered側と同じ結果になる（同じ幾何実装のため）。
融合結果だけは上記の通りモック検出器の性質から異なる。

## セットアップ

ビルド・実行には[.NET SDKのセットアップ](../SETUP.md)が必要。
