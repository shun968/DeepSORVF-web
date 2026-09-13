using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Geometry;
using Xunit;

namespace CleanArchitecture.Domain.Tests;

public class AisRecordTests
{
    private static AisRecord CreateValid(
        long mmsi = 431234567,
        double longitude = 121.5,
        double latitude = 29.87,
        double speedKnots = 5.2,
        double courseDegrees = 45,
        double headingDegrees = 47,
        int shipType = 30,
        long timestampUnixMs = 1_600_000_000_000) =>
        new(mmsi, longitude, latitude, speedKnots, courseDegrees, headingDegrees, shipType,
            DateTimeOffset.FromUnixTimeMilliseconds(timestampUnixMs));

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_600_000_000_000);

        var record = new AisRecord(431234567, 121.5, 29.87, 5.2, 45, 47, 30, timestamp);

        Assert.Equal(431234567, record.Mmsi);
        Assert.Equal(121.5, record.Longitude);
        Assert.Equal(29.87, record.Latitude);
        Assert.Equal(5.2, record.SpeedKnots);
        Assert.Equal(45, record.CourseDegrees);
        Assert.Equal(47, record.HeadingDegrees);
        Assert.Equal(30, record.ShipType);
        Assert.Equal(timestamp, record.Timestamp);
    }

    [Fact]
    public void IsValid_WithAllFieldsInRange_ReturnsTrue()
    {
        Assert.True(CreateValid().IsValid);
    }

    [Fact]
    public void IsValid_WithMmsiTooShort_ReturnsFalse()
    {
        Assert.False(CreateValid(mmsi: 99_999_999).IsValid);
    }

    [Fact]
    public void IsValid_WithMmsiTooLong_ReturnsFalse()
    {
        Assert.False(CreateValid(mmsi: 1_000_000_000).IsValid);
    }

    [Fact]
    public void IsValid_WithLongitudeBelowZero_ReturnsFalse()
    {
        Assert.False(CreateValid(longitude: -1).IsValid);
    }

    [Fact]
    public void IsValid_WithLongitudeAbove180_ReturnsFalse()
    {
        Assert.False(CreateValid(longitude: 181).IsValid);
    }

    [Fact]
    public void IsValid_WithLatitudeBelowZero_ReturnsFalse()
    {
        Assert.False(CreateValid(latitude: -1).IsValid);
    }

    [Fact]
    public void IsValid_WithLatitudeAbove90_ReturnsFalse()
    {
        Assert.False(CreateValid(latitude: 91).IsValid);
    }

    [Fact]
    public void IsValid_WithCourseBelowZero_ReturnsFalse()
    {
        Assert.False(CreateValid(courseDegrees: -1).IsValid);
    }

    [Fact]
    public void IsValid_WithCourseAtOrAbove360_ReturnsFalse()
    {
        Assert.False(CreateValid(courseDegrees: 360).IsValid);
    }

    [Fact]
    public void IsValid_WithHeadingBelowZero_ReturnsFalse()
    {
        Assert.False(CreateValid(headingDegrees: -1).IsValid);
    }

    [Fact]
    public void IsValid_WithHeadingAtOrAbove360_ReturnsFalse()
    {
        Assert.False(CreateValid(headingDegrees: 360).IsValid);
    }

    [Fact]
    public void IsValid_WithSpeedAtOrBelowThreshold_ReturnsFalse()
    {
        Assert.False(CreateValid(speedKnots: 0.3).IsValid);
    }

    [Fact]
    public void PredictAt_MovesTheVesselAlongItsCourseAtItsSpeed()
    {
        var record = CreateValid(speedKnots: 10, courseDegrees: 90);

        var predicted = record.PredictAt(record.Timestamp.AddHours(1));

        // 10 knots for an hour is 10 nautical miles.
        var travelled = GeoMath.DistanceMeters(record.Latitude, record.Longitude, predicted.Latitude, predicted.Longitude);
        Assert.Equal(10 * 1852, travelled, 1);
        Assert.Equal(90, GeoMath.InitialBearingDegrees(record.Latitude, record.Longitude, predicted.Latitude, predicted.Longitude), 3);
    }

    [Fact]
    public void PredictAt_CarriesTheRemainingFieldsAndTheNewTimestamp()
    {
        var record = CreateValid();
        var target = record.Timestamp.AddSeconds(30);

        var predicted = record.PredictAt(target);

        Assert.Equal(target, predicted.Timestamp);
        Assert.Equal(record.Mmsi, predicted.Mmsi);
        Assert.Equal(record.SpeedKnots, predicted.SpeedKnots);
        Assert.Equal(record.CourseDegrees, predicted.CourseDegrees);
        Assert.Equal(record.HeadingDegrees, predicted.HeadingDegrees);
        Assert.Equal(record.ShipType, predicted.ShipType);
    }

    [Fact]
    public void PredictAt_WithNoElapsedTime_LeavesThePositionUnchanged()
    {
        var record = CreateValid();

        var predicted = record.PredictAt(record.Timestamp);

        Assert.Equal(record.Latitude, predicted.Latitude, 9);
        Assert.Equal(record.Longitude, predicted.Longitude, 9);
    }
}
