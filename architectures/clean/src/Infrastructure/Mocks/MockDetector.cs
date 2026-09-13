using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Mocks;

// Stands in for YOLOX, whose weights this repository does not carry and which would need a
// video decoder this port does not have (issue #1 and #2 both allow a dummy here).
//
// It reports two boxes that sweep across the frame as a deterministic function of the frame
// index, so a run is reproducible. It does not look at AIS, so which vessel a box ends up
// bound to is arbitrary — what the run demonstrates is that the stages are wired together,
// not that detection is correct.
public sealed class MockDetector : IDetector
{
    private const int BoxHalfWidth = 30;
    private const int BoxHalfHeight = 20;
    private const int VesselCount = 2;
    private const int PixelsPerFrame = 40;

    public IReadOnlyList<Detection> Detect(VideoFrame frame)
    {
        var detections = new List<Detection>(VesselCount);
        for (var vessel = 0; vessel < VesselCount; vessel++)
        {
            var centreX = Wrap((vessel * frame.Width / VesselCount) + (frame.Index * PixelsPerFrame), frame.Width);
            var centreY = frame.Height * 2 / 3;
            detections.Add(new Detection(
                centreX - BoxHalfWidth,
                centreY - BoxHalfHeight,
                centreX + BoxHalfWidth,
                centreY + BoxHalfHeight,
                frame.Timestamp));
        }

        return detections;
    }

    private static int Wrap(int value, int limit) => limit > 0 ? ((value % limit) + limit) % limit : value;
}
