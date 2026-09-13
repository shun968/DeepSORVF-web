using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class FusionServiceTests
{
    private const double Gate = 540;
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UnixEpoch;
    private readonly FusionService _service = new();

    private static VisualTrack TrackAt(int id, int centreX, int centreY) =>
        new(id, new DetectionBox(centreX - 30, centreY - 20, centreX + 30, centreY + 20, Timestamp));

    private static ProjectedAisRecord AisAt(long mmsi, int x, int y) =>
        new(new AisRecord(mmsi, 121.5, 29.87, 8, 270, 270, 70, Timestamp), x, y);

    [Fact]
    public void Fuse_BindsEachTrackToTheNearestAisPosition()
    {
        var fused = _service.Fuse(
            [TrackAt(1, 960, 700), TrackAt(2, 400, 650)],
            [AisAt(431987654, 402, 648), AisAt(431234567, 958, 702)],
            Gate,
            Timestamp);

        Assert.Equal(431234567, fused[0].MatchedAis!.Mmsi);
        Assert.Equal(431987654, fused[1].MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_DoesNotGiveTheSameVesselToTwoTracks()
    {
        var fused = _service.Fuse(
            [TrackAt(1, 960, 700), TrackAt(2, 964, 704)],
            [AisAt(431234567, 958, 702)],
            Gate,
            Timestamp);

        Assert.Equal(431234567, fused[0].MatchedAis!.Mmsi);
        Assert.Null(fused[1].MatchedAis);
    }

    [Fact]
    public void Fuse_LeavesTracksBeyondTheGateUnmatched()
    {
        var fused = _service.Fuse(
            [TrackAt(1, 100, 100)],
            [AisAt(431234567, 1800, 1000)],
            Gate,
            Timestamp);

        Assert.Null(Assert.Single(fused).MatchedAis);
    }

    [Fact]
    public void Fuse_WithNoAisRecords_LeavesEveryTrackUnmatched()
    {
        var fused = _service.Fuse([TrackAt(1, 960, 700)], [], Gate, Timestamp);

        Assert.Null(Assert.Single(fused).MatchedAis);
    }

    [Fact]
    public void Fuse_WithNoTracks_ReturnsEmpty()
    {
        Assert.Empty(_service.Fuse([], [AisAt(431234567, 958, 702)], Gate, Timestamp));
    }

    [Fact]
    public void Fuse_KeepsTheTrackIdAndBox()
    {
        var track = TrackAt(7, 960, 700);

        var fused = Assert.Single(_service.Fuse([track], [], Gate, Timestamp));

        Assert.Equal(7, fused.TrackId);
        Assert.Same(track.Box, fused.Box);
        Assert.Equal(Timestamp, fused.Timestamp);
    }
}
