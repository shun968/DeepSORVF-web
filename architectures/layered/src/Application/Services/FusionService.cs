using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// A simplified stand-in for FUSPRO.traj_match: each visual track takes the nearest AIS
// position in image space that no earlier track has claimed, as long as it falls inside
// the same gate the original uses.
//
// The real algorithm compares whole trajectories with DTW over angle and speed features
// rather than single points, which needs the two-minute history of projected AIS positions
// this port does not keep yet. Matching single points at least uses the real projected
// geometry, where the previous implementation could only pair by list position.
public sealed class FusionService
{
    public IReadOnlyList<FusedTrack> Fuse(
        IReadOnlyList<VisualTrack> visualTracks,
        IReadOnlyList<ProjectedAisRecord> visibleAis,
        double maxMatchDistancePixels,
        DateTimeOffset timestamp)
    {
        var unmatched = visibleAis.ToList();
        var fused = new List<FusedTrack>(visualTracks.Count);

        foreach (var track in visualTracks)
        {
            var nearest = NearestWithinGate(track, unmatched, maxMatchDistancePixels);
            if (nearest is not null)
            {
                unmatched.Remove(nearest);
            }

            fused.Add(new FusedTrack(track.TrackId, nearest?.Record, track.Box, timestamp));
        }

        return fused;
    }

    private static ProjectedAisRecord? NearestWithinGate(
        VisualTrack track,
        List<ProjectedAisRecord> candidates,
        double maxMatchDistancePixels)
    {
        ProjectedAisRecord? nearest = null;
        var nearestDistance = double.MaxValue;

        foreach (var candidate in candidates)
        {
            var distance = PixelDistance(track, candidate);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearestDistance <= maxMatchDistancePixels ? nearest : null;
    }

    private static double PixelDistance(VisualTrack track, ProjectedAisRecord ais)
    {
        var dx = track.Box.CenterX - ais.X;
        var dy = track.Box.CenterY - ais.Y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
