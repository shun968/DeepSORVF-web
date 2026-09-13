namespace CleanArchitecture.Domain.Entities;

// A track with whatever AIS identity fusion could give it — null when the vessel carries
// no transponder, or when nothing plausible was in range.
public sealed class FusionResult
{
    public Track Track { get; }
    public AisRecord? MatchedVessel { get; }
    public DateTimeOffset Timestamp { get; }

    public FusionResult(Track track, AisRecord? matchedVessel, DateTimeOffset timestamp)
    {
        Track = track;
        MatchedVessel = matchedVessel;
        Timestamp = timestamp;
    }
}
