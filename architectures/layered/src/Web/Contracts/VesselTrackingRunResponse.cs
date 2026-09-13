namespace LayeredArchitecture.Web.Contracts;

public sealed record VesselTrackingRunResponse(double ImageWidth, double ImageHeight, IReadOnlyList<FrameResultDto> Frames);

public sealed record FrameResultDto(
    int FrameIndex,
    DateTimeOffset Timestamp,
    IReadOnlyList<VisibleAisRecordDto> AisRecords,
    IReadOnlyList<VisualTrackDto> Tracks,
    IReadOnlyList<FusedTrackDto> FusedTracks);

// An AIS record the camera can see, with the pixel coordinates its position projects to.
public sealed record VisibleAisRecordDto(
    long Mmsi,
    double Longitude,
    double Latitude,
    double SpeedKnots,
    double CourseDegrees,
    double HeadingDegrees,
    int ShipType,
    DateTimeOffset Timestamp,
    int X,
    int Y);

public sealed record VisualTrackDto(int TrackId, double X1, double Y1, double X2, double Y2);

public sealed record FusedTrackDto(int TrackId, long? Mmsi, double X1, double Y1, double X2, double Y2);
