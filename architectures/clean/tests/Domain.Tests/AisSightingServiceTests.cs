using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Geometry;
using CleanArchitecture.Domain.Services;
using Xunit;

namespace CleanArchitecture.Domain.Tests;

public class AisSightingServiceTests
{
    private const double CameraLongitude = 121.5;
    private const double CameraLatitude = 29.87;
    private static readonly DateTimeOffset Start = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Due east, tilted 5° down, 20m up, 55°x35° field of view on a 1920x1080 sensor.
    private static readonly CameraGeometry Camera = new(new CameraParameters(
        longitudeDegrees: CameraLongitude,
        latitudeDegrees: CameraLatitude,
        bearingDegrees: 90,
        tiltDegrees: 5,
        heightMeters: 20,
        horizontalFovDegrees: 55,
        verticalFovDegrees: 35,
        focalLengthX: 1500,
        focalLengthY: 1500,
        principalPointX: 960,
        principalPointY: 540));

    private static AisRecord VesselAt(
        long mmsi,
        double bearingDegrees,
        double distanceMeters,
        DateTimeOffset timestamp,
        double speedKnots = 8,
        double courseDegrees = 270)
    {
        var (latitude, longitude) = GeoMath.Destination(CameraLatitude, CameraLongitude, bearingDegrees, distanceMeters);
        return new AisRecord(mmsi, longitude, latitude, speedKnots, courseDegrees, courseDegrees, 70, timestamp);
    }

    [Fact]
    public void Assemble_ProjectsVesselsInsideTheFieldOfView()
    {
        var service = new AisSightingService();

        var visible = service.Assemble([VesselAt(431234567, 90, 800, Start)], Camera, Start);

        var vessel = Assert.Single(visible);
        Assert.Equal(431234567, vessel.Record.Mmsi);
        Assert.InRange(vessel.X, 958, 962);
        Assert.True(vessel.Y > 540, $"a vessel below the horizon should project below the principal point, got {vessel.Y}");
    }

    [Fact]
    public void Assemble_DropsVesselsOutsideTheHorizontalFieldOfView()
    {
        var service = new AisSightingService();

        Assert.Empty(service.Assemble([VesselAt(431555001, 0, 900, Start)], Camera, Start));
    }

    [Fact]
    public void Assemble_DropsVesselsBeyondTwoNauticalMiles()
    {
        var service = new AisSightingService();

        Assert.Empty(service.Assemble([VesselAt(431222999, 90, 4500, Start)], Camera, Start));
    }

    [Fact]
    public void Assemble_DropsRecordsFailingTheSentinelChecks()
    {
        var service = new AisSightingService();
        var stationary = VesselAt(431234567, 90, 800, Start, speedKnots: 0.1);

        Assert.Empty(service.Assemble([stationary], Camera, Start));
    }

    [Fact]
    public void Assemble_CarriesVesselsForwardWhenNoMessageArrives()
    {
        var service = new AisSightingService();
        var first = service.Assemble([VesselAt(431234567, 90, 800, Start)], Camera, Start);

        var later = service.Assemble([], Camera, Start.AddSeconds(60));

        var carried = Assert.Single(later);
        Assert.Equal(Start.AddSeconds(60), carried.Record.Timestamp);
        var closingMeters = Camera.DistanceMeters(first[0].Record.Longitude, first[0].Record.Latitude)
            - Camera.DistanceMeters(carried.Record.Longitude, carried.Record.Latitude);
        Assert.Equal(8 * 1852 / 60.0, closingMeters, 0);
    }

    [Fact]
    public void Assemble_DeadReckonsMessagesStampedForAnEarlierSecond()
    {
        var service = new AisSightingService();
        var stale = VesselAt(431234567, 90, 800, Start.AddSeconds(-30));

        var visible = service.Assemble([stale], Camera, Start);

        var vessel = Assert.Single(visible);
        Assert.Equal(Start, vessel.Record.Timestamp);
        Assert.True(
            Camera.DistanceMeters(vessel.Record.Longitude, vessel.Record.Latitude) < 800,
            "a vessel closing on the camera should have been advanced towards it");
    }

    [Fact]
    public void Assemble_DropsRecordsThatJumpImplausiblyFromTheSecondBefore()
    {
        var service = new AisSightingService();
        service.Assemble([VesselAt(431234567, 90, 800, Start)], Camera, Start);
        var reference = VesselAt(431234567, 90, 800, Start.AddSeconds(1));
        var jumped = new AisRecord(
            reference.Mmsi,
            reference.Longitude + 2,
            reference.Latitude,
            reference.SpeedKnots,
            reference.CourseDegrees,
            reference.HeadingDegrees,
            reference.ShipType,
            reference.Timestamp);

        var second = service.Assemble([jumped], Camera, Start.AddSeconds(1));

        // The jumped message is discarded, so the vessel is dead reckoned instead.
        var carried = Assert.Single(second);
        Assert.InRange(carried.Record.Longitude, 121.5, 121.51);
    }

    [Fact]
    public void Assemble_JudgesAJumpAgainstTheLastMessageWhenASecondCarriesSeveral()
    {
        var service = new AisSightingService();
        service.Assemble(
            [VesselAt(431234567, 90, 700, Start, speedKnots: 8), VesselAt(431234567, 90, 700, Start, speedKnots: 20)],
            Camera,
            Start);

        // 20kt matches the last of the two messages exactly, but differs from the first by
        // more than the 7kt jump threshold — so this is only accepted if the last one wins.
        var second = service.Assemble(
            [VesselAt(431234567, 90, 900, Start.AddSeconds(1), speedKnots: 20)],
            Camera,
            Start.AddSeconds(1));

        var vessel = Assert.Single(second);
        Assert.Equal(900, Camera.DistanceMeters(vessel.Record.Longitude, vessel.Record.Latitude), 0);
    }

    [Fact]
    public void Assemble_WithNoRecords_ReturnsEmpty()
    {
        Assert.Empty(new AisSightingService().Assemble([], Camera, Start));
    }
}
