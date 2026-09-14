using System.Runtime.InteropServices;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace CleanArchitecture.Infrastructure.Adapters;

// YOLOX, the paper's ship detector (detection_yolox/), run through ONNX Runtime.
//
// The model is YOLOX-final.pth exported by scripts/export-yolox-onnx.py with the output
// decoding (utils_bbox.decode_outputs) inside the graph, so each output row is one anchor's
// centre x, centre y, width and height in 640x640 input pixels, then its objectness and vessel
// score. The steps around it follow detection_yolox/yolo.py: letterbox the RGB picture onto a
// grey 640x640 canvas, normalise with the ImageNet mean and deviation, keep boxes whose
// objectness times score is at least 0.5, suppress those overlapping a better one by more than
// 0.3 IoU, and map the rest back onto the picture.
//
// Registered once: the model is loaded on the first frame that has a picture, and ONNX Runtime
// allows concurrent runs on one session. A frame without a picture has nothing to detect.
public sealed class YoloxDetector : IDetector, IDisposable
{
    private const int InputSize = 640;
    private const float ScoreThreshold = 0.5f;
    private const float SuppressionOverlap = 0.3f;
    private const double PaddingGrey = 128;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Deviation = [0.229f, 0.224f, 0.225f];

    private readonly string _modelPath;
    private readonly Lazy<InferenceSession> _session;

    public YoloxDetector(string modelPath)
    {
        _modelPath = modelPath;
        // PublicationOnly, so a missing model is reported again rather than cached as a failure.
        _session = new Lazy<InferenceSession>(OpenSession, LazyThreadSafetyMode.PublicationOnly);
    }

    public IReadOnlyList<Detection> Detect(VideoFrame frame)
    {
        if (frame.Image is null)
        {
            return [];
        }

        var session = _session.Value;
        var letterbox = Letterbox.Fit(frame.Image.Width, frame.Image.Height);
        var input = NamedOnnxValue.CreateFromTensor(session.InputNames[0], ToInput(frame.Image, letterbox));
        using var outputs = session.Run([input]);
        return ToDetections(outputs[0].AsTensor<float>(), letterbox, frame.Image, frame.Timestamp);
    }

    public void Dispose()
    {
        if (_session.IsValueCreated)
        {
            _session.Value.Dispose();
        }
    }

    private InferenceSession OpenSession()
    {
        if (!File.Exists(_modelPath))
        {
            throw new FileNotFoundException(
                $"The YOLOX model {_modelPath} does not exist. Export it with `python3 scripts/export-yolox-onnx.py` " +
                "(after fetching the weights with scripts/fetch-model-weights.sh).",
                _modelPath);
        }

        return new InferenceSession(_modelPath);
    }

    private static DenseTensor<float> ToInput(FrameImage image, Letterbox letterbox)
    {
        using var picture = Mat.FromPixelData(image.Height, image.Width, MatType.CV_8UC3, image.Rgb.ToArray());
        using var scaled = new Mat();
        Cv2.Resize(picture, scaled, new Size(letterbox.ScaledWidth, letterbox.ScaledHeight), interpolation: InterpolationFlags.Cubic);
        using var canvas = new Mat(InputSize, InputSize, MatType.CV_8UC3, new Scalar(PaddingGrey, PaddingGrey, PaddingGrey));
        using (var area = new Mat(canvas, new Rect(letterbox.OffsetX, letterbox.OffsetY, letterbox.ScaledWidth, letterbox.ScaledHeight)))
        {
            scaled.CopyTo(area);
        }

        var pixels = new byte[InputSize * InputSize * 3];
        Marshal.Copy(canvas.Data, pixels, 0, pixels.Length);

        // HWC bytes to normalised CHW floats.
        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
        var values = tensor.Buffer.Span;
        var plane = InputSize * InputSize;
        for (var pixel = 0; pixel < plane; pixel++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                values[(channel * plane) + pixel] = ((pixels[(pixel * 3) + channel] / 255f) - Mean[channel]) / Deviation[channel];
            }
        }

        return tensor;
    }

    private static List<Detection> ToDetections(Tensor<float> rows, Letterbox letterbox, FrameImage image, DateTimeOffset timestamp)
    {
        var candidates = new List<Candidate>();
        for (var row = 0; row < rows.Dimensions[1]; row++)
        {
            var score = rows[0, row, 4] * rows[0, row, 5];
            if (score < ScoreThreshold)
            {
                continue;
            }

            float centreX = rows[0, row, 0], centreY = rows[0, row, 1], halfWidth = rows[0, row, 2] / 2, halfHeight = rows[0, row, 3] / 2;
            candidates.Add(new Candidate(centreX - halfWidth, centreY - halfHeight, centreX + halfWidth, centreY + halfHeight, score));
        }

        var kept = new List<Candidate>();
        foreach (var candidate in candidates.OrderByDescending(candidate => candidate.Score))
        {
            if (kept.TrueForAll(better => Overlap(better, candidate) <= SuppressionOverlap))
            {
                kept.Add(candidate);
            }
        }

        // Undo the letterbox, then clip to the picture the way yolo.py does.
        return kept
            .Select(box => new Detection(
                Math.Max(0, Math.Floor((box.X1 - letterbox.OffsetX) / letterbox.Scale)),
                Math.Max(0, Math.Floor((box.Y1 - letterbox.OffsetY) / letterbox.Scale)),
                Math.Min(image.Width, Math.Floor((box.X2 - letterbox.OffsetX) / letterbox.Scale)),
                Math.Min(image.Height, Math.Floor((box.Y2 - letterbox.OffsetY) / letterbox.Scale)),
                timestamp))
            .ToList();
    }

    private static float Overlap(Candidate a, Candidate b)
    {
        var width = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
        var height = Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var intersection = width * height;
        return intersection / (((a.X2 - a.X1) * (a.Y2 - a.Y1)) + ((b.X2 - b.X1) * (b.Y2 - b.Y1)) - intersection);
    }

    // A box in model input pixels.
    private readonly record struct Candidate(float X1, float Y1, float X2, float Y2, float Score);

    // Where a picture lands on the square model input: scaled to fit without distortion and
    // centred, with grey bars filling the rest.
    private readonly record struct Letterbox(double Scale, int ScaledWidth, int ScaledHeight, int OffsetX, int OffsetY)
    {
        public static Letterbox Fit(int width, int height)
        {
            var scale = Math.Min((double)InputSize / width, (double)InputSize / height);
            var scaledWidth = (int)(width * scale);
            var scaledHeight = (int)(height * scale);
            return new Letterbox(scale, scaledWidth, scaledHeight, (InputSize - scaledWidth) / 2, (InputSize - scaledHeight) / 2);
        }
    }
}
