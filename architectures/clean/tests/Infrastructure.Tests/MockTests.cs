using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Infrastructure.Adapters;
using CleanArchitecture.Infrastructure.Mocks;
using Xunit;

namespace CleanArchitecture.Infrastructure.Tests;

public class MockTests
{
    private static readonly DateTimeOffset Timestamp = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static VideoFrame Frame(int index) => new(index, Timestamp, 1920, 1080);

    private static Track TrackAt(int id, double centreX, double centreY) =>
        new(id, new Detection(centreX - 30, centreY - 20, centreX + 30, centreY + 20, Timestamp));

    private static VisibleVessel VesselAt(long mmsi, int x, int y) =>
        new(new AisRecord(mmsi, 121.5, 29.87, 8, 270, 270, 70, Timestamp), x, y);

    [Fact]
    public void MockDetector_ReportsTheSameBoxesForTheSameFrame()
    {
        var detector = new MockDetector();

        var first = detector.Detect(Frame(2));
        var second = detector.Detect(Frame(2));

        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(d => d.CenterX), second.Select(d => d.CenterX));
    }

    [Fact]
    public void MockDetector_SweepsTheBoxesAcrossTheFrame()
    {
        var detector = new MockDetector();

        Assert.NotEqual(detector.Detect(Frame(0))[0].CenterX, detector.Detect(Frame(1))[0].CenterX);
    }

    [Fact]
    public void MockDetector_KeepsTheBoxesInsideTheFrame()
    {
        var detector = new MockDetector();

        foreach (var detection in detector.Detect(Frame(1000)))
        {
            Assert.InRange(detection.CenterX, 0, 1920);
            Assert.InRange(detection.CenterY, 0, 1080);
        }
    }

    [Fact]
    public void MockDetector_WithAZeroWidthFrame_LeavesTheBoxesUnwrapped()
    {
        var detections = new MockDetector().Detect(new VideoFrame(1, Timestamp, 0, 1080));

        Assert.All(detections, detection => Assert.Equal(40, detection.CenterX));
    }

    [Fact]
    public void SequentialTracker_NumbersTracksFromOne()
    {
        Detection[] detections = [new(0, 0, 10, 10, Timestamp), new(20, 20, 30, 30, Timestamp)];

        var tracks = new SequentialTracker().Track(detections, Timestamp);

        Assert.Equal([1, 2], tracks.Select(track => track.Id));
        Assert.Same(detections[0], tracks[0].Detection);
    }

    [Fact]
    public void SequentialTracker_WithNoDetections_ReturnsEmpty()
    {
        Assert.Empty(new SequentialTracker().Track([], Timestamp));
    }

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
