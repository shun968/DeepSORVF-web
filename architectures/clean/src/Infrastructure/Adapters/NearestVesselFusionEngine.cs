using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Adapters;

// Each track takes the nearest vessel in image space that no earlier track has claimed,
// as long as it falls inside the gate the paper uses (min(width, height) / 2).
//
// The paper matches whole trajectories with DTW instead of single points. That lives behind
// the same interface, so swapping this for one is a DI registration change — which is the
// property issue #2 set out to check.
public sealed class NearestVesselFusionEngine : IFusionEngine
{
    public IReadOnlyList<FusionResult> Fuse(
        IReadOnlyList<Track> tracks,
        IReadOnlyList<VisibleVessel> vessels,
        double maxMatchDistancePixels,
        DateTimeOffset timestamp)
    {
        var unmatched = vessels.ToList();
        var results = new List<FusionResult>(tracks.Count);

        foreach (var track in tracks)
        {
            var nearest = NearestWithinGate(track, unmatched, maxMatchDistancePixels);
            if (nearest is not null)
            {
                unmatched.Remove(nearest);
            }

            results.Add(new FusionResult(track, nearest?.Record, timestamp));
        }

        return results;
    }

    private static VisibleVessel? NearestWithinGate(
        Track track,
        List<VisibleVessel> candidates,
        double maxMatchDistancePixels)
    {
        VisibleVessel? nearest = null;
        var nearestDistance = double.MaxValue;

        foreach (var candidate in candidates)
        {
            var dx = track.Detection.CenterX - candidate.X;
            var dy = track.Detection.CenterY - candidate.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearestDistance <= maxMatchDistancePixels ? nearest : null;
    }
}
