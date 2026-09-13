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
| `utils/AIS_utils.py`（AISPRO: CSV読み込み・粗選別・位置推算・座標変換） | `Domain/Entities/AisRecord.cs`, `Domain/Geometry/*`, `Infrastructure/Repositories/CsvAisRepository.cs`, `Application/Services/AisService.cs` | 移植済み（AIS_vis履歴を除く。下記参照） |
| `utils/file_read.py`（カメラパラメータ読み込み） | `Infrastructure/Repositories/TextFileCameraParametersRepository.cs` | 移植済み |
| `utils/VIS_utils.py`（YOLOX検出・DeepSORT追跡） | `Application/Services/DetectionService.cs`, `TrackingService.cs` | issue #1の方針通りモック実装（実アルゴリズムは対象外）。軌跡履歴の蓄積のみ実装 |
| `utils/FUS_utils.py`（FUSPRO: DTW軌跡類似度によるAIS-映像の対応付け） | `Domain/Trajectory/*`, `Application/Services/FusionService.cs` | 移植済み（DTW・ハンガリアン法・束縛状態機械） |
| `utils/gen_result.py`（MOT形式の結果ファイル出力） | `Application/Pipeline/MotResultRows.cs`, `Infrastructure/Repositories/MotResultFileWriter.cs` | 移植済み |
| `main.py`（フレームループ: AIS処理→検出→追跡→融合） | `Application/Pipeline/VesselTrackingPipeline.cs`, `Web/Controllers/VesselTrackingController.cs` | 移植済み |
| 動画入出力・描画（`main.py`, `utils/draw.py`） | 未着手・対象外 | 下記「スコープ外」参照 |

### スコープ外にした部分とその理由

issue #1の主眼は「アーキテクチャの当てはめ方の検証」であり、DeepSORVFのアルゴリズム自体の
精度移植ではないため、以下は意図的に簡略化・対象外としている。

- **検出（YOLOX）・追跡（DeepSORT）の実アルゴリズム**: issue #1に明記の通りモックに留める。
  学習済み重み（`YOLOX-final.pth`/`ckpt.t7`）はこのリポジトリに含まれておらず、動画デコーダも
  無いため。`DetectionService`は**カメラの視野に入ったAIS位置の上にbboxを置く**モックで、
  これにより追跡・融合の後段を動画なしで動かせる。裏を返すと検出と融合が構造的に循環している
  ので、融合が当たることは配線が通っている証拠にはなるが、実映像での精度の証拠にはならない
- **動画のデコード・描画**: OpenCvSharpやFFmpegといったネイティブ依存が必要になる一方、
  検出がモックである以上デコードしたフレームを消費する先が無く、描画しても偽のbboxが出るだけ
  なので見送っている
- **アンチオクルージョン処理**（論文の中核貢献、`VIS_utils.py`の`arg.anti`）: DeepSORTの
  トラッカー内部状態に対して働く処理なので、実トラッカーが無い状態では実装しても意味がない

### 融合（DTW軌跡マッチング）について

`FusionService`はFUSPROの構造をそのまま移植している。

1. 視覚トラックとAIS船の全組み合わせについてコスト行列を作る。距離・進行方向が妥当な組は
   **DTW距離×exp(進行方向の角度差)**、そうでない組は到達不能値
2. **ハンガリアン法**で全体最適な割当を求める（各トラックが勝手に最近傍を取るのではない）
3. 割当後にもう一度距離・角度でふるいにかける
4. 同じ組が繰り返し一致した回数を数え、規定回数（4回）を超えた組は**束縛**される。束縛された
   組はコストが大きな負値になるため割当が維持され、数秒間は一致しなくても保持される

なお元実装が使う`fastdtw`は近似アルゴリズムだが、ここで扱う軌跡は高々2分×1Hzで、比較前に
半分に圧縮されるため、**厳密なDTW**を実装している（近似のradiusパラメータが不要になる）。

## API

```
POST /api/vessel-tracking/runs
{
  "aisDataDirectory": "...",      // <yyyy_MM_dd_HH_mm_ss>.csv が置かれたディレクトリ
  "cameraParametersPath": "...",  // カメラパラメータ11値の .txt
  "startTime": "2021-01-01T12:00:00Z",
  "frameCount": 3,
  "frameIntervalSeconds": 60,
  "resultDirectory": "..."       // 任意。指定するとMOT形式の結果ファイルも書き出す
}
```

`resultDirectory` を指定した場合、Python版の`gen_result.py`と同じ10列形式
（`frame,id,x,y,w,h,1,1,1,1`）で `detection.txt` / `tracking.txt` / `fusion.txt` を出力する
（detectionはidが常に0、fusionはMMSIが紐づいたトラックのみ）。

フレームごとにAIS処理→検出→追跡→融合を実行し、各フレームの「カメラに映っているAISレコード
（ピクセル座標付き）」「映像トラック」「融合結果」をJSONで返す。動画のデコードは行わず、
`frameCount`件のダミーフレームとして処理する（issue #1の「映像またはダミーのフレーム列」の
許容範囲内）。

## 実行方法

```sh
task run
```

Web APIを起動して `POST /api/vessel-tracking/runs` を1回送り、結果のJSONを表示してからAPIを停止する。
既定では同梱の `sample-data/` を使う。別のデータで実行する場合は変数で上書きする
（`AIS_DIR` / `CAMERA_PARAMS` / `START_TIME` / `FRAME_COUNT` / `FRAME_INTERVAL_SECONDS` / `RESULT_DIR`）。

### 動作確認

`sample-data/` に合成サンプルデータ（AIS 4隻分＋カメラパラメータ）を同梱している。
`task run` は既定でこのデータを使い、2021-01-01T12:00:00Zから60秒間隔で3フレームを処理する。

サンプルデータの4隻は、パイプラインの各段が効いていることを1回のリクエストで確認できるように
選んである（詳細は [`sample-data/README.md`](./sample-data/README.md)）。

- 真東800mの船（MMSI 431234567）→ ピクセルx≈960（主点）に投影され、融合でMMSIが紐づく
- 東北東1500mの船（MMSI 431987654）→ 画面左寄りに投影され、同じく紐づく
- 真北900mの船（MMSI 431555001）→ 水平視野外なので出てこない
- 真東4500mの船（MMSI 431222999）→ 2海里の距離ゲート外なので出てこない
- AISファイルは12:00:00の1秒分しか無いため、2フレーム目以降は**位置推算**で座標が動く
  （西進する船は画面下方向へ、東進して遠ざかる船は水平線方向へ）
- 3本目のトラックは「AIS非搭載船」を模したモック検出なので、MMSIが `null` のまま残る

## セットアップ

ビルド・実行には[.NET SDKのセットアップ](../SETUP.md)が必要。
