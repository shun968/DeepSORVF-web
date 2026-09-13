using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class MotResultRowsTests
{
    private const double ImageWidth = 1920;
    private const double ImageHeight = 1080;
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AisRecord Vessel =
        new(431234567, 121.5, 29.87, 8, 270, 270, 70, Start);

    private static FrameResult Frame(int index, DetectionBox box, AisRecord? matched)
    {
        var track = new VisualTrack(1, box);
        return new FrameResult(
            index,
            Start,
            [new ProjectedAisRecord(Vessel, 960, 700)],
            [track],
            [new FusedTrack(track.TrackId, matched, box, Start)]);
    }

    [Fact]
    public void Detections_NumberFramesFromOneAndCarryNoIdentity()
    {
        var rows = MotResultRows.Detections(
            [Frame(0, new DetectionBox(930, 680, 990, 720, Start), Vessel)],
            ImageWidth,
            ImageHeight);

        var row = Assert.Single(rows);
        Assert.Equal(1, row.Frame);
        Assert.Equal(0, row.Id);
        Assert.Equal(930, row.X);
        Assert.Equal(680, row.Y);
        Assert.Equal(60, row.Width);
        Assert.Equal(40, row.Height);
    }

    [Fact]
    public void Tracks_CarryTheTrackId()
    {
        var rows = MotResultRows.Tracks(
            [Frame(0, new DetectionBox(930, 680, 990, 720, Start), Vessel)],
            ImageWidth,
            ImageHeight);

        Assert.Equal(1, Assert.Single(rows).Id);
    }

    [Fact]
    public void Fusions_CarryTheMmsiAndSkipUnmatchedTracks()
    {
        var frames = new[]
        {
            Frame(0, new DetectionBox(930, 680, 990, 720, Start), Vessel),
            Frame(1, new DetectionBox(100, 680, 160, 720, Start), null),
        };

        var rows = MotResultRows.Fusions(frames, ImageWidth, ImageHeight);

        Assert.Equal(431234567, Assert.Single(rows).Id);
    }

    [Fact]
    public void Rows_ClampBoxesToTheFrame()
    {
        var rows = MotResultRows.Tracks(
            [Frame(0, new DetectionBox(-40, -30, 1960, 1120, Start), Vessel)],
            ImageWidth,
            ImageHeight);

        var row = Assert.Single(rows);
        Assert.Equal(0, row.X);
        Assert.Equal(0, row.Y);
        Assert.Equal(1920, row.Width);
        Assert.Equal(1080, row.Height);
    }
}
