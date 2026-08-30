# レイヤードアーキテクチャ（ASP.NET Core）

DeepSORVF本体（Pythonの `utils/AIS_utils.py` / `utils/FUS_utils.py` /
`deep_sort/deep_sort/sort/` にある検出・追跡・AIS融合ロジック）をC#へ移植し、
レイヤードアーキテクチャで実装したもの。

## 層とプロジェクトの対応

| 層 | プロジェクト | 依存先 |
|---|---|---|
| Presentation | `src/Web` | Application, Infrastructure |
| Application（ビジネスロジック） | `src/Application` | Domain |
| Domain | `src/Domain` | なし |
| Infrastructure（データアクセス） | `src/Infrastructure` | Domain |

## 移植状況

機械学習（YOLOX検出・DeepSortの外見特徴量抽出CNN）を含まない、純粋なロジック部分から着手している。

| Pythonの対応箇所 | 移植先 | 状態 |
|---|---|---|
| `utils/AIS_utils.py`（AISPRO: 読み込み・粗選別・推算・座標変換） | `Application/Services/AisProcessingService.cs`, `Domain/Geometry/CameraGeometry.cs`, `Domain/Geometry/GeoMath.cs` | 移植済み |
| `deep_sort/deep_sort/sort/kalman_filter.py` | `Domain/Tracking/KalmanFilter.cs` | 移植済み |
| `deep_sort/deep_sort/sort/linear_assignment.py`（min_cost_matching） | `Domain/Tracking/HungarianAlgorithm.cs`, `Domain/Tracking/LinearAssignment.cs` | 移植済み |
| `utils/FUS_utils.py`（DTW軌跡類似度・FUSPRO） | `Domain/Trajectory/FastDtw.cs`, `Domain/Trajectory/TrajectorySimilarity.cs`, `Application/Services/TrajectoryFusionService.cs` | 移植済み |
| `detection_yolox/yolo.py`（YOLOX検出） | `Domain/Inference/IDetector.cs` | インターフェースのみ。ONNX Runtimeによる実装は未着手（[理由](#ニューラルネット部分について)） |
| `deep_sort/deep_sort/deep/feature_extractor.py`（外見特徴量抽出） | `Domain/Inference/IFeatureExtractor.cs` | インターフェースのみ。同上 |
| `deep_sort/deep_sort/sort/tracker.py`（matching_cascade・IOUマッチング等の追跡状態機械） | 未着手 | IDetector/IFeatureExtractorの実装後に着手 |
| 動画入出力・描画（`main.py`, `utils/draw.py`） | 未着手 | 同上 |

### 移植上の注意点

- 距離・方位角の計算（`GeoMath.cs`）は、Python版が使う geopy（Karney法）・pyproj の代わりに
  Vincenty法で実装している。本リポジトリで扱う距離（数海里程度）では十分な精度が出るが、
  桁単位で完全に一致する値にはならない。
- `CameraGeometry.Classify` は元の `data_filter` にある `ais_del` を返す分岐（実際には
  到達しない死んだコード）を、到達可能な範囲だけに整理して実装している。挙動は同じ。
- `Domain/Inference/IDetector.cs` と `IFeatureExtractor.cs` は、ONNX Runtime
  （`Microsoft.ML.OnnxRuntime`）で学習済み重みを実行する実装を想定したインターフェースのみ。

#### ニューラルネット部分について

YOLOX（`YOLOX-final.pth`）とDeepSortの特徴量抽出CNN（`ckpt.t7`）は、CNNレイヤーをC#で
再実装するのではなく、ONNX Runtimeで元の学習済み重みをそのまま実行する方針を採る。
ただし重みファイル自体がこのリポジトリに含まれておらず（README記載のとおりGoogle Driveから
別途取得する運用）、ONNX変換も本パスの対象外のため、実装クラスはまだ用意していない。

## 実行方法

```sh
dotnet run --project src/Web
```

## セットアップ

ビルド・実行には[.NET SDKのセットアップ](../SETUP.md)が必要。
