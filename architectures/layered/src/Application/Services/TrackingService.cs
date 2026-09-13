using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// Mock per issue #1: real tracking (DeepSORT) is out of scope, so a track ID is just the
// detection's position in the list. IDs only stay stable frame to frame because
// DetectionService emits its boxes in a deterministic order — nothing here re-identifies a
// vessel, and the paper's anti-occlusion logic has no counterpart either.
//
// It does keep the trailing window of past positions (VISPRO's Vis_tra), because
// trajectory matching compares paths rather than single points.
public sealed class TrackingService
{
    // Matched to the AIS side's window so both trajectories cover the same span.
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(2);

    private readonly List<VisualTrack> _history = [];

    public VisualFrame Track(IReadOnlyList<DetectionBox> detections, DateTimeOffset timestamp)
    {
        var current = new List<VisualTrack>(detections.Count);
        for (var i = 0; i < detections.Count; i++)
        {
            current.Add(new VisualTrack(i + 1, detections[i]));
        }

        _history.AddRange(current);
        _history.RemoveAll(track => track.Box.Timestamp < timestamp - HistoryWindow);

        return new VisualFrame(current, _history.ToList());
    }
}
