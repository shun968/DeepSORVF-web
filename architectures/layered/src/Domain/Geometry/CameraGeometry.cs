namespace LayeredArchitecture.Domain.Geometry;

// Ported from utils/AIS_utils.py's data_filter and visual_transform: decides whether an
// AIS position falls in the camera's field of view, and projects it into image pixel
// coordinates.
public sealed class CameraGeometry
{
    // The original widens the horizontal field of view by a fixed 8° before deciding a
    // vessel is out of frame, to absorb AIS positions lagging behind where the vessel
    // actually appears in the video (which would otherwise lose vessels at the edges).
    private const double HorizontalFovMarginDegrees = 8;

    private readonly CameraParameters _parameters;

    public CameraGeometry(CameraParameters parameters)
    {
        _parameters = parameters;
    }

    public double DistanceMeters(double longitudeDegrees, double latitudeDegrees) =>
        GeoMath.DistanceMeters(
            _parameters.LatitudeDegrees,
            _parameters.LongitudeDegrees,
            latitudeDegrees,
            longitudeDegrees);

    public AisVisibility Classify(double longitudeDegrees, double latitudeDegrees)
    {
        var distanceMeters = DistanceMeters(longitudeDegrees, latitudeDegrees);
        var depressionDegrees = ToDegrees(Math.Atan(distanceMeters / _parameters.HeightMeters));
        if (90 + _parameters.TiltDegrees - (_parameters.VerticalFovDegrees / 2) >= depressionDegrees)
        {
            return AisVisibility.OutsideVerticalFov;
        }

        var bearingDegrees = GeoMath.InitialBearingDegrees(
            _parameters.LatitudeDegrees,
            _parameters.LongitudeDegrees,
            latitudeDegrees,
            longitudeDegrees);
        var offsetDegrees = Math.Abs(_parameters.BearingDegrees - bearingDegrees);
        var angleFromCentreDegrees = offsetDegrees < 180 ? offsetDegrees : 360 - offsetDegrees;

        // The original has a third branch returning 'ais_del' beyond FOV_hor/2 + 12, but
        // the preceding if/elif pair already covers every value, so it never runs — and
        // data_coarse_process's check for 'ais_del' is inert for the same reason. Only the
        // two reachable outcomes are implemented here; the behaviour is unchanged.
        return angleFromCentreDegrees <= (_parameters.HorizontalFovDegrees / 2) + HorizontalFovMarginDegrees
            ? AisVisibility.Transform
            : AisVisibility.RemoveVisualTrack;
    }

    public (int X, int Y) Project(double longitudeDegrees, double latitudeDegrees)
    {
        var distanceMeters = DistanceMeters(longitudeDegrees, latitudeDegrees);
        var bearingDegrees = GeoMath.InitialBearingDegrees(
            _parameters.LatitudeDegrees,
            _parameters.LongitudeDegrees,
            latitudeDegrees,
            longitudeDegrees);

        var horizontalAngleDegrees = bearingDegrees - _parameters.BearingDegrees;
        if (horizontalAngleDegrees < -180)
        {
            horizontalAngleDegrees += 360;
        }
        else if (horizontalAngleDegrees > 180)
        {
            horizontalAngleDegrees -= 360;
        }

        var horizontalAngle = ToRadians(horizontalAngleDegrees);
        var tilt = ToRadians(-_parameters.TiltDegrees);

        var worldZ = distanceMeters * Math.Cos(horizontalAngle);
        var worldX = distanceMeters * Math.Sin(horizontalAngle);
        var worldY = _parameters.HeightMeters;

        var cameraZ = (worldZ / Math.Cos(tilt)) + ((worldY - (worldZ * Math.Tan(tilt))) * Math.Sin(tilt));
        var cameraY = (worldY - (worldZ * Math.Tan(tilt))) * Math.Cos(tilt);

        return (
            (int)((_parameters.FocalLengthX * worldX / cameraZ) + _parameters.PrincipalPointX),
            (int)((_parameters.FocalLengthY * cameraY / cameraZ) + _parameters.PrincipalPointY));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
