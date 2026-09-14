using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Geometry;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Domain.Services;

namespace CleanArchitecture.Application.UseCases;

public sealed record ProcessVideoFrameRequest(
    string AisDirectoryPath,
    CameraGeometry Camera,
    double MaxMatchDistancePixels,
    int FrameIndex,
    DateTimeOffset Timestamp,
    int ImageWidth,
    int ImageHeight,
    string? VideoPath = null,
    TimeSpan VideoPosition = default);

public sealed record ProcessVideoFrameResponse(
    int FrameIndex,
    DateTimeOffset Timestamp,
    IReadOnlyList<VisibleVessel> Vessels,
    IReadOnlyList<Track> Tracks,
    IReadOnlyList<FusionResult> Fusions);

// One turn of main.py's loop: AIS処理 -> 検出 -> 追跡 -> 融合.
//
// Every step is reached through an interface the Domain owns, so any of them can be
// swapped by changing a DI registration. The order, on the other hand, is written out
// straight down this method — there is no stage abstraction, so adding, reordering or
// parallelising a step means editing this code. That trade-off is the point of the
// exercise (issue #2); patterns 3 and 4 are where the alternative gets tried.
public sealed class ProcessVideoFrameUseCase
{
    private readonly IAisReader _aisReader;
    private readonly IVideoFrameReader _videoFrameReader;
    private readonly IDetector _detector;
    private readonly ITracker _tracker;
    private readonly IFusionEngine _fusionEngine;
    private readonly AisSightingService _aisSightingService;

    public ProcessVideoFrameUseCase(
        IAisReader aisReader,
        IVideoFrameReader videoFrameReader,
        IDetector detector,
        ITracker tracker,
        IFusionEngine fusionEngine,
        AisSightingService aisSightingService)
    {
        _aisReader = aisReader;
        _videoFrameReader = videoFrameReader;
        _detector = detector;
        _tracker = tracker;
        _fusionEngine = fusionEngine;
        _aisSightingService = aisSightingService;
    }

    public ProcessVideoFrameResponse Execute(ProcessVideoFrameRequest request)
    {
        var received = _aisReader.ReadAt(request.AisDirectoryPath, request.Timestamp);
        var vessels = _aisSightingService.Assemble(received, request.Camera, request.Timestamp);

        // Without a video the detector is handed a frame with no picture in it. With one, the
        // picture's own size wins over the size the calibration implies.
        var image = request.VideoPath is null ? null : _videoFrameReader.ReadAt(request.VideoPath, request.VideoPosition);
        var frame = new VideoFrame(
            request.FrameIndex,
            request.Timestamp,
            image?.Width ?? request.ImageWidth,
            image?.Height ?? request.ImageHeight,
            image);
        var detections = _detector.Detect(frame);
        var tracks = _tracker.Track(detections, request.Timestamp);
        var fusions = _fusionEngine.Fuse(tracks, vessels, request.MaxMatchDistancePixels, request.Timestamp);

        return new ProcessVideoFrameResponse(request.FrameIndex, request.Timestamp, vessels, tracks, fusions);
    }
}
