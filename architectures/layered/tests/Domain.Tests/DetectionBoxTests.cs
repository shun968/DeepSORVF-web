using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class DetectionBoxTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000);

        var box = new DetectionBox(10, 20, 30, 50, timestamp);

        Assert.Equal(10, box.X1);
        Assert.Equal(20, box.Y1);
        Assert.Equal(30, box.X2);
        Assert.Equal(50, box.Y2);
        Assert.Equal(timestamp, box.Timestamp);
    }

    [Fact]
    public void CenterAndSize_AreDerivedFromCorners()
    {
        var box = new DetectionBox(10, 20, 30, 50, DateTimeOffset.UnixEpoch);

        Assert.Equal(20, box.CenterX);
        Assert.Equal(35, box.CenterY);
        Assert.Equal(20, box.Width);
        Assert.Equal(30, box.Height);
    }
}
