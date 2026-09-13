namespace LayeredArchitecture.Domain.Entities;

public sealed class FusedTrack
{
    public int TrackId { get; }
    public AisRecord? MatchedAis { get; }
    public DetectionBox Box { get; }
    public DateTimeOffset Timestamp { get; }

    public FusedTrack(int trackId, AisRecord? matchedAis, DetectionBox box, DateTimeOffset timestamp)
    {
        TrackId = trackId;
        MatchedAis = matchedAis;
        Box = box;
        Timestamp = timestamp;
    }
}
