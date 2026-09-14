using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Infrastructure.Adapters;
using Xunit;

namespace CleanArchitecture.Infrastructure.Tests;

public class FusionEngineTests
{
    private static readonly DateTimeOffset Timestamp = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Track TrackAt(int id, double centreX, double centreY) =>
        new(id, new Detection(centreX - 30, centreY - 20, centreX + 30, centreY + 20, Timestamp));

    private static VisibleVessel VesselAt(long mmsi, int x, int y) =>
        new(new AisRecord(mmsi, 121.5, 29.87, 8, 270, 270, 70, Timestamp), x, y);

    [Fact]
    public void NearestVesselFusionEngine_BindsEachTrackToItsNearestVessel()
    {
        var results = new NearestVesselFusionEngine().Fuse(
            [TrackAt(1, 960, 700), TrackAt(2, 400, 650)],
            [VesselAt(431987654, 402, 648), VesselAt(431234567, 958, 702)],
            540,
            Timestamp);

        Assert.Equal(431234567, results[0].MatchedVessel!.Mmsi);
        Assert.Equal(431987654, results[1].MatchedVessel!.Mmsi);
    }

    [Fact]
    public void NearestVesselFusionEngine_DoesNotGiveOneVesselToTwoTracks()
    {
        var results = new NearestVesselFusionEngine().Fuse(
            [TrackAt(1, 960, 700), TrackAt(2, 964, 704)],
            [VesselAt(431234567, 958, 702)],
            540,
            Timestamp);

        Assert.Equal(431234567, results[0].MatchedVessel!.Mmsi);
        Assert.Null(results[1].MatchedVessel);
    }

    [Fact]
    public void NearestVesselFusionEngine_LeavesTracksBeyondTheGateUnmatched()
    {
        var results = new NearestVesselFusionEngine().Fuse(
            [TrackAt(1, 100, 100)], [VesselAt(431234567, 1800, 1000)], 540, Timestamp);

        Assert.Null(Assert.Single(results).MatchedVessel);
    }

    [Fact]
    public void NearestVesselFusionEngine_WithNoVessels_LeavesEveryTrackUnmatched()
    {
        var results = new NearestVesselFusionEngine().Fuse([TrackAt(1, 960, 700)], [], 540, Timestamp);

        Assert.Null(Assert.Single(results).MatchedVessel);
    }

    [Fact]
    public void NearestVesselFusionEngine_WithNoTracks_ReturnsEmpty()
    {
        Assert.Empty(new NearestVesselFusionEngine().Fuse([], [VesselAt(431234567, 958, 702)], 540, Timestamp));
    }
}
