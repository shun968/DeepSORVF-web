using CleanArchitecture.Domain.Entities;
using Xunit;

namespace CleanArchitecture.Domain.Tests;

public class EntityTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UnixEpoch;

    private static AisRecord Record() => new(431234567, 121.5, 29.87, 8, 270, 270, 70, Timestamp);

    [Fact]
    public void Detection_DerivesCentreAndSizeFromItsCorners()
    {
        var detection = new Detection(10, 20, 30, 50, Timestamp);

        Assert.Equal(10, detection.X1);
        Assert.Equal(20, detection.Y1);
        Assert.Equal(30, detection.X2);
        Assert.Equal(50, detection.Y2);
        Assert.Equal(Timestamp, detection.Timestamp);
        Assert.Equal(20, detection.CenterX);
        Assert.Equal(35, detection.CenterY);
        Assert.Equal(20, detection.Width);
        Assert.Equal(30, detection.Height);
    }

    [Fact]
    public void Track_CarriesItsIdAndDetection()
    {
        var detection = new Detection(10, 20, 30, 50, Timestamp);

        var track = new Track(7, detection);

        Assert.Equal(7, track.Id);
        Assert.Same(detection, track.Detection);
    }

    [Fact]
    public void VisibleVessel_CarriesItsRecordAndPixelPosition()
    {
        var record = Record();

        var vessel = new VisibleVessel(record, 960, 700);

        Assert.Same(record, vessel.Record);
        Assert.Equal(960, vessel.X);
        Assert.Equal(700, vessel.Y);
    }

    [Fact]
    public void FusionResult_CarriesTheMatchedVesselWhenThereIsOne()
    {
        var track = new Track(1, new Detection(10, 20, 30, 50, Timestamp));
        var record = Record();

        var fusion = new FusionResult(track, record, Timestamp);

        Assert.Same(track, fusion.Track);
        Assert.Same(record, fusion.MatchedVessel);
        Assert.Equal(Timestamp, fusion.Timestamp);
    }

    [Fact]
    public void FusionResult_LeavesTheMatchedVesselNullForAVesselWithoutAis()
    {
        var fusion = new FusionResult(new Track(1, new Detection(10, 20, 30, 50, Timestamp)), null, Timestamp);

        Assert.Null(fusion.MatchedVessel);
    }

    [Fact]
    public void VideoFrame_CarriesItsPositionAndSize()
    {
        var frame = new VideoFrame(3, Timestamp, 1920, 1080);

        Assert.Equal(3, frame.Index);
        Assert.Equal(Timestamp, frame.Timestamp);
        Assert.Equal(1920, frame.Width);
        Assert.Equal(1080, frame.Height);
    }
}
