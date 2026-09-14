using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Geometry;
using LayeredArchitecture.Domain.Repositories;
using Moq;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

// Exercises the real AisService/DetectionService/TrackingService/FusionService together —
// only the two repositories, which are the actual DI boundaries, are faked. None of the
// services expose virtual members or interfaces to mock individually, so this verifies the
// orchestration wiring end to end rather than the call sequence in isolation.
public class VesselTrackingPipelineTests
{
    private const string AisDirectory = "/ais";
    private const string CameraPath = "/camera.txt";
    private const double CameraLongitude = 121.5;
    private const double CameraLatitude = 29.87;
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CameraParameters Parameters = new(
        longitudeDegrees: CameraLongitude,
        latitudeDegrees: CameraLatitude,
        bearingDegrees: 90,
        tiltDegrees: 5,
        heightMeters: 20,
        horizontalFovDegrees: 55,
        verticalFovDegrees: 35,
        focalLengthX: 1500,
        focalLengthY: 1500,
        principalPointX: 960,
        principalPointY: 540);

    private static VesselTrackingPipeline CreatePipeline(params AisRecord[] records) =>
        CreatePipeline(new Mock<IMotResultWriter>(), records);

    private static VesselTrackingPipeline CreatePipeline(Mock<IMotResultWriter> motResultWriter, params AisRecord[] records) =>
        CreatePipeline(motResultWriter, new Mock<IVideoFrameRepository>(), [], records);

    // detections: the boxes the detector reports on every picture the video yields.
    private static VesselTrackingPipeline CreatePipeline(
        Mock<IMotResultWriter> motResultWriter,
        Mock<IVideoFrameRepository> videoFrameRepository,
        IReadOnlyList<(double CentreX, double CentreY)> detections,
        params AisRecord[] records)
    {
        var vesselDetector = new Mock<IVesselDetector>();
        vesselDetector
            .Setup(d => d.Detect(It.IsAny<FrameImage>(), It.IsAny<DateTimeOffset>()))
            .Returns<FrameImage, DateTimeOffset>((_, at) => detections
                .Select(centre => new DetectionBox(centre.CentreX - 30, centre.CentreY - 20, centre.CentreX + 30, centre.CentreY + 20, at))
                .ToList());
        var aisRepository = new Mock<IAisRepository>();
        aisRepository
            .Setup(r => r.GetRecordsAt(AisDirectory, It.IsAny<DateTimeOffset>()))
            .Returns<string, DateTimeOffset>((_, at) => at == Start ? records : []);
        var cameraRepository = new Mock<ICameraParametersRepository>();
        cameraRepository.Setup(r => r.Load(CameraPath)).Returns(Parameters);

        return new VesselTrackingPipeline(
            cameraRepository.Object,
            motResultWriter.Object,
            new AisService(aisRepository.Object),
            new DetectionService(videoFrameRepository.Object, vesselDetector.Object),
            new TrackingService(),
            new FusionService());
    }

    private static AisRecord VesselAt(long mmsi, double bearingDegrees, double distanceMeters)
    {
        var (latitude, longitude) = GeoMath.Destination(CameraLatitude, CameraLongitude, bearingDegrees, distanceMeters);
        return new AisRecord(mmsi, longitude, latitude, 8, 270, 270, 70, Start);
    }

    [Fact]
    public void ProcessFrames_BindsADetectedVesselToItsAis()
    {
        // The vessel 800m due east projects to about (960, 709); the detector sees it there, and
        // a second vessel with no AIS far off to the left.
        var videoFrameRepository = PicturesEverywhere();
        var pipeline = CreatePipeline(
            new Mock<IMotResultWriter>(),
            videoFrameRepository,
            [(960, 705), (200, 700)],
            VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800));

        var run = pipeline.ProcessFrames(AisDirectory, CameraPath, Start, 1, TimeSpan.FromSeconds(1), videoPath: VideoPath);

        var frame = Assert.Single(run.Frames);
        Assert.Equal(431234567, Assert.Single(frame.AisRecords).Record.Mmsi);
        Assert.Equal([1, 2], frame.VisualTracks.Select(track => track.TrackId));
        Assert.Equal(431234567, frame.FusedTracks[0].MatchedAis!.Mmsi);
        Assert.Null(frame.FusedTracks[1].MatchedAis);
    }

    [Fact]
    public void ProcessFrames_WithoutAVideo_DetectsNothing()
    {
        var videoFrameRepository = PicturesEverywhere();
        var pipeline = CreatePipeline(new Mock<IMotResultWriter>(), videoFrameRepository, [(960, 705)], VesselAt(431234567, 90, 800));

        var frame = Assert.Single(pipeline.ProcessFrames(AisDirectory, CameraPath, Start, 1, TimeSpan.FromSeconds(1)).Frames);

        Assert.Empty(frame.VisualTracks);
        Assert.Empty(frame.FusedTracks);
        videoFrameRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessFrames_TakesEachFramesPictureFromItsMomentInTheVideo()
    {
        var videoFrameRepository = PicturesEverywhere();
        var pipeline = CreatePipeline(new Mock<IMotResultWriter>(), videoFrameRepository, []);

        pipeline.ProcessFrames(
            AisDirectory, CameraPath, Start, 3, TimeSpan.FromSeconds(10), videoPath: VideoPath, videoStartTime: Start.AddSeconds(-5));

        // The video started five seconds before the run, so each frame is five seconds further in.
        foreach (var seconds in new[] { 5, 15, 25 })
        {
            videoFrameRepository.Verify(r => r.ReadAt(VideoPath, TimeSpan.FromSeconds(seconds)));
        }
    }

    private const string VideoPath = "/video.mp4";

    private static Mock<IVideoFrameRepository> PicturesEverywhere()
    {
        var videoFrameRepository = new Mock<IVideoFrameRepository>();
        videoFrameRepository
            .Setup(r => r.ReadAt(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(new FrameImage(2, 2, new byte[12]));
        return videoFrameRepository;
    }

    [Fact]
    public void ProcessFrames_ProducesOneResultPerFrameWithAdvancingTimestamps()
    {
        var pipeline = CreatePipeline();
        var interval = TimeSpan.FromSeconds(10);

        var results = pipeline.ProcessFrames(AisDirectory, CameraPath, Start, frameCount: 3, interval).Frames;

        Assert.Equal([0, 1, 2], results.Select(frame => frame.FrameIndex));
        Assert.Equal([Start, Start + interval, Start + (interval * 2)], results.Select(frame => frame.Timestamp));
    }

    [Fact]
    public void ProcessFrames_ReportsTheImageSizeFromThePrincipalPoint()
    {
        var run = CreatePipeline().ProcessFrames(AisDirectory, CameraPath, Start, 1, TimeSpan.FromSeconds(1));

        // The principal point sits at the image centre: 960x540 means a 1920x1080 frame.
        Assert.Equal(1920, run.ImageWidth);
        Assert.Equal(1080, run.ImageHeight);
    }

    [Fact]
    public void ProcessFrames_WithoutAResultDirectory_WritesNothing()
    {
        var writer = new Mock<IMotResultWriter>();
        var pipeline = CreatePipeline(writer, VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800));

        pipeline.ProcessFrames(AisDirectory, CameraPath, Start, 1, TimeSpan.FromSeconds(1));

        writer.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessFrames_WithAResultDirectory_WritesAFilePerResultKind()
    {
        var writer = new Mock<IMotResultWriter>();
        var pipeline = CreatePipeline(writer, VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800));

        pipeline.ProcessFrames(AisDirectory, CameraPath, Start, 1, TimeSpan.FromSeconds(1), "/results");

        foreach (var kind in new[] { MotResultKind.Detection, MotResultKind.Tracking, MotResultKind.Fusion })
        {
            writer.Verify(w => w.Write("/results", kind, It.IsAny<IReadOnlyList<MotResultRow>>()), Times.Once);
        }
    }

    [Fact]
    public void ProcessFrames_MovesAVesselAcrossFramesByDeadReckoning()
    {
        var pipeline = CreatePipeline(VesselAt(431234567, bearingDegrees: 90, distanceMeters: 1500));

        var results = pipeline.ProcessFrames(AisDirectory, CameraPath, Start, frameCount: 3, TimeSpan.FromSeconds(60)).Frames;

        // Closing on the camera at 8kt, so it should appear progressively lower in frame.
        var ys = results.Select(frame => Assert.Single(frame.AisRecords).Y).ToList();
        Assert.True(ys[0] < ys[1] && ys[1] < ys[2], $"expected the vessel to descend the frame, got {string.Join(", ", ys)}");
    }
}
