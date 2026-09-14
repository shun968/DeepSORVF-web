namespace LayeredArchitecture.Web.Contracts;

// A positional record here would let System.Text.Json silently default any omitted
// value-type field (StartTime/FrameCount/FrameIntervalSeconds) to 0 instead of rejecting
// the request ("under-posting") — required properties make deserialization throw when
// the client leaves one out.
public sealed record VesselTrackingRunRequest
{
    public required string AisDataDirectory { get; init; }
    public required string CameraParametersPath { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required int FrameCount { get; init; }
    public required int FrameIntervalSeconds { get; init; }

    // Optional: when set, the run also writes the MOT-format detection/tracking/fusion
    // files the Python original produces into this directory.
    public string? ResultDirectory { get; init; }

    // Optional: when the configured video (RunDefaults:VideoPath) starts at a different moment
    // from the run. Defaults to StartTime.
    public DateTimeOffset? VideoStartTime { get; init; }
}
