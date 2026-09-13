using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class VisualTrackTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var box = new DetectionBox(10, 20, 30, 50, DateTimeOffset.UnixEpoch);

        var track = new VisualTrack(7, box);

        Assert.Equal(7, track.TrackId);
        Assert.Same(box, track.Box);
    }
}
