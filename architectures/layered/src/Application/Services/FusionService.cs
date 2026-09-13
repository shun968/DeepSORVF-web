using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Trajectory;

namespace LayeredArchitecture.Application.Services;

// Ported from utils/FUS_utils.py's FUSPRO: binds visual tracks to AIS vessels by comparing
// whole trajectories rather than single positions.
//
// Each frame it scores every track/vessel pair, takes the globally cheapest set of pairings
// (Hungarian assignment), throws out the ones that are too far apart or heading the wrong
// way, and counts how often each pairing recurs. A pairing seen often enough becomes
// "bound": it is then scored far below everything else so the assignment keeps it, and it
// survives a few seconds of not matching — which is what carries an identity through a
// vessel being briefly hidden.
//
// Like AISPRO this is stateful and registered per request, so one instance must see a run's
// frames in order.
public sealed class FusionService
{
    // FUSPRO.bin_num: a pairing has to recur more times than this before it counts as bound.
    private const int BindThreshold = 3;

    // FUSPRO.fog_num: how many seconds a bound pairing survives without being re-matched.
    private const int ForgetAfterSeconds = 3;

    // The original's stand-in for "never pair these", large enough to lose to any real score.
    private const double Unreachable = 1_000_000_000;

    // Pairings more than this far apart in heading are rejected outright.
    private static readonly double MaxAngle = Math.PI * 5 / 6;

    private List<MatchState> _matches = [];

    public IReadOnlyList<FusedTrack> Fuse(
        VisualFrame visual,
        AisFrame ais,
        double maxMatchDistancePixels,
        DateTimeOffset timestamp)
    {
        var tracks = TrackTrajectories(visual);
        var vessels = VesselTrajectories(ais);
        var bound = _matches.Where(match => match.Count > BindThreshold).ToList();

        var costs = BuildCostMatrix(tracks, vessels, bound, maxMatchDistancePixels);
        var assignment = HungarianAlgorithm.Solve(costs);
        var accepted = AcceptablePairs(assignment, tracks, vessels, maxMatchDistancePixels);

        _matches = UpdateMatchCounts(accepted, tracks, vessels, bound, timestamp);

        return BuildResult(visual.Current, tracks, vessels, accepted, timestamp);
    }

    private static double[,] BuildCostMatrix(
        IReadOnlyList<Trajectory> tracks,
        IReadOnlyList<Trajectory> vessels,
        IReadOnlyList<MatchState> bound,
        double maxMatchDistancePixels)
    {
        var boundTrackIds = bound.Select(match => match.TrackId).ToHashSet();
        var boundMmsis = bound.Select(match => match.Mmsi).ToHashSet();
        var costs = new double[tracks.Count, vessels.Count];

        for (var i = 0; i < tracks.Count; i++)
        {
            for (var j = 0; j < vessels.Count; j++)
            {
                var boundPair = bound.FirstOrDefault(match =>
                    match.TrackId == tracks[i].Id && match.Mmsi == vessels[j].Id);

                if (boundPair is not null)
                {
                    // Already bound: score it far below any real similarity so the
                    // assignment has no reason to break the pairing up.
                    costs[i, j] = -boundPair.Count * 100;
                }
                else if (boundTrackIds.Contains(tracks[i].Id) || boundMmsis.Contains(vessels[j].Id))
                {
                    // One of the two is already spoken for by a different partner.
                    costs[i, j] = Unreachable;
                }
                else
                {
                    costs[i, j] = IsPlausible(tracks[i], vessels[j], maxMatchDistancePixels)
                        ? TrajectorySimilarity.Distance(tracks[i].Points, vessels[j].Points)
                        : Unreachable;
                }
            }
        }

        return costs;
    }

    private static List<(int TrackIndex, int VesselIndex)> AcceptablePairs(
        IReadOnlyList<int> assignment,
        IReadOnlyList<Trajectory> tracks,
        IReadOnlyList<Trajectory> vessels,
        double maxMatchDistancePixels)
    {
        var accepted = new List<(int TrackIndex, int VesselIndex)>();
        for (var i = 0; i < assignment.Count; i++)
        {
            // The assignment has to pair everything it can, including pairs that were only
            // ever scored as unreachable, so the distance and heading checks run again here.
            if (assignment[i] >= 0 && IsPlausible(tracks[i], vessels[assignment[i]], maxMatchDistancePixels))
            {
                accepted.Add((i, assignment[i]));
            }
        }

        return accepted;
    }

    private List<MatchState> UpdateMatchCounts(
        IReadOnlyList<(int TrackIndex, int VesselIndex)> accepted,
        IReadOnlyList<Trajectory> tracks,
        IReadOnlyList<Trajectory> vessels,
        IReadOnlyList<MatchState> bound,
        DateTimeOffset timestamp)
    {
        var updated = new List<MatchState>(accepted.Count);
        foreach (var (trackIndex, vesselIndex) in accepted)
        {
            var trackId = tracks[trackIndex].Id;
            var mmsi = vessels[vesselIndex].Id;
            var previous = _matches.FirstOrDefault(match => match.TrackId == trackId && match.Mmsi == mmsi);
            updated.Add(new MatchState(trackId, mmsi, timestamp, (previous?.Count ?? 0) + 1));
        }

        // A bound pairing that went unmatched keeps its count for a few seconds, so a vessel
        // that is briefly hidden does not lose its identity.
        var visibleMmsis = vessels.Select(vessel => vessel.Id).ToHashSet();
        foreach (var match in bound)
        {
            if (visibleMmsis.Contains(match.Mmsi)
                && !updated.Any(entry => entry.TrackId == match.TrackId && entry.Mmsi == match.Mmsi)
                && (timestamp - match.Timestamp).TotalSeconds < ForgetAfterSeconds)
            {
                updated.Add(match);
            }
        }

        return updated;
    }

    private static List<FusedTrack> BuildResult(
        IReadOnlyList<VisualTrack> currentTracks,
        IReadOnlyList<Trajectory> tracks,
        IReadOnlyList<Trajectory> vessels,
        IReadOnlyList<(int TrackIndex, int VesselIndex)> accepted,
        DateTimeOffset timestamp)
    {
        var matchedByTrackId = accepted.ToDictionary(
            pair => tracks[pair.TrackIndex].Id,
            pair => vessels[pair.VesselIndex].Record);

        // The original only reports matched pairs; reporting every current track instead
        // keeps the vessels with no AIS visible in the output.
        return currentTracks
            .Select(track => new FusedTrack(
                track.TrackId,
                matchedByTrackId.GetValueOrDefault(track.TrackId),
                track.Box,
                timestamp))
            .ToList();
    }

    private static bool IsPlausible(Trajectory track, Trajectory vessel, double maxMatchDistancePixels)
    {
        var trackEnd = track.Points[^1];
        var vesselEnd = vessel.Points[^1];
        var dx = trackEnd.X - vesselEnd.X;
        var dy = trackEnd.Y - vesselEnd.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        return distance < maxMatchDistancePixels
            && TrajectorySimilarity.AngleBetween(track.Points, vessel.Points) < MaxAngle;
    }

    // traj_group for the visual side: the path of every track that is on screen now.
    private static List<Trajectory> TrackTrajectories(VisualFrame visual)
    {
        var currentIds = visual.Current.Select(track => track.TrackId).ToHashSet();

        return visual.History
            .Where(track => currentIds.Contains(track.TrackId))
            .GroupBy(track => track.TrackId)
            .Select(group => new Trajectory(
                group.Key,
                group.Select(track => new TrajectoryPoint(track.Box.CenterX, track.Box.CenterY)).ToList(),
                null))
            .ToList();
    }

    // traj_group for the AIS side: the projected path of every vessel the camera sees now.
    private static List<Trajectory> VesselTrajectories(AisFrame ais)
    {
        var visibleMmsis = ais.Visible.Select(record => record.Record.Mmsi).ToHashSet();

        return ais.History
            .Where(record => visibleMmsis.Contains(record.Record.Mmsi))
            .GroupBy(record => record.Record.Mmsi)
            .Select(group => new Trajectory(
                group.Key,
                group.Select(record => new TrajectoryPoint(record.X, record.Y)).ToList(),
                group.Last().Record))
            .ToList();
    }

    private sealed record Trajectory(long Id, IReadOnlyList<TrajectoryPoint> Points, AisRecord? Record);

    private sealed record MatchState(long TrackId, long Mmsi, DateTimeOffset Timestamp, int Count);
}
