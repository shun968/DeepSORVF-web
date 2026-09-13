using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Pipeline;

public sealed record FrameResult(
    int FrameIndex,
    DateTimeOffset Timestamp,
    IReadOnlyList<AisRecord> AisRecords,
    IReadOnlyList<VisualTrack> VisualTracks,
    IReadOnlyList<FusedTrack> FusedTracks);
