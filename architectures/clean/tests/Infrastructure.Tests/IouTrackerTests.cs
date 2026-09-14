using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Infrastructure.Adapters;
using Xunit;

namespace CleanArchitecture.Infrastructure.Tests;

public class IouTrackerTests
{
    private static readonly DateTimeOffset Start = new(2022, 6, 4, 4, 5, 12, TimeSpan.Zero);
    private readonly IouTracker _tracker = new();

    // A 100x50 box with its top left corner at (x, y).
    private static Detection BoxAt(double x, double y) => new(x, y, x + 100, y + 50, Start);

    private IEnumerable<int> Ids(params Detection[] detections) =>
        _tracker.Track(detections, Start).Select(track => track.Id);

    [Fact]
    public void Track_NumbersNewVesselsFromOneInDetectionOrder()
    {
        Detection[] detections = [BoxAt(0, 0), BoxAt(500, 0)];

        var tracks = _tracker.Track(detections, Start);

        Assert.Equal([1, 2], tracks.Select(track => track.Id));
        Assert.Same(detections[1], tracks[1].Detection);
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
        Assert.Equal([4], Ids(BoxAt(1000, 0)).Skip(0));
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
        Assert.Empty(_tracker.Track([], Start));
    }
}
