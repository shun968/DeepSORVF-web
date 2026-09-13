using LayeredArchitecture.Application.Services;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class DetectionServiceTests
{
    private readonly DetectionService _service = new();

    [Fact]
    public void Detect_ReturnsRequestedCount()
    {
        var boxes = _service.Detect(frameIndex: 0, DateTimeOffset.UnixEpoch, count: 3);

        Assert.Equal(3, boxes.Count);
    }

    [Fact]
    public void Detect_IsDeterministicForTheSameFrameIndex()
    {
        var first = _service.Detect(frameIndex: 2, DateTimeOffset.UnixEpoch);
        var second = _service.Detect(frameIndex: 2, DateTimeOffset.UnixEpoch);

        Assert.Equal(first.Select(box => (box.X1, box.Y1, box.X2, box.Y2)), second.Select(box => (box.X1, box.Y1, box.X2, box.Y2)));
    }

    [Fact]
    public void Detect_MovesAcrossFrames()
    {
        var frame0 = _service.Detect(frameIndex: 0, DateTimeOffset.UnixEpoch);
        var frame1 = _service.Detect(frameIndex: 1, DateTimeOffset.UnixEpoch);

        Assert.NotEqual(frame0[0].X1, frame1[0].X1);
    }
}
