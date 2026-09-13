using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Web.Contracts;

public static class VesselTrackingMapper
{
    public static VesselTrackingRunResponse ToResponse(ProcessVesselTrackingRunResponse response) =>
        new(response.ImageWidth, response.ImageHeight, response.Frames.Select(ToDto).ToList());

    private static FrameDto ToDto(ProcessVideoFrameResponse frame) => new(
        frame.FrameIndex,
        frame.Timestamp,
        frame.Vessels.Select(ToDto).ToList(),
        frame.Tracks.Select(ToDto).ToList(),
        frame.Fusions.Select(ToDto).ToList());

    private static VisibleVesselDto ToDto(VisibleVessel vessel) => new(
        vessel.Record.Mmsi,
        vessel.Record.Longitude,
        vessel.Record.Latitude,
        vessel.Record.SpeedKnots,
        vessel.Record.CourseDegrees,
        vessel.Record.HeadingDegrees,
        vessel.Record.ShipType,
        vessel.Record.Timestamp,
        vessel.X,
        vessel.Y);

    private static TrackDto ToDto(Track track) => new(
        track.Id, track.Detection.X1, track.Detection.Y1, track.Detection.X2, track.Detection.Y2);

    private static FusionResultDto ToDto(FusionResult fusion) => new(
        fusion.Track.Id,
        fusion.MatchedVessel?.Mmsi,
        fusion.Track.Detection.X1,
        fusion.Track.Detection.Y1,
        fusion.Track.Detection.X2,
        fusion.Track.Detection.Y2);
}
