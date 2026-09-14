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

issue #2のスコープ（最小フローが動けばよい）に沿った範囲で実装し、その後、検出をYOLOXに、追跡を
IoUによる簡易版に置き換えた。

| Pythonの対応箇所 | 移植先 | 状態 |
|---|---|---|
| `utils/AIS_utils.py`（CSV読み込み・粗選別・位置推算・座標変換） | `Domain/Services/AisSightingService.cs`, `Domain/Geometry/*`, `Infrastructure/Adapters/CsvAisReader.cs` | 移植済み |
| `utils/file_read.py`（カメラパラメータ読み込み） | `Infrastructure/Adapters/TextFileCameraParametersReader.cs` | 移植済み |
| `utils/VIS_utils.py`（YOLOX検出・DeepSORT追跡） | `Infrastructure/Adapters/YoloxDetector.cs`, `IouTracker.cs`, `OpenCvVideoFrameReader.cs` | 検出はYOLOX（ONNX Runtime）で移植済み。追跡はIoUによる簡易版（DeepSORT・耐遮蔽処理は対象外） |
| `utils/FUS_utils.py`（DTW軌跡類似度） | `Infrastructure/Adapters/NearestVesselFusionEngine.cs` | ピクセル距離の最近傍マッチングに簡略化 |
| `main.py`（フレームループ） | `Application/UseCases/*` | 移植済み |

`Domain/Geometry/` の測地線計算・カメラ投影はアーキテクチャパターンに依存しない純粋な数値計算
なので、[layered](../layered) と同一の実装を使っている（比較対象は構造であってアルゴリズムの
深さではないため）。逆にlayered側にあるDTW・ハンガリアン法・MOT形式出力は、issue #2のスコープ外
なので持ち込んでいない。

### 検出・追跡について

当初はモック（フレーム番号の関数としてbboxを動かす `MockDetector` と、並び順でIDを振る
`SequentialTracker`）だったものを、次の3つのアダプタに置き換えた。ユースケースの変更は、動画の
フレームを読むポート `IVideoFrameReader` を1つ足しただけで、検出器・追跡器の差し替え自体は
`Program.cs` のDI登録で済んでいる。

- `OpenCvVideoFrameReader`: OpenCvSharp（同梱FFmpeg）で動画のフレームを読む。1回の実行の間は
  動画を開いたまま先へ読み進める。公式LinuxランタイムはGTKの共有ライブラリに依存するため、
  devcontainerに `libgtk-3-0` を入れている
- `YoloxDetector`: `scripts/export-yolox-onnx.py` で書き出したONNX（出力の復元までを含む）を
  ONNX Runtimeで推論する。前処理・しきい値・NMSは `detection_yolox/yolo.py` と同じ。`task run` は
  書き出し済みでなければ自動で書き出す（重みは `scripts/fetch-model-weights.sh` で先に取得しておく）
- `IouTracker`: 前フレームの枠との重なり（IoU）で同じ船を対応付ける。外観特徴による再識別は
  持たず、3フレームを超えて検出されなかった船は別のトラックIDになる

動画が無い実行（同梱の合成データなど）では検出を行わない。

## 実行方法

```sh
task run
```

Web APIを起動し、`http://localhost:5000/` で可視化画面を開けるようにする（Ctrl+Cで停止）。画面は
パイプラインを実行し、各フレームのAIS投影位置（と軌跡）・検出/追跡のbbox・融合結果（紐づいたMMSI）を
描画する。フォームの既定値は、リポジトリ直下に `clip-01/`（FVesselのテストデータ）があればそのAIS・
カメラパラメータ・動画（開始時刻と長さは動画のファイル名から求める）、無ければ同梱の合成データ
`sample-data/`（動画なし）で、変数で上書きできる（`AIS_DIR` / `CAMERA_PARAMS` / `START_TIME` / `FRAME_COUNT` / `FRAME_INTERVAL_SECONDS` / `VIDEO_PATH` / `VIDEO_START_TIME`）。

`VIDEO_PATH` に動画を指定すると、その上に重ねて描画する（動画の開始時刻は `VIDEO_START_TIME` か
画面で指定でき、既定は開始時刻と同じ）。別の場所のデータを指定する例（ファイル名の時刻は現地時刻なので `+08:00` を付ける）:

```sh
task run AIS_DIR=/workspace/clip-01/ais CAMERA_PARAMS=/workspace/clip-01/camera_para.txt \
  VIDEO_PATH=/workspace/clip-01/2022_06_04_12_05_12_12_07_02_b.mp4 \
  START_TIME=2022-06-04T12:05:12+08:00 FRAME_COUNT=100 FRAME_INTERVAL_SECONDS=1
```

### 動作確認

`sample-data/` に合成サンプルデータを同梱している（内容はlayered側と同じ）。
`clip-01/` が無い場合、`task run` の画面はこのデータを使い、2021-01-01T12:00:00Zから60秒間隔で3フレームを処理する。

AISの投影座標・視野判定・距離ゲート・位置推算はlayered側と同じ結果になる（同じ幾何実装のため）。
動画が無いので検出・追跡・融合は行われない（検出まで確認するには `clip-01/` を使う）。

## セットアップ

ビルド・実行には[.NET SDKのセットアップ](../SETUP.md)が必要。
