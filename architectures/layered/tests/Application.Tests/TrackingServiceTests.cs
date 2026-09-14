using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class TrackingServiceTests
{
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TrackingService _service = new();

    // A 100x50 box with its top left corner at (x, y).
    private static DetectionBox BoxAt(double x, double y, DateTimeOffset? timestamp = null) =>
        new(x, y, x + 100, y + 50, timestamp ?? Start);

    private IEnumerable<int> Ids(params DetectionBox[] detections) =>
        _service.Track(detections, Start).Current.Select(track => track.TrackId);

    [Fact]
    public void Track_NumbersNewVesselsFromOneInDetectionOrder()
    {
        DetectionBox[] detections = [BoxAt(0, 0), BoxAt(500, 0)];

        var visual = _service.Track(detections, Start);

        Assert.Equal([1, 2], visual.Current.Select(track => track.TrackId));
        Assert.Same(detections[1], visual.Current[1].Box);
    }

    [Fact]
    public void Track_KeepsTheIdsOfVesselsThatMoveALittle()
    {
        Ids(BoxAt(0, 0), BoxAt(500, 0));

        Assert.Equal([2, 1], Ids(BoxAt(510, 5), BoxAt(10, 0)));
    }

    [Fact]
    public void Track_GivesADetectionThatOverlapsNoFreeTrackANewId()
    {
        Ids(BoxAt(0, 0));

        // The second box overlaps the same track as the first, which takes it; the third lines
        // up with it horizontally but sits well below.
        Assert.Equal([1, 2, 3], Ids(BoxAt(5, 0), BoxAt(10, 0), BoxAt(0, 300)));
        Assert.Equal([4], Ids(BoxAt(1000, 0)));
    }

    [Fact]
    public void Track_GivesADetectionToTheTrackItOverlapsMost()
    {
        Ids(BoxAt(0, 0), BoxAt(60, 0));

        Assert.Equal([2], Ids(BoxAt(40, 0)));
    }

    [Fact]
    public void Track_RemembersAVesselThroughAShortGapButNotALongOne()
    {
        Ids(BoxAt(0, 0));
        for (var frame = 0; frame < 3; frame++)
        {
            Ids();
        }

        Assert.Equal([1], Ids(BoxAt(0, 0)));

        for (var frame = 0; frame < 4; frame++)
        {
            Ids();
        }

        Assert.Equal([2], Ids(BoxAt(0, 0)));
    }

    [Fact]
    public void Track_WithNoDetections_ReturnsEmpty()
    {
        Assert.Empty(_service.Track([], Start).Current);
    }

    [Fact]
    public void Track_AccumulatesAHistoryAcrossFrames()
    {
        _service.Track([BoxAt(100, 680)], Start);

        var visual = _service.Track([BoxAt(110, 680, Start.AddSeconds(1))], Start.AddSeconds(1));

        Assert.Single(visual.Current);
        Assert.Equal([150, 160], visual.History.Select(track => track.Box.CenterX));
        Assert.All(visual.History, track => Assert.Equal(1, track.TrackId));
    }

    [Fact]
    public void Track_ForgetsHistoryOlderThanTwoMinutes()
    {
        _service.Track([BoxAt(100, 680)], Start);

        var visual = _service.Track([BoxAt(110, 680, Start.AddSeconds(150))], Start.AddSeconds(150));

        Assert.Equal([160], visual.History.Select(track => track.Box.CenterX));
    }
}
