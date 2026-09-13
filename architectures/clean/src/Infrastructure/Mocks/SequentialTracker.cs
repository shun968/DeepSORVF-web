using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Mocks;

// Stands in for DeepSORT: a track ID is just the detection's position in the list, so IDs
// only stay stable because MockDetector emits its boxes in a fixed order. There is no
// re-identification, and no counterpart to the paper's anti-occlusion logic.
public sealed class SequentialTracker : ITracker
{
    public IReadOnlyList<Track> Track(IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var tracks = new List<Track>(detections.Count);
        for (var i = 0; i < detections.Count; i++)
        {
            tracks.Add(new Track(i + 1, detections[i]));
        }

        return tracks;
    }
}
