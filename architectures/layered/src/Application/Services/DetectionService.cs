using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// Mock per issue #1: real detection (YOLOX) is explicitly out of scope for this
// architecture-pattern exercise. Boxes are a deterministic function of frameIndex (not
// random) so a ProcessFrames() sequence visibly "moves" a box across frames without any
// real image/video input, which lets TrackingService/FusionService be exercised
// meaningfully despite there being no real detector behind this.
public sealed class DetectionService
{
    public IReadOnlyList<DetectionBox> Detect(int frameIndex, DateTimeOffset timestamp, int count = 2)
    {
        var boxes = new List<DetectionBox>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = (frameIndex * 10) + (i * 100);
            boxes.Add(new DetectionBox(offset, offset, offset + 40, offset + 40, timestamp));
        }

        return boxes;
    }
}
