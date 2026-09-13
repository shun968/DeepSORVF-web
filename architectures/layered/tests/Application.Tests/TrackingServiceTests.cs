using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class TrackingServiceTests
{
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TrackingService _service = new();

    private static DetectionBox BoxAt(int centreX, DateTimeOffset timestamp) =>
        new(centreX - 30, 680, centreX + 30, 720, timestamp);

    [Fact]
    public void Track_AssignsSequentialIdsStartingAtOne()
    {
        DetectionBox[] detections = [BoxAt(100, Start), BoxAt(200, Start)];

        var visual = _service.Track(detections, Start);

        Assert.Equal([1, 2], visual.Current.Select(track => track.TrackId));
        Assert.Same(detections[0], visual.Current[0].Box);
        Assert.Same(detections[1], visual.Current[1].Box);
    }

    [Fact]
    public void Track_WithNoDetections_ReturnsEmpty()
    {
        Assert.Empty(_service.Track([], Start).Current);
    }

    [Fact]
    public void Track_AccumulatesAHistoryAcrossFrames()
    {
        _service.Track([BoxAt(100, Start)], Start);

        var visual = _service.Track([BoxAt(110, Start.AddSeconds(1))], Start.AddSeconds(1));

        Assert.Single(visual.Current);
        Assert.Equal([100, 110], visual.History.Select(track => track.Box.CenterX));
    }

    [Fact]
    public void Track_ForgetsHistoryOlderThanTwoMinutes()
    {
        _service.Track([BoxAt(100, Start)], Start);

        var visual = _service.Track([BoxAt(110, Start.AddSeconds(150))], Start.AddSeconds(150));

        Assert.Equal([110], visual.History.Select(track => track.Box.CenterX));
    }
}
