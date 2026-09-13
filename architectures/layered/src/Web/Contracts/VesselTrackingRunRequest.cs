namespace LayeredArchitecture.Web.Contracts;

public sealed record VesselTrackingRunRequest(
    string AisDataDirectory,
    DateTimeOffset StartTime,
    int FrameCount,
    int FrameIntervalSeconds);
