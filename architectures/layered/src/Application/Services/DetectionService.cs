using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Application.Services;

// Ported from utils/VIS_utils.py's VISPRO.detection: takes the picture of the moment being
// processed from the run's video and asks YOLOX (behind IVesselDetector) which vessels it shows.
// A run without a video, or a moment the video does not cover, has nothing to detect.
public sealed class DetectionService
{
    private readonly IVideoFrameRepository _videoFrameRepository;
    private readonly IVesselDetector _vesselDetector;

    public DetectionService(IVideoFrameRepository videoFrameRepository, IVesselDetector vesselDetector)
    {
        _videoFrameRepository = videoFrameRepository;
        _vesselDetector = vesselDetector;
    }

    public IReadOnlyList<DetectionBox> Detect(string? videoPath, TimeSpan videoPosition, DateTimeOffset timestamp)
    {
        if (videoPath is null)
        {
            return [];
        }

        var image = _videoFrameRepository.ReadAt(videoPath, videoPosition);
        return image is null ? [] : _vesselDetector.Detect(image, timestamp);
    }
}
