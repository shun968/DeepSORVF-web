using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class TrackingServiceTests
{
    private readonly TrackingService _service = new();

    [Fact]
    public void Track_AssignsSequentialIdsStartingAtOne()
    {
        DetectionBox[] detections =
        [
            new(0, 0, 10, 10, DateTimeOffset.UnixEpoch),
            new(20, 20, 30, 30, DateTimeOffset.UnixEpoch),
        ];

        var tracks = _service.Track(detections);

        Assert.Equal([1, 2], tracks.Select(track => track.TrackId));
        Assert.Same(detections[0], tracks[0].Box);
        Assert.Same(detections[1], tracks[1].Box);
    }

    [Fact]
    public void Track_WithNoDetections_ReturnsEmpty()
    {
        var tracks = _service.Track([]);

        Assert.Empty(tracks);
    }
}
