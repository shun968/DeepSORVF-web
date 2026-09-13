using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class FusedTrackTests
{
    private static readonly DetectionBox Box = new(10, 20, 30, 50, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Constructor_WithMatchedAis_SetsAllProperties()
    {
        var ais = new AisRecord(431234567, 121.5, 29.87, 5.2, 45, 47, 30, DateTimeOffset.UnixEpoch);

        var fused = new FusedTrack(3, ais, Box, DateTimeOffset.UnixEpoch);

        Assert.Equal(3, fused.TrackId);
        Assert.Same(ais, fused.MatchedAis);
        Assert.Same(Box, fused.Box);
        Assert.Equal(DateTimeOffset.UnixEpoch, fused.Timestamp);
    }

    [Fact]
    public void Constructor_WithoutMatchedAis_LeavesMatchedAisNull()
    {
        var fused = new FusedTrack(3, null, Box, DateTimeOffset.UnixEpoch);

        Assert.Null(fused.MatchedAis);
    }
}
