namespace CleanArchitecture.Web.Contracts;

// A positional record would let System.Text.Json silently default any omitted value-type
// field to zero instead of rejecting the request; required properties make deserialization
// throw when the client leaves one out.
public sealed record VesselTrackingRunRequest
{
    public required string AisDataDirectory { get; init; }
    public required string CameraParametersPath { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required int FrameCount { get; init; }
    public required int FrameIntervalSeconds { get; init; }
}

public sealed record VesselTrackingRunResponse(int ImageWidth, int ImageHeight, IReadOnlyList<FrameDto> Frames);

public sealed record FrameDto(
    int FrameIndex,
    DateTimeOffset Timestamp,
    IReadOnlyList<VisibleVesselDto> Vessels,
    IReadOnlyList<TrackDto> Tracks,
    IReadOnlyList<FusionResultDto> Fusions);

public sealed record VisibleVesselDto(
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

public sealed record TrackDto(int TrackId, double X1, double Y1, double X2, double Y2);

public sealed record FusionResultDto(int TrackId, long? Mmsi, double X1, double Y1, double X2, double Y2);
