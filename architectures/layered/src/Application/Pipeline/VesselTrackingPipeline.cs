using LayeredArchitecture.Application.Services;

namespace LayeredArchitecture.Application.Pipeline;

// Orchestrates the per-frame sequence from DeepSORVF's main.py loop: AIS処理 -> 検出 ->
// 追跡 -> 融合, in that order, with each step's output feeding directly into the next.
public sealed class VesselTrackingPipeline
{
    private readonly AisService _aisService;
    private readonly DetectionService _detectionService;
    private readonly TrackingService _trackingService;
    private readonly FusionService _fusionService;

    public VesselTrackingPipeline(
        AisService aisService,
        DetectionService detectionService,
        TrackingService trackingService,
        FusionService fusionService)
    {
        _aisService = aisService;
        _detectionService = detectionService;
        _trackingService = trackingService;
        _fusionService = fusionService;
    }

    public FrameResult ProcessFrame(string aisDirectoryPath, int frameIndex, DateTimeOffset timestamp)
    {
        var aisRecords = _aisService.GetValidRecordsAt(aisDirectoryPath, timestamp);
        var detections = _detectionService.Detect(frameIndex, timestamp);
        var visualTracks = _trackingService.Track(detections);
        var fusedTracks = _fusionService.Fuse(visualTracks, aisRecords, timestamp);

        return new FrameResult(frameIndex, timestamp, aisRecords, visualTracks, fusedTracks);
    }

    public IReadOnlyList<FrameResult> ProcessFrames(
        string aisDirectoryPath,
        DateTimeOffset startTimeUtc,
        int frameCount,
        TimeSpan frameInterval)
    {
        var results = new List<FrameResult>(frameCount);
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var timestamp = startTimeUtc + (frameInterval * frameIndex);
            results.Add(ProcessFrame(aisDirectoryPath, frameIndex, timestamp));
        }

        return results;
    }
}
