using LayeredArchitecture.Domain.Geometry;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class CameraGeometryTests
{
    private const double CameraLongitude = 121.5;
    private const double CameraLatitude = 29.87;
    private const double PrincipalPointX = 960;
    private const double PrincipalPointY = 540;

    // Looking due east, tilted 5° down, 20m above the water, 55°x35° field of view on a
    // 1920x1080 sensor. With that tilt and field of view, anything closer than roughly 90m
    // falls below the bottom of the frame.
    private static CameraGeometry CreateCamera(double bearingDegrees = 90) =>
        new(new CameraParameters(
            longitudeDegrees: CameraLongitude,
            latitudeDegrees: CameraLatitude,
            bearingDegrees: bearingDegrees,
            tiltDegrees: 5,
            heightMeters: 20,
            horizontalFovDegrees: 55,
            verticalFovDegrees: 35,
            focalLengthX: 1500,
            focalLengthY: 1500,
            principalPointX: PrincipalPointX,
            principalPointY: PrincipalPointY));

    private static (double Latitude, double Longitude) VesselAt(double bearingDegrees, double distanceMeters) =>
        GeoMath.Destination(CameraLatitude, CameraLongitude, bearingDegrees, distanceMeters);

    [Fact]
    public void Classify_WithVesselAheadAndInRange_ReturnsTransform()
    {
        var (latitude, longitude) = VesselAt(bearingDegrees: 90, distanceMeters: 1000);

        Assert.Equal(AisVisibility.Transform, CreateCamera().Classify(longitude, latitude));
    }

    [Fact]
    public void Classify_WithVesselTooCloseToBeInFrame_ReturnsOutsideVerticalFov()
    {
        var (latitude, longitude) = VesselAt(bearingDegrees: 90, distanceMeters: 50);

        Assert.Equal(AisVisibility.OutsideVerticalFov, CreateCamera().Classify(longitude, latitude));
    }

    [Fact]
    public void Classify_WithVesselBeyondTheWidenedHorizontalFov_ReturnsRemoveVisualTrack()
    {
        var (latitude, longitude) = VesselAt(bearingDegrees: 0, distanceMeters: 1000);

        Assert.Equal(AisVisibility.RemoveVisualTrack, CreateCamera().Classify(longitude, latitude));
    }

    [Fact]
    public void Classify_WithBearingsEitherSideOfNorth_MeasuresTheShorterAngle()
    {
        // 350° and 10° are 20° apart, not 340° — without the wrap-around the vessel would
        // be judged far outside the field of view.
        var (latitude, longitude) = VesselAt(bearingDegrees: 10, distanceMeters: 1000);

        Assert.Equal(AisVisibility.Transform, CreateCamera(bearingDegrees: 350).Classify(longitude, latitude));
    }

    [Fact]
    public void Classify_JustInsideAndJustOutsideTheMargin_SwitchesOutcome()
    {
        var camera = CreateCamera();
        var (insideLatitude, insideLongitude) = VesselAt(bearingDegrees: 90 + 35, distanceMeters: 1000);
        var (outsideLatitude, outsideLongitude) = VesselAt(bearingDegrees: 90 + 36, distanceMeters: 1000);

        Assert.Equal(AisVisibility.Transform, camera.Classify(insideLongitude, insideLatitude));
        Assert.Equal(AisVisibility.RemoveVisualTrack, camera.Classify(outsideLongitude, outsideLatitude));
    }

    [Fact]
    public void Project_WithVesselDeadAhead_LandsOnThePrincipalPointColumn()
    {
        var (latitude, longitude) = VesselAt(bearingDegrees: 90, distanceMeters: 1000);

        var (x, y) = CreateCamera().Project(longitude, latitude);

        Assert.InRange(x, PrincipalPointX - 2, PrincipalPointX + 2);
        Assert.True(y > PrincipalPointY, $"a vessel below the horizon should project below the principal point, got {y}");
    }

    [Fact]
    public void Project_MovesRightAsTheVesselMovesClockwiseOfTheCamera()
    {
        var camera = CreateCamera();
        var (leftLatitude, leftLongitude) = VesselAt(bearingDegrees: 80, distanceMeters: 1000);
        var (rightLatitude, rightLongitude) = VesselAt(bearingDegrees: 100, distanceMeters: 1000);

        var (leftX, _) = camera.Project(leftLongitude, leftLatitude);
        var (rightX, _) = camera.Project(rightLongitude, rightLatitude);

        Assert.True(leftX < PrincipalPointX, $"expected the vessel to the left of centre to project left, got {leftX}");
        Assert.True(rightX > PrincipalPointX, $"expected the vessel to the right of centre to project right, got {rightX}");
    }

    [Fact]
    public void Project_MovesTowardsTheHorizonAsTheVesselGetsFurtherAway()
    {
        var camera = CreateCamera();
        var (nearLatitude, nearLongitude) = VesselAt(bearingDegrees: 90, distanceMeters: 1000);
        var (farLatitude, farLongitude) = VesselAt(bearingDegrees: 90, distanceMeters: 2000);

        var (_, nearY) = camera.Project(nearLongitude, nearLatitude);
        var (_, farY) = camera.Project(farLongitude, farLatitude);

        Assert.True(farY < nearY, $"the further vessel should sit closer to the horizon, got {farY} and {nearY}");
    }

    [Fact]
    public void Project_WithBearingWrappingBelowMinus180_NormalisesTheAngle()
    {
        var (latitude, longitude) = VesselAt(bearingDegrees: 10, distanceMeters: 1000);

        var (x, _) = CreateCamera(bearingDegrees: 350).Project(longitude, latitude);

        Assert.True(x > PrincipalPointX, $"20° clockwise of the camera should project right of centre, got {x}");
    }

    [Fact]
    public void Project_WithBearingWrappingAbove180_NormalisesTheAngle()
    {
        var (latitude, longitude) = VesselAt(bearingDegrees: 350, distanceMeters: 1000);

        var (x, _) = CreateCamera(bearingDegrees: 10).Project(longitude, latitude);

        Assert.True(x < PrincipalPointX, $"20° anticlockwise of the camera should project left of centre, got {x}");
    }
}
