using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class FusionServiceTests
{
    private readonly FusionService _service = new();
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UnixEpoch;

    private static VisualTrack Track(int id) => new(id, new DetectionBox(0, 0, 10, 10, Timestamp));

    private static AisRecord Ais(long mmsi) => new(mmsi, 121.5, 29.87, 5.2, 45, 47, 30, Timestamp);

    [Fact]
    public void Fuse_WithEqualCounts_PairsByPosition()
    {
        var fused = _service.Fuse([Track(1), Track(2)], [Ais(100), Ais(200)], Timestamp);

        Assert.Equal(100, fused[0].MatchedAis!.Mmsi);
        Assert.Equal(200, fused[1].MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_WithMoreTracksThanAisRecords_LeavesExtraTracksUnmatched()
    {
        var fused = _service.Fuse([Track(1), Track(2)], [Ais(100)], Timestamp);

        Assert.Equal(100, fused[0].MatchedAis!.Mmsi);
        Assert.Null(fused[1].MatchedAis);
    }

    [Fact]
    public void Fuse_WithMoreAisRecordsThanTracks_IgnoresExtraAisRecords()
    {
        var fused = _service.Fuse([Track(1)], [Ais(100), Ais(200)], Timestamp);

        Assert.Single(fused);
        Assert.Equal(100, fused[0].MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_WithNoTracks_ReturnsEmpty()
    {
        var fused = _service.Fuse([], [Ais(100)], Timestamp);

        Assert.Empty(fused);
    }
}
