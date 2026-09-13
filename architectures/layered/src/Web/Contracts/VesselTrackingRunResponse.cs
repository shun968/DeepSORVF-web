namespace LayeredArchitecture.Web.Contracts;

public sealed record VesselTrackingRunResponse(IReadOnlyList<FrameResultDto> Frames);

public sealed record FrameResultDto(
    int FrameIndex,
    DateTimeOffset Timestamp,
    IReadOnlyList<AisRecordDto> AisRecords,
    IReadOnlyList<VisualTrackDto> Tracks,
    IReadOnlyList<FusedTrackDto> FusedTracks);

public sealed record AisRecordDto(
    long Mmsi,
    double Longitude,
    double Latitude,
    double SpeedKnots,
    double CourseDegrees,
    double HeadingDegrees,
    int ShipType,
    DateTimeOffset Timestamp);

public sealed record VisualTrackDto(int TrackId, double X1, double Y1, double X2, double Y2);

public sealed record FusedTrackDto(int TrackId, long? Mmsi, double X1, double Y1, double X2, double Y2);
