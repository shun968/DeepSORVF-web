using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// Simplified per issue #1's confirmed scope: the real DTW trajectory matching
// (utils/FUS_utils.py FUSPRO.traj_match) compares AIS positions and visual tracks in a
// shared pixel space, which needs the camera-projection math this port deliberately
// leaves out. Positional matching is therefore not meaningful here either, so this pairs
// the i-th visual track with the i-th AIS record by LIST POSITION only. Extra visual
// tracks beyond the AIS record count are left unmatched (MatchedAis == null); extra AIS
// records beyond the track count are simply not represented in the output.
public sealed class FusionService
{
    public IReadOnlyList<FusedTrack> Fuse(
        IReadOnlyList<VisualTrack> visualTracks,
        IReadOnlyList<AisRecord> aisRecords,
        DateTimeOffset timestamp)
    {
        var fused = new List<FusedTrack>(visualTracks.Count);
        for (var i = 0; i < visualTracks.Count; i++)
        {
            var matchedAis = i < aisRecords.Count ? aisRecords[i] : null;
            fused.Add(new FusedTrack(visualTracks[i].TrackId, matchedAis, visualTracks[i].Box, timestamp));
        }

        return fused;
    }
}
