using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Infrastructure.Detection;
using Xunit;

namespace LayeredArchitecture.Infrastructure.Tests;

// Runs against Fixtures/fake-yolox.onnx (see make-fake-yolox-onnx.py next to it): a stand-in
// with the exported model's interface that always predicts the same five rows, so the ONNX
// Runtime path and the post-processing are exercised without the real 35MB model.
public class YoloxVesselDetectorTests
{
    private static readonly DateTimeOffset Timestamp = new(2022, 6, 4, 4, 5, 12, TimeSpan.Zero);
    private static readonly string FakeModelPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fake-yolox.onnx");

    // A 1280x720 picture letterboxes onto the 640x640 input at half scale, 140 pixels down.
    private static FrameImage Picture() => new(1280, 720, new byte[1280 * 720 * 3]);

    [Fact]
    public void Detect_KeepsConfidentBoxesAfterSuppressionAndMapsThemOntoThePicture()
    {
        using var detector = new YoloxVesselDetector(FakeModelPath);

        var detections = detector.Detect(Picture(), Timestamp);

        // Best score first. The fake's second row overlaps its first and its fourth scores too
        // low, so both are gone; the last runs past the picture's right edge and is clipped.
        Assert.Collection(
            detections,
            box => AssertBox(box, 540, 310, 740, 410),
            box => AssertBox(box, 160, 100, 240, 140),
            box => AssertBox(box, 1220, 0, 1280, 40));
    }

    [Fact]
    public void Detect_WithoutTheModelFile_SaysHowToExportIt()
    {
        using var detector = new YoloxVesselDetector("/does/not/exist.onnx");

        var error = Assert.Throws<FileNotFoundException>(() => detector.Detect(Picture(), Timestamp));

        Assert.Contains("export-yolox-onnx.py", error.Message);
    }

    private static void AssertBox(DetectionBox box, double x1, double y1, double x2, double y2)
    {
        Assert.Equal((x1, y1, x2, y2), (box.X1, box.Y1, box.X2, box.Y2));
        Assert.Equal(Timestamp, box.Timestamp);
    }
}
