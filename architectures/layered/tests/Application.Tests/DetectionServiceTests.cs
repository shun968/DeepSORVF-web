using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;
using Moq;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class DetectionServiceTests
{
    private static readonly DateTimeOffset Timestamp = new(2022, 6, 4, 4, 5, 23, TimeSpan.Zero);
    private static readonly TimeSpan Position = TimeSpan.FromSeconds(11);
    private readonly Mock<IVideoFrameRepository> _videoFrameRepository = new();
    private readonly Mock<IVesselDetector> _vesselDetector = new();

    private DetectionService Service() => new(_videoFrameRepository.Object, _vesselDetector.Object);

    [Fact]
    public void Detect_AsksTheDetectorAboutThePictureAtThatMomentOfTheVideo()
    {
        var image = new FrameImage(2, 2, new byte[12]);
        var box = new DetectionBox(10, 20, 30, 40, Timestamp);
        _videoFrameRepository.Setup(r => r.ReadAt("/video.mp4", Position)).Returns(image);
        _vesselDetector.Setup(d => d.Detect(image, Timestamp)).Returns([box]);

        Assert.Same(box, Assert.Single(Service().Detect("/video.mp4", Position, Timestamp)));
    }

    [Fact]
    public void Detect_WithoutAVideo_DetectsNothing()
    {
        Assert.Empty(Service().Detect(null, Position, Timestamp));
        _videoFrameRepository.VerifyNoOtherCalls();
        _vesselDetector.VerifyNoOtherCalls();
    }

    [Fact]
    public void Detect_AtAMomentTheVideoDoesNotCover_DetectsNothing()
    {
        _videoFrameRepository.Setup(r => r.ReadAt("/video.mp4", Position)).Returns((FrameImage?)null);

        Assert.Empty(Service().Detect("/video.mp4", Position, Timestamp));
        _vesselDetector.VerifyNoOtherCalls();
    }
}
