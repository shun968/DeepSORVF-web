# レイヤードアーキテクチャ（ASP.NET Core）

DeepSORVF本体（Pythonの `utils/AIS_utils.py` / `utils/VIS_utils.py` / `utils/FUS_utils.py`
にあるAIS処理・検出・追跡・融合ロジック）を、レイヤードアーキテクチャに当てはめて実装した
場合の練習用実装（[issue #1](https://github.com/shun968/DeepSORVF-web/issues/1)）。
DeepSORVF自体のPythonからの本格移植ではなく、レイヤードアーキテクチャという設計パターンを
実際に当てはめたときの過不足を検証することが目的。そのため、検出（YOLOX）・追跡（DeepSORT）
は固定値/決定的な値を返すモックに留めている。

## 層とプロジェクトの対応

| 層 | プロジェクト | 依存先 |
|---|---|---|
| Presentation | `src/Web` | Application, Infrastructure |
| Application（ビジネスロジック） | `src/Application` | Domain |
| Domain | `src/Domain` | なし |
| Infrastructure（データアクセス） | `src/Infrastructure` | Domain |

## 実装状況

| Pythonの対応箇所 | 移植先 | 状態 |
|---|---|---|
| `utils/AIS_utils.py`（AISPRO: CSV読み込み・粗選別） | `Domain/Entities/AisRecord.cs`, `Domain/Repositories/IAisRepository.cs`, `Infrastructure/Repositories/CsvAisRepository.cs`, `Application/Services/AisService.cs` | 移植済み（下記の簡略化あり） |
| `utils/VIS_utils.py`（YOLOX検出・DeepSORT追跡） | `Application/Services/DetectionService.cs`, `TrackingService.cs` | issue #1の方針通りモック実装（実アルゴリズムは対象外） |
| `utils/FUS_utils.py`（FUSPRO: DTW軌跡類似度によるAIS-映像の対応付け） | `Application/Services/FusionService.cs` | 大幅に簡略化（リスト順による単純な対応付け。下記参照） |
| `main.py`（フレームループ: AIS処理→検出→追跡→融合） | `Application/Pipeline/VesselTrackingPipeline.cs`, `Web/Controllers/VesselTrackingController.cs` | 移植済み |
| 動画入出力・描画（`main.py`, `utils/draw.py`）、カメラ座標変換・デッドレコニング補間 | 未着手・対象外 | 下記「スコープ外」参照 |

### スコープ外にした部分とその理由

issue #1の主眼は「アーキテクチャの当てはめ方の検証」であり、DeepSORVFのアルゴリズム自体の
精度移植ではないため、以下は意図的に簡略化・対象外としている。

- **カメラ座標変換・デッドレコニング補間**（`AIS_utils.py`の`visual_transform`/`data_pred`）:
  カメラパラメータ（焦点距離・画角等）を使った幾何計算が必要で、アーキテクチャ検証に対して
  実装コストが見合わないため未移植。`CsvAisRepository`は該当秒のCSVファイルが無ければ
  空リストを返すのみで、前秒からの位置推定は行わない。
- **検出（YOLOX）・追跡（DeepSORT）の実アルゴリズム**: issue #1に明記の通りモックに留める。
  `DetectionService`はframeIndexの関数として決定的にbboxを返し、`TrackingService`は
  検出リストの並び順をそのままトラックIDにする（再識別ロジックなし）。
- **融合（DTW軌跡マッチング）**: 本来は映像上のトラックとAISレコードをピクセル空間で
  比較するが、上記のカメラ座標変換が無いため位置ベースの比較ができない。そのため
  `FusionService`は単にリスト順で対応付けるだけの簡略実装になっている。

## API

```
POST /api/vessel-tracking/runs
{
  "aisDataDirectory": "...",   // <yyyy_MM_dd_HH_mm_ss>.csv が置かれたディレクトリ
  "startTime": "2021-01-01T00:00:00Z",
  "frameCount": 10,
  "frameIntervalSeconds": 1
}
```

フレームごとにAIS処理→検出→追跡→融合を実行し、各フレームのAISレコード・映像トラック・
融合結果をJSONで返す。動画のデコードは行わず、`frameCount`件のダミーフレームとして処理する
（issue #1の「映像またはダミーのフレーム列」の許容範囲内）。

## 実行方法

```sh
dotnet run --project src/Web
```

## セットアップ

ビルド・実行には[.NET SDKのセットアップ](../SETUP.md)が必要。
