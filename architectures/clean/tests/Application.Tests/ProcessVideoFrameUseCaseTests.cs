using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Geometry;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Domain.Services;
using Moq;
using Xunit;

namespace CleanArchitecture.Application.Tests;

// Every collaborator is an interface the Domain owns, so unlike the layered implementation
// each stage can be mocked individually — which is exactly the property issue #2 wanted to
// check. These tests lean on that: the detector, tracker and fusion engine are all fakes.
public class ProcessVideoFrameUseCaseTests
{
    private const string AisDirectory = "/ais";
    private static readonly DateTimeOffset Timestamp = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CameraGeometry Camera = new(new CameraParameters(
        longitudeDegrees: 121.5,
        latitudeDegrees: 29.87,
        bearingDegrees: 90,
        tiltDegrees: 5,
        heightMeters: 20,
        horizontalFovDegrees: 55,
        verticalFovDegrees: 35,
        focalLengthX: 1500,
        focalLengthY: 1500,
        principalPointX: 960,
        principalPointY: 540));

    private static ProcessVideoFrameRequest Request() =>
        new(AisDirectory, Camera, 540, FrameIndex: 2, Timestamp, ImageWidth: 1920, ImageHeight: 1080);

    private static AisRecord VesselAt(long mmsi, double bearingDegrees, double distanceMeters)
    {
        var (latitude, longitude) = GeoMath.Destination(29.87, 121.5, bearingDegrees, distanceMeters);
        return new AisRecord(mmsi, longitude, latitude, 8, 270, 270, 70, Timestamp);
    }

    private static Detection DetectionAt(double centreX, double centreY) =>
        new(centreX - 30, centreY - 20, centreX + 30, centreY + 20, Timestamp);

    [Fact]
    public void Execute_RunsTheStagesInOrderAndFeedsEachOneTheLastOnesOutput()
    {
        var detection = DetectionAt(960, 700);
        var track = new Track(1, detection);
        var vessel = VesselAt(431234567, 90, 800);

        var aisReader = new Mock<IAisReader>();
        aisReader.Setup(r => r.ReadAt(AisDirectory, Timestamp)).Returns([vessel]);
        var detector = new Mock<IDetector>();
        detector.Setup(d => d.Detect(It.IsAny<VideoFrame>())).Returns([detection]);
        var tracker = new Mock<ITracker>();
        tracker.Setup(t => t.Track(It.IsAny<IReadOnlyList<Detection>>(), Timestamp)).Returns([track]);
        var fusion = new Mock<IFusionEngine>();
        fusion
            .Setup(f => f.Fuse(
                It.IsAny<IReadOnlyList<Track>>(),
                It.IsAny<IReadOnlyList<VisibleVessel>>(),
                540,
                Timestamp))
            .Returns([new FusionResult(track, vessel, Timestamp)]);

        var useCase = new ProcessVideoFrameUseCase(
            aisReader.Object, new Mock<IVideoFrameReader>().Object, detector.Object, tracker.Object, fusion.Object, new AisSightingService());

        var response = useCase.Execute(Request());

        // The detector was handed the frame the request described.
        detector.Verify(d => d.Detect(It.Is<VideoFrame>(f =>
            f.Index == 2 && f.Timestamp == Timestamp && f.Width == 1920 && f.Height == 1080)));
        // The tracker got the detector's boxes, and fusion got the tracker's tracks plus the
        // vessels the AIS stage projected.
        tracker.Verify(t => t.Track(It.Is<IReadOnlyList<Detection>>(d => d.Single() == detection), Timestamp));
        fusion.Verify(f => f.Fuse(
            It.Is<IReadOnlyList<Track>>(t => t.Single() == track),
            It.Is<IReadOnlyList<VisibleVessel>>(v => v.Single().Record.Mmsi == 431234567),
            540,
            Timestamp));

        Assert.Equal(2, response.FrameIndex);
        Assert.Equal(431234567, Assert.Single(response.Vessels).Record.Mmsi);
        Assert.Same(track, Assert.Single(response.Tracks));
        Assert.Equal(431234567, Assert.Single(response.Fusions).MatchedVessel!.Mmsi);
    }

    [Fact]
    public void Execute_WithADetectorThatSeesNothing_StillReportsTheVesselsAisKnowsAbout()
    {
        var aisReader = new Mock<IAisReader>();
        aisReader.Setup(r => r.ReadAt(AisDirectory, Timestamp)).Returns([VesselAt(431234567, 90, 800)]);
        var detector = new Mock<IDetector>();
        detector.Setup(d => d.Detect(It.IsAny<VideoFrame>())).Returns([]);
        var tracker = new Mock<ITracker>();
        tracker.Setup(t => t.Track(It.IsAny<IReadOnlyList<Detection>>(), Timestamp)).Returns([]);
        var fusion = new Mock<IFusionEngine>();
        fusion
            .Setup(f => f.Fuse(
                It.IsAny<IReadOnlyList<Track>>(),
                It.IsAny<IReadOnlyList<VisibleVessel>>(),
                It.IsAny<double>(),
                It.IsAny<DateTimeOffset>()))
            .Returns([]);

        var useCase = new ProcessVideoFrameUseCase(
            aisReader.Object, new Mock<IVideoFrameReader>().Object, detector.Object, tracker.Object, fusion.Object, new AisSightingService());

        var response = useCase.Execute(Request());

        Assert.Single(response.Vessels);
        Assert.Empty(response.Tracks);
        Assert.Empty(response.Fusions);
    }

    [Fact]
    public void Execute_WithAVideo_HandsTheDetectorThePictureAtTheRequestedPosition()
    {
        var image = new FrameImage(4, 2, new byte[4 * 2 * 3]);
        var videoFrameReader = new Mock<IVideoFrameReader>();
        videoFrameReader.Setup(r => r.ReadAt("/video.mp4", TimeSpan.FromSeconds(11))).Returns(image);
        var detector = new Mock<IDetector>();
        var useCase = UseCaseSeeingNothing(videoFrameReader.Object, detector);

        useCase.Execute(Request() with { VideoPath = "/video.mp4", VideoPosition = TimeSpan.FromSeconds(11) });

        // The picture's own size replaces the one the calibration implies.
        detector.Verify(d => d.Detect(It.Is<VideoFrame>(f => f.Image == image && f.Width == 4 && f.Height == 2)));
    }

    [Fact]
    public void Execute_WithoutAVideo_ReadsNoPictureAndHandsTheDetectorAnEmptyFrame()
    {
        var videoFrameReader = new Mock<IVideoFrameReader>();
        var detector = new Mock<IDetector>();
        var useCase = UseCaseSeeingNothing(videoFrameReader.Object, detector);

        useCase.Execute(Request());

        videoFrameReader.VerifyNoOtherCalls();
        detector.Verify(d => d.Detect(It.Is<VideoFrame>(f => f.Image == null && f.Width == 1920 && f.Height == 1080)));
    }

    private static ProcessVideoFrameUseCase UseCaseSeeingNothing(IVideoFrameReader videoFrameReader, Mock<IDetector> detector)
    {
        var aisReader = new Mock<IAisReader>();
        aisReader.Setup(r => r.ReadAt(It.IsAny<string>(), It.IsAny<DateTimeOffset>())).Returns([]);
        detector.Setup(d => d.Detect(It.IsAny<VideoFrame>())).Returns([]);
        var tracker = new Mock<ITracker>();
        tracker.Setup(t => t.Track(It.IsAny<IReadOnlyList<Detection>>(), It.IsAny<DateTimeOffset>())).Returns([]);
        var fusion = new Mock<IFusionEngine>();
        fusion
            .Setup(f => f.Fuse(
                It.IsAny<IReadOnlyList<Track>>(),
                It.IsAny<IReadOnlyList<VisibleVessel>>(),
                It.IsAny<double>(),
                It.IsAny<DateTimeOffset>()))
            .Returns([]);

        return new ProcessVideoFrameUseCase(
            aisReader.Object, videoFrameReader, detector.Object, tracker.Object, fusion.Object, new AisSightingService());
    }
}
