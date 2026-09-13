using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class DetectionServiceTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UnixEpoch;
    private readonly DetectionService _service = new();

    private static ProjectedAisRecord Projected(int x, int y) =>
        new(new AisRecord(431234567, 121.5, 29.87, 8, 270, 270, 70, Timestamp), x, y);

    [Fact]
    public void Detect_ReturnsOneBoxPerVisibleVesselPlusOneWithoutAis()
    {
        var boxes = _service.Detect([Projected(960, 700), Projected(400, 650)], Timestamp);

        Assert.Equal(3, boxes.Count);
    }

    [Fact]
    public void Detect_PlacesBoxesOnTheProjectedAisPositions()
    {
        var boxes = _service.Detect([Projected(960, 700)], Timestamp);

        Assert.Equal(966, boxes[0].CenterX);
        Assert.Equal(704, boxes[0].CenterY);
        Assert.Equal(Timestamp, boxes[0].Timestamp);
    }

    [Fact]
    public void Detect_WithNoVisibleVessels_StillReportsTheVesselWithoutAis()
    {
        var boxes = _service.Detect([], Timestamp);

        Assert.Single(boxes);
    }
}
