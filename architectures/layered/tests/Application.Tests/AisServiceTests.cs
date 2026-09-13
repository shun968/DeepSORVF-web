using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Geometry;
using LayeredArchitecture.Domain.Repositories;
using Moq;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class AisServiceTests
{
    private const string AisDirectory = "/ais";
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

    private static (double Latitude, double Longitude) PositionAt(double bearingDegrees, double distanceMeters) =>
        GeoMath.Destination(CameraLatitude, CameraLongitude, bearingDegrees, distanceMeters);

    private static AisRecord VesselAt(
        long mmsi,
        double bearingDegrees,
        double distanceMeters,
        DateTimeOffset timestamp,
        double speedKnots = 8,
        double courseDegrees = 270)
    {
        var (latitude, longitude) = PositionAt(bearingDegrees, distanceMeters);
        return new AisRecord(mmsi, longitude, latitude, speedKnots, courseDegrees, courseDegrees, 70, timestamp);
    }

    private static AisService CreateService(params (DateTimeOffset At, AisRecord[] Records)[] readings)
    {
        var repository = new Mock<IAisRepository>();
        repository
            .Setup(r => r.GetRecordsAt(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns<string, DateTimeOffset>((_, at) =>
                readings.FirstOrDefault(reading => reading.At == at).Records ?? []);

        return new AisService(repository.Object);
    }

    [Fact]
    public void Process_ProjectsVesselsInsideTheFieldOfView()
    {
        var service = CreateService((Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]));

        var visible = service.Process(AisDirectory, Camera, Start).Visible;

        var projected = Assert.Single(visible);
        Assert.Equal(431234567, projected.Record.Mmsi);
        Assert.InRange(projected.X, 958, 962);
        Assert.True(projected.Y > 540, $"a vessel below the horizon should project below the principal point, got {projected.Y}");
    }

    [Fact]
    public void Process_DropsVesselsOutsideTheHorizontalFieldOfView()
    {
        var service = CreateService((Start, [VesselAt(431555001, bearingDegrees: 0, distanceMeters: 900, Start)]));

        Assert.Empty(service.Process(AisDirectory, Camera, Start).Visible);
    }

    [Fact]
    public void Process_DropsVesselsBeyondTwoNauticalMiles()
    {
        var service = CreateService((Start, [VesselAt(431222999, bearingDegrees: 90, distanceMeters: 4500, Start)]));

        Assert.Empty(service.Process(AisDirectory, Camera, Start).Visible);
    }

    [Fact]
    public void Process_DropsRecordsFailingTheSentinelChecks()
    {
        var (latitude, longitude) = PositionAt(bearingDegrees: 90, distanceMeters: 800);
        var stationary = new AisRecord(431234567, longitude, latitude, 0.1, 270, 270, 70, Start);
        var service = CreateService((Start, [stationary]));

        Assert.Empty(service.Process(AisDirectory, Camera, Start).Visible);
    }

    [Fact]
    public void Process_CarriesVesselsForwardByDeadReckoningWhenNoMessageArrives()
    {
        // Heading due west at 8kt, so after 60s it should be about 247m closer to the camera.
        var service = CreateService((Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]));
        var firstFrame = service.Process(AisDirectory, Camera, Start).Visible;

        var laterFrame = service.Process(AisDirectory, Camera, Start.AddSeconds(60)).Visible;

        var carried = Assert.Single(laterFrame);
        Assert.Equal(431234567, carried.Record.Mmsi);
        Assert.Equal(Start.AddSeconds(60), carried.Record.Timestamp);
        var closingMeters = Camera.DistanceMeters(firstFrame[0].Record.Longitude, firstFrame[0].Record.Latitude)
            - Camera.DistanceMeters(carried.Record.Longitude, carried.Record.Latitude);
        Assert.Equal(8 * 1852 / 60.0, closingMeters, 0);
    }

    [Fact]
    public void Process_DropsRecordsThatJumpImplausiblyFromTheSecondBefore()
    {
        var teleported = VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start.AddSeconds(1));
        var jumped = new AisRecord(
            teleported.Mmsi,
            teleported.Longitude + 2,
            teleported.Latitude,
            teleported.SpeedKnots,
            teleported.CourseDegrees,
            teleported.HeadingDegrees,
            teleported.ShipType,
            teleported.Timestamp);
        var service = CreateService(
            (Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]),
            (Start.AddSeconds(1), [jumped]));
        service.Process(AisDirectory, Camera, Start);

        var second = service.Process(AisDirectory, Camera, Start.AddSeconds(1)).Visible;

        // The jumped message is discarded, so the vessel is dead reckoned instead of moved.
        var carried = Assert.Single(second);
        Assert.InRange(carried.Record.Longitude, 121.5, 121.51);
    }

    [Fact]
    public void Process_DeadReckonsMessagesStampedForAnEarlierSecond()
    {
        var stale = VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start.AddSeconds(-30));
        var service = CreateService((Start, [stale]));

        var visible = service.Process(AisDirectory, Camera, Start).Visible;

        var projected = Assert.Single(visible);
        Assert.Equal(Start, projected.Record.Timestamp);
        Assert.True(
            Camera.DistanceMeters(projected.Record.Longitude, projected.Record.Latitude) < 800,
            "a vessel closing on the camera should have been advanced towards it");
    }

    [Fact]
    public void Process_DoesNotDuplicateAVesselReportedAgainTheNextSecond()
    {
        var service = CreateService(
            (Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]),
            (Start.AddSeconds(1), [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 796, Start.AddSeconds(1))]));
        service.Process(AisDirectory, Camera, Start);

        var second = service.Process(AisDirectory, Camera, Start.AddSeconds(1)).Visible;

        Assert.Equal(Start.AddSeconds(1), Assert.Single(second).Record.Timestamp);
    }

    [Fact]
    public void Process_KeepsTheHistoryOfAVesselThatPassesBelowTheFrame()
    {
        // 50m out, a vessel is under the bottom edge of the vertical field of view: it is not
        // projected, but unlike leaving sideways its trajectory is kept.
        var service = CreateService(
            (Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]),
            (Start.AddSeconds(1), [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 50, Start.AddSeconds(1))]));
        service.Process(AisDirectory, Camera, Start);

        var frame = service.Process(AisDirectory, Camera, Start.AddSeconds(1));

        Assert.Empty(frame.Visible);
        Assert.Equal(Start, Assert.Single(frame.History).Record.Timestamp);
    }

    [Fact]
    public void Process_WithNoRecords_ReturnsEmpty()
    {
        Assert.Empty(CreateService().Process(AisDirectory, Camera, Start).Visible);
    }

    [Fact]
    public void Process_AccumulatesAHistoryOfProjectedPositions()
    {
        var service = CreateService((Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]));
        service.Process(AisDirectory, Camera, Start);

        var frame = service.Process(AisDirectory, Camera, Start.AddSeconds(30));

        Assert.Single(frame.Visible);
        Assert.Equal(2, frame.History.Count);
        Assert.Equal([Start, Start.AddSeconds(30)], frame.History.Select(entry => entry.Record.Timestamp));
    }

    [Fact]
    public void Process_ForgetsHistoryOlderThanTwoMinutes()
    {
        var service = CreateService((Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]));
        service.Process(AisDirectory, Camera, Start);

        // Far enough ahead that the vessel is still in view, but the first entry has aged out.
        var frame = service.Process(AisDirectory, Camera, Start.AddSeconds(150));

        Assert.Equal(Start.AddSeconds(150), Assert.Single(frame.History).Record.Timestamp);
    }

    [Fact]
    public void Process_DropsTheHistoryOfAVesselThatLeavesTheFrameSideways()
    {
        // Starts in view, then the next message puts it due north of the camera.
        var service = CreateService(
            (Start, [VesselAt(431234567, bearingDegrees: 90, distanceMeters: 800, Start)]),
            (Start.AddSeconds(1), [VesselAt(431234567, bearingDegrees: 0, distanceMeters: 900, Start.AddSeconds(1))]));
        service.Process(AisDirectory, Camera, Start);

        var frame = service.Process(AisDirectory, Camera, Start.AddSeconds(1));

        Assert.Empty(frame.Visible);
        Assert.Empty(frame.History);
    }
}
