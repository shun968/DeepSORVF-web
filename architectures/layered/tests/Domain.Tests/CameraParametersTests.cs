using LayeredArchitecture.Domain.Geometry;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class CameraParametersTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var parameters = new CameraParameters(
            longitudeDegrees: 121.5,
            latitudeDegrees: 29.87,
            bearingDegrees: 90,
            tiltDegrees: 5,
            heightMeters: 20,
            horizontalFovDegrees: 55,
            verticalFovDegrees: 35,
            focalLengthX: 1500,
            focalLengthY: 1501,
            principalPointX: 960,
            principalPointY: 540);

        Assert.Equal(121.5, parameters.LongitudeDegrees);
        Assert.Equal(29.87, parameters.LatitudeDegrees);
        Assert.Equal(90, parameters.BearingDegrees);
        Assert.Equal(5, parameters.TiltDegrees);
        Assert.Equal(20, parameters.HeightMeters);
        Assert.Equal(55, parameters.HorizontalFovDegrees);
        Assert.Equal(35, parameters.VerticalFovDegrees);
        Assert.Equal(1500, parameters.FocalLengthX);
        Assert.Equal(1501, parameters.FocalLengthY);
        Assert.Equal(960, parameters.PrincipalPointX);
        Assert.Equal(540, parameters.PrincipalPointY);
    }
}
