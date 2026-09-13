using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Web.Contracts;

namespace LayeredArchitecture.Web.Mapping;

public static class FrameResultMapper
{
    public static VesselTrackingRunResponse ToResponse(IReadOnlyList<FrameResult> frames) =>
        new(frames.Select(ToDto).ToList());

    private static FrameResultDto ToDto(FrameResult frame) => new(
        frame.FrameIndex,
        frame.Timestamp,
        frame.AisRecords.Select(ToDto).ToList(),
        frame.VisualTracks.Select(ToDto).ToList(),
        frame.FusedTracks.Select(ToDto).ToList());

    private static VisibleAisRecordDto ToDto(ProjectedAisRecord projected) => new(
        projected.Record.Mmsi,
        projected.Record.Longitude,
        projected.Record.Latitude,
        projected.Record.SpeedKnots,
        projected.Record.CourseDegrees,
        projected.Record.HeadingDegrees,
        projected.Record.ShipType,
        projected.Record.Timestamp,
        projected.X,
        projected.Y);

    private static VisualTrackDto ToDto(VisualTrack track) =>
        new(track.TrackId, track.Box.X1, track.Box.Y1, track.Box.X2, track.Box.Y2);

    private static FusedTrackDto ToDto(FusedTrack fused) =>
        new(fused.TrackId, fused.MatchedAis?.Mmsi, fused.Box.X1, fused.Box.Y1, fused.Box.X2, fused.Box.Y2);
}
