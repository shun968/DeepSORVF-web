using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Geometry;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Domain.Services;
using Moq;
using Xunit;

namespace CleanArchitecture.Application.Tests;

public class ProcessVesselTrackingRunUseCaseTests
{
    private const string AisDirectory = "/ais";
    private const string CameraPath = "/camera.txt";
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CameraParameters Parameters = new(
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
        principalPointY: 540);

    private static ProcessVesselTrackingRunUseCase CreateUseCase(
        IFusionEngine fusionEngine,
        params AisRecord[] records)
    {
        var aisReader = new Mock<IAisReader>();
        aisReader
            .Setup(r => r.ReadAt(AisDirectory, It.IsAny<DateTimeOffset>()))
            .Returns<string, DateTimeOffset>((_, at) => at == Start ? records : []);
        var cameraReader = new Mock<ICameraParametersReader>();
        cameraReader.Setup(r => r.Read(CameraPath)).Returns(Parameters);

        // Detection and tracking are irrelevant to what these tests assert, and pulling in
        // Infrastructure's mocks would make the Application tests depend on a layer they
        // should not know about.
        var detector = new Mock<IDetector>();
        detector.Setup(d => d.Detect(It.IsAny<VideoFrame>())).Returns([]);
        var tracker = new Mock<ITracker>();
        tracker.Setup(t => t.Track(It.IsAny<IReadOnlyList<Detection>>(), It.IsAny<DateTimeOffset>())).Returns([]);

        var frameUseCase = new ProcessVideoFrameUseCase(
            aisReader.Object,
            new Mock<IVideoFrameReader>().Object,
            detector.Object,
            tracker.Object,
            fusionEngine,
            new AisSightingService());

        return new ProcessVesselTrackingRunUseCase(cameraReader.Object, frameUseCase);
    }

    private static AisRecord VesselAt(long mmsi, double bearingDegrees, double distanceMeters)
    {
        var (latitude, longitude) = GeoMath.Destination(29.87, 121.5, bearingDegrees, distanceMeters);
        return new AisRecord(mmsi, longitude, latitude, 8, 270, 270, 70, Start);
    }

    [Fact]
    public void Execute_ProducesOneResultPerFrameWithAdvancingTimestamps()
    {
        var useCase = CreateUseCase(new Mock<IFusionEngine>().Object);
        var interval = TimeSpan.FromSeconds(10);

        var response = useCase.Execute(new ProcessVesselTrackingRunRequest(
            AisDirectory, CameraPath, Start, FrameCount: 3, interval));

        Assert.Equal([0, 1, 2], response.Frames.Select(frame => frame.FrameIndex));
        Assert.Equal([Start, Start + interval, Start + (interval * 2)], response.Frames.Select(frame => frame.Timestamp));
    }

    [Fact]
    public void Execute_DerivesTheFrameSizeAndMatchGateFromTheCameraCalibration()
    {
        var fusionEngine = new Mock<IFusionEngine>();
        var useCase = CreateUseCase(fusionEngine.Object);

        var response = useCase.Execute(new ProcessVesselTrackingRunRequest(
            AisDirectory, CameraPath, Start, FrameCount: 1, TimeSpan.FromSeconds(1)));

        // The principal point sits at the image centre: 960x540 means a 1920x1080 frame and
        // a min(width, height) / 2 gate of 540.
        Assert.Equal(1920, response.ImageWidth);
        Assert.Equal(1080, response.ImageHeight);
        fusionEngine.Verify(f => f.Fuse(
            It.IsAny<IReadOnlyList<Track>>(),
            It.IsAny<IReadOnlyList<VisibleVessel>>(),
            540,
            Start));
    }

    [Fact]
    public void Execute_CarriesAVesselAcrossFramesByDeadReckoning()
    {
        var useCase = CreateUseCase(new Mock<IFusionEngine>().Object, VesselAt(431234567, 90, 1500));

        var response = useCase.Execute(new ProcessVesselTrackingRunRequest(
            AisDirectory, CameraPath, Start, FrameCount: 3, TimeSpan.FromSeconds(60)));

        // Closing on the camera at 8kt, so it should appear progressively lower in frame
        // even though only the first second has a message.
        var ys = response.Frames.Select(frame => Assert.Single(frame.Vessels).Y).ToList();
        Assert.True(ys[0] < ys[1] && ys[1] < ys[2], $"expected the vessel to descend the frame, got {string.Join(", ", ys)}");
    }

    [Fact]
    public void Execute_TakesEachFramesPictureFromItsMomentInTheVideo()
    {
        var cameraReader = new Mock<ICameraParametersReader>();
        cameraReader.Setup(r => r.Read(CameraPath)).Returns(Parameters);
        var aisReader = new Mock<IAisReader>();
        aisReader.Setup(r => r.ReadAt(AisDirectory, It.IsAny<DateTimeOffset>())).Returns([]);
        var videoFrameReader = new Mock<IVideoFrameReader>();
        var detector = new Mock<IDetector>();
        detector.Setup(d => d.Detect(It.IsAny<VideoFrame>())).Returns([]);
        var tracker = new Mock<ITracker>();
        tracker.Setup(t => t.Track(It.IsAny<IReadOnlyList<Detection>>(), It.IsAny<DateTimeOffset>())).Returns([]);
        var frameUseCase = new ProcessVideoFrameUseCase(
            aisReader.Object,
            videoFrameReader.Object,
            detector.Object,
            tracker.Object,
            new Mock<IFusionEngine>().Object,
            new AisSightingService());
        var useCase = new ProcessVesselTrackingRunUseCase(cameraReader.Object, frameUseCase);

        useCase.Execute(new ProcessVesselTrackingRunRequest(
            AisDirectory, CameraPath, Start, FrameCount: 3, TimeSpan.FromSeconds(10), "/video.mp4", VideoStartTime: Start.AddSeconds(-5)));

        // The video started five seconds before the run, so each frame is five seconds further in.
        foreach (var seconds in new[] { 5, 15, 25 })
        {
            videoFrameReader.Verify(r => r.ReadAt("/video.mp4", TimeSpan.FromSeconds(seconds)));
        }
    }
}
