using CleanArchitecture.Domain.Geometry;
using Xunit;

namespace CleanArchitecture.Domain.Tests;

public class GeoMathTests
{
    [Fact]
    public void DistanceMeters_BetweenIdenticalPoints_IsZero()
    {
        Assert.Equal(0, GeoMath.DistanceMeters(29.87, 121.5, 29.87, 121.5));
    }

    [Fact]
    public void DistanceMeters_AlongTheEquator_MatchesTheEquatorialRadius()
    {
        // One degree of longitude on the equator is a * π/180 on WGS84. This is also the
        // case where cos²α collapses to zero, which the iteration has to special-case.
        Assert.Equal(111319.4908, GeoMath.DistanceMeters(0, 0, 0, 1), 3);
    }

    [Fact]
    public void DistanceMeters_MatchesThePublishedFlindersPeakTestLine()
    {
        // The standard geodetic test line, Flinders Peak (-37°57'03.72030",
        // 144°25'29.52440") to Buninyong (-37°39'10.15610", 143°55'35.38390"), published
        // as 54972.271m.
        var distance = GeoMath.DistanceMeters(
            -37.951033416667,
            144.424867888889,
            -37.652821138889,
            143.926495527778);

        Assert.Equal(54972.271, distance, 3);
    }

    [Fact]
    public void DistanceMeters_IsSymmetric()
    {
        var forward = GeoMath.DistanceMeters(29.87, 121.5, 29.88, 121.52);
        var backward = GeoMath.DistanceMeters(29.88, 121.52, 29.87, 121.5);

        Assert.Equal(forward, backward, 6);
    }

    [Theory]
    [InlineData(0, 0, 0, 1, 90)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 0, -1, 0, 180)]
    [InlineData(0, 0, 0, -1, 270)]
    public void InitialBearingDegrees_ReturnsCompassBearing(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2,
        double expected)
    {
        Assert.Equal(expected, GeoMath.InitialBearingDegrees(latitude1, longitude1, latitude2, longitude2), 6);
    }

    [Fact]
    public void Destination_TravellingEastAlongTheEquator_AdvancesLongitudeOnly()
    {
        var (latitude, longitude) = GeoMath.Destination(0, 0, 90, 111319.4908);

        Assert.Equal(0, latitude, 9);
        Assert.Equal(1, longitude, 6);
    }

    [Fact]
    public void Destination_TravellingNorth_AdvancesLatitudeOnly()
    {
        var (latitude, longitude) = GeoMath.Destination(29.87, 121.5, 0, 1000);

        Assert.True(latitude > 29.87);
        Assert.Equal(121.5, longitude, 9);
    }

    [Fact]
    public void Destination_IsTheInverseOfDistance()
    {
        var (latitude, longitude) = GeoMath.Destination(29.87, 121.5, 47, 3704);

        Assert.Equal(3704, GeoMath.DistanceMeters(29.87, 121.5, latitude, longitude), 3);
    }

    [Fact]
    public void Destination_WithZeroDistance_ReturnsTheStartingPoint()
    {
        var (latitude, longitude) = GeoMath.Destination(29.87, 121.5, 47, 0);

        Assert.Equal(29.87, latitude, 9);
        Assert.Equal(121.5, longitude, 9);
    }

    [Fact]
    public void Destination_CrossingTheAntimeridian_WrapsLongitudeIntoRange()
    {
        var (_, longitude) = GeoMath.Destination(0, 179.99, 90, 5000);

        Assert.True(longitude < 0, $"expected a wrapped negative longitude but got {longitude}");
        Assert.True(longitude > -180, $"expected a longitude above -180 but got {longitude}");
    }
}
