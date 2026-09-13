using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// Mock per issue #1: real tracking (DeepSORT) is out of scope. Assigns a track ID from
// each detection's position in the list — no re-identification across frames, so a track
// ID is only stable frame-to-frame because DetectionService's mock output order is itself
// deterministic, not because this service tracks anything.
public sealed class TrackingService
{
    public IReadOnlyList<VisualTrack> Track(IReadOnlyList<DetectionBox> detections)
    {
        var tracks = new List<VisualTrack>(detections.Count);
        for (var i = 0; i < detections.Count; i++)
        {
            tracks.Add(new VisualTrack(i + 1, detections[i]));
        }

        return tracks;
    }
}
