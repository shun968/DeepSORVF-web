using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Adapters;

// Keeps a vessel's track ID from frame to frame by box overlap, the part of DeepSORT's job the
// fusion step depends on. Each detection goes to the track whose last box it overlaps most
// (intersection over union), best overlaps first. A detection that overlaps no free track
// enough starts a new one, and a track that finds no detection is dropped once it has gone
// unmatched for more than MaxMissedFrames frames in a row.
//
// There is no motion model, no appearance re-identification (DeepSORT's ckpt.t7), and no
// counterpart to the paper's anti-occlusion logic, so a vessel hidden for longer than that
// comes back under a new ID. Registered per request, since it remembers one run's tracks.
public sealed class IouTracker : ITracker
{
    private const double MinOverlap = 0.3;
    private const int MaxMissedFrames = 3;

    private readonly List<TrackState> _tracks = [];
    private int _nextId = 1;

    public IReadOnlyList<Track> Track(IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var ids = new int[detections.Count];
        var matched = new HashSet<TrackState>();
        var pairs = (
            from track in _tracks
            from index in Enumerable.Range(0, detections.Count)
            let overlap = Overlap(track.Last, detections[index])
            where overlap >= MinOverlap
            orderby overlap descending
            select (Track: track, Index: index)).ToList();

        foreach (var (track, index) in pairs)
        {
            if (ids[index] != 0 || !matched.Add(track))
            {
                continue;
            }

            track.Last = detections[index];
            track.Missed = 0;
            ids[index] = track.Id;
        }

        foreach (var track in _tracks)
        {
            if (!matched.Contains(track))
            {
                track.Missed++;
            }
        }

        _tracks.RemoveAll(track => track.Missed > MaxMissedFrames);

        for (var index = 0; index < detections.Count; index++)
        {
            if (ids[index] == 0)
            {
                var track = new TrackState(_nextId++, detections[index]);
                _tracks.Add(track);
                ids[index] = track.Id;
            }
        }

        return detections.Select((detection, index) => new Track(ids[index], detection)).ToList();
    }

    private static double Overlap(Detection a, Detection b)
    {
        var width = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
        var height = Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var intersection = width * height;
        return intersection / ((a.Width * a.Height) + (b.Width * b.Height) - intersection);
    }

    private sealed class TrackState(int id, Detection last)
    {
        public int Id { get; } = id;
        public Detection Last { get; set; } = last;
        public int Missed { get; set; }
    }
}
