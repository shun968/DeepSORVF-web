using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Geometry;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Application.Pipeline;

// Orchestrates the per-frame sequence from DeepSORVF's main.py loop: AIS処理 -> 検出 ->
// 追跡 -> 融合, in that order, with each step's output feeding directly into the next.
public sealed class VesselTrackingPipeline
{
    private readonly ICameraParametersRepository _cameraParametersRepository;
    private readonly IMotResultWriter _motResultWriter;
    private readonly AisService _aisService;
    private readonly DetectionService _detectionService;
    private readonly TrackingService _trackingService;
    private readonly FusionService _fusionService;

    public VesselTrackingPipeline(
        ICameraParametersRepository cameraParametersRepository,
        IMotResultWriter motResultWriter,
        AisService aisService,
        DetectionService detectionService,
        TrackingService trackingService,
        FusionService fusionService)
    {
        _cameraParametersRepository = cameraParametersRepository;
        _motResultWriter = motResultWriter;
        _aisService = aisService;
        _detectionService = detectionService;
        _trackingService = trackingService;
        _fusionService = fusionService;
    }

    public IReadOnlyList<FrameResult> ProcessFrames(
        string aisDirectoryPath,
        string cameraParametersPath,
        DateTimeOffset startTimeUtc,
        int frameCount,
        TimeSpan frameInterval,
        string? resultDirectoryPath = null)
    {
        var parameters = _cameraParametersRepository.Load(cameraParametersPath);
        var camera = new CameraGeometry(parameters);

        // FUSPRO gates a match at min(image width, image height) / 2. The principal point
        // sits at the image centre, so its smaller component is that same half-extent —
        // which saves carrying the image size around for this one use.
        var maxMatchDistancePixels = Math.Min(parameters.PrincipalPointX, parameters.PrincipalPointY);

        var results = new List<FrameResult>(frameCount);
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var timestamp = startTimeUtc + (frameInterval * frameIndex);
            var ais = _aisService.Process(aisDirectoryPath, camera, timestamp);
            var detections = _detectionService.Detect(ais.Visible, timestamp);
            var visual = _trackingService.Track(detections, timestamp);
            var fusedTracks = _fusionService.Fuse(visual, ais, maxMatchDistancePixels, timestamp);

            results.Add(new FrameResult(frameIndex, timestamp, ais.Visible, visual.Current, fusedTracks));
        }

        if (resultDirectoryPath is not null)
        {
            WriteResults(results, parameters, resultDirectoryPath);
        }

        return results;
    }

    private void WriteResults(
        IReadOnlyList<FrameResult> results,
        CameraParameters parameters,
        string resultDirectoryPath)
    {
        // The principal point sits at the image centre, so doubling it recovers the frame
        // size the original clamps its boxes to.
        var imageWidth = parameters.PrincipalPointX * 2;
        var imageHeight = parameters.PrincipalPointY * 2;

        _motResultWriter.Write(
            resultDirectoryPath, MotResultKind.Detection, MotResultRows.Detections(results, imageWidth, imageHeight));
        _motResultWriter.Write(
            resultDirectoryPath, MotResultKind.Tracking, MotResultRows.Tracks(results, imageWidth, imageHeight));
        _motResultWriter.Write(
            resultDirectoryPath, MotResultKind.Fusion, MotResultRows.Fusions(results, imageWidth, imageHeight));
    }
}
