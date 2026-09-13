using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class FusionServiceTests
{
    private const double Gate = 540;
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FusionService _service = new();

    private static VisualTrack Track(int id, double centreX, double centreY, DateTimeOffset timestamp) =>
        new(id, new DetectionBox(centreX - 30, centreY - 20, centreX + 30, centreY + 20, timestamp));

    private static ProjectedAisRecord Ais(long mmsi, int x, int y, DateTimeOffset timestamp) =>
        new(new AisRecord(mmsi, 121.5, 29.87, 8, 270, 270, 70, timestamp), x, y);

    private static VisualFrame Visual(params VisualTrack[] tracks) => new(tracks, tracks);

    private static AisFrame AisFrame(params ProjectedAisRecord[] records) => new(records, records);

    // Four matching frames (seconds 0-3) is enough to bind track 1 to vessel 431234567.
    private void BindTrackOneToTheVessel()
    {
        for (var second = 0; second < 4; second++)
        {
            var at = Start.AddSeconds(second);
            _service.Fuse(Visual(Track(1, 960, 700, at)), AisFrame(Ais(431234567, 958, 702, at)), Gate, at);
        }
    }

    // Track 1 back beside the bound vessel, with a second vessel sitting exactly on it: only a
    // pairing that is still bound keeps 431234567 over the nearer 431999999.
    private IReadOnlyList<FusedTrack> FuseWithANearerVesselAt(DateTimeOffset at) =>
        _service.Fuse(
            Visual(Track(1, 960, 700, at)),
            AisFrame(Ais(431234567, 958, 702, at), Ais(431999999, 960, 700, at)),
            Gate,
            at);

    [Fact]
    public void Fuse_BindsATrackToTheVesselItOverlaps()
    {
        var fused = _service.Fuse(
            Visual(Track(1, 960, 700, Start)),
            AisFrame(Ais(431234567, 958, 702, Start)),
            Gate,
            Start);

        Assert.Equal(431234567, Assert.Single(fused).MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_TakesTheCheapestPairingOverallRatherThanEachTracksOwnNearest()
    {
        // Track 1 is marginally nearer vessel B, but pairing it with B would strand track 2
        // far from anything, so the cheapest overall pairing is the straight one.
        var fused = _service.Fuse(
            Visual(Track(1, 100, 700, Start), Track(2, 130, 700, Start)),
            AisFrame(Ais(431000001, 105, 700, Start), Ais(431000002, 128, 700, Start)),
            Gate,
            Start);

        Assert.Equal(431000001, fused[0].MatchedAis!.Mmsi);
        Assert.Equal(431000002, fused[1].MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_LeavesTracksBeyondTheGateUnmatched()
    {
        var fused = _service.Fuse(
            Visual(Track(1, 100, 100, Start)),
            AisFrame(Ais(431234567, 1800, 1000, Start)),
            Gate,
            Start);

        Assert.Null(Assert.Single(fused).MatchedAis);
    }

    [Fact]
    public void Fuse_RejectsAVesselTravellingTheOppositeWay()
    {
        var track = new VisualFrame(
            [Track(1, 200, 700, Start.AddSeconds(1))],
            [Track(1, 100, 700, Start), Track(1, 200, 700, Start.AddSeconds(1))]);
        // Same place, but tracking backwards along the track's path.
        var vessel = new AisFrame(
            [Ais(431234567, 205, 700, Start.AddSeconds(1))],
            [Ais(431234567, 305, 700, Start), Ais(431234567, 205, 700, Start.AddSeconds(1))]);

        var fused = _service.Fuse(track, vessel, Gate, Start.AddSeconds(1));

        Assert.Null(Assert.Single(fused).MatchedAis);
    }

    [Fact]
    public void Fuse_KeepsABoundPairWhenANearerVesselAppears()
    {
        BindTrackOneToTheVessel();

        var later = Start.AddSeconds(4);
        var fused = _service.Fuse(
            Visual(Track(1, 960, 700, later)),
            AisFrame(Ais(431234567, 958, 702, later), Ais(431999999, 960, 700, later)),
            Gate,
            later);

        Assert.Equal(431234567, Assert.Single(fused).MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_ReportsTracksWithNoVesselOfTheirOwn()
    {
        var fused = _service.Fuse(
            Visual(Track(1, 960, 700, Start), Track(2, 200, 700, Start)),
            AisFrame(Ais(431234567, 958, 702, Start)),
            Gate,
            Start);

        Assert.Equal(431234567, fused[0].MatchedAis!.Mmsi);
        Assert.Null(fused[1].MatchedAis);
    }

    [Fact]
    public void Fuse_WithNoVessels_LeavesEveryTrackUnmatched()
    {
        var fused = _service.Fuse(Visual(Track(1, 960, 700, Start)), AisFrame(), Gate, Start);

        Assert.Null(Assert.Single(fused).MatchedAis);
    }

    [Fact]
    public void Fuse_WithNoTracks_ReturnsEmpty()
    {
        Assert.Empty(_service.Fuse(Visual(), AisFrame(Ais(431234567, 958, 702, Start)), Gate, Start));
    }

    [Fact]
    public void Fuse_KeepsTheTrackIdAndBox()
    {
        var track = Track(7, 960, 700, Start);

        var fused = Assert.Single(_service.Fuse(Visual(track), AisFrame(), Gate, Start));

        Assert.Equal(7, fused.TrackId);
        Assert.Same(track.Box, fused.Box);
        Assert.Equal(Start, fused.Timestamp);
    }

    [Fact]
    public void Fuse_KeepsABoundPairThroughABriefMiss()
    {
        BindTrackOneToTheVessel();

        // The track jumps away for one frame, so the pairing fails the distance check.
        var missedAt = Start.AddSeconds(4);
        var missed = _service.Fuse(
            Visual(Track(1, 100, 100, missedAt)),
            AisFrame(Ais(431234567, 958, 702, missedAt)),
            Gate,
            missedAt);

        Assert.Null(Assert.Single(missed).MatchedAis);
        Assert.Equal(431234567, Assert.Single(FuseWithANearerVesselAt(Start.AddSeconds(5))).MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_ForgetsABoundPairMissedForThreeSeconds()
    {
        BindTrackOneToTheVessel();

        for (var second = 4; second < 7; second++)
        {
            var at = Start.AddSeconds(second);
            _service.Fuse(Visual(Track(1, 100, 100, at)), AisFrame(Ais(431234567, 958, 702, at)), Gate, at);
        }

        Assert.Equal(431999999, Assert.Single(FuseWithANearerVesselAt(Start.AddSeconds(7))).MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_ForgetsABoundPairOnceItsVesselLeavesTheFrame()
    {
        BindTrackOneToTheVessel();

        var goneAt = Start.AddSeconds(4);
        _service.Fuse(Visual(Track(1, 960, 700, goneAt)), AisFrame(), Gate, goneAt);

        Assert.Equal(431999999, Assert.Single(FuseWithANearerVesselAt(Start.AddSeconds(5))).MatchedAis!.Mmsi);
    }

    [Fact]
    public void Fuse_BindsTwoTracksToTheirOwnVesselsIndependently()
    {
        IReadOnlyList<FusedTrack> fused = [];
        for (var second = 0; second < 5; second++)
        {
            var at = Start.AddSeconds(second);
            fused = _service.Fuse(
                Visual(Track(1, 300, 700, at), Track(2, 1500, 700, at)),
                AisFrame(Ais(431000001, 302, 702, at), Ais(431000002, 1498, 702, at)),
                Gate,
                at);
        }

        Assert.Equal(431000001, fused[0].MatchedAis!.Mmsi);
        Assert.Equal(431000002, fused[1].MatchedAis!.Mmsi);
    }
}
