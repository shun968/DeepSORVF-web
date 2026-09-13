namespace LayeredArchitecture.Web.Contracts;

// A positional record here would let System.Text.Json silently default any omitted
// value-type field (StartTime/FrameCount/FrameIntervalSeconds) to 0 instead of rejecting
// the request ("under-posting") — required properties make deserialization throw when
// the client leaves one out.
public sealed record VesselTrackingRunRequest
{
    public required string AisDataDirectory { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required int FrameCount { get; init; }
    public required int FrameIntervalSeconds { get; init; }
}
