using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Geometry;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Application.Pipeline;

// Orchestrates the per-frame sequence from DeepSORVF's main.py loop: AIS処理 -> 検出 ->
// 追跡 -> 融合, in that order, with each step's output feeding directly into the next.
public sealed class VesselTrackingPipeline
{
    private readonly ICameraParametersRepository _cameraParametersRepository;
    private readonly AisService _aisService;
    private readonly DetectionService _detectionService;
    private readonly TrackingService _trackingService;
    private readonly FusionService _fusionService;

    public VesselTrackingPipeline(
        ICameraParametersRepository cameraParametersRepository,
        AisService aisService,
        DetectionService detectionService,
        TrackingService trackingService,
        FusionService fusionService)
    {
        _cameraParametersRepository = cameraParametersRepository;
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
        TimeSpan frameInterval)
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
            var visibleAis = _aisService.Process(aisDirectoryPath, camera, timestamp);
            var detections = _detectionService.Detect(visibleAis, timestamp);
            var visualTracks = _trackingService.Track(detections);
            var fusedTracks = _fusionService.Fuse(visualTracks, visibleAis, maxMatchDistancePixels, timestamp);

            results.Add(new FrameResult(frameIndex, timestamp, visibleAis, visualTracks, fusedTracks));
        }

        return results;
    }
}
