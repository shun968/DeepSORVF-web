namespace CleanArchitecture.Domain.Geometry;

// The camera calibration the Python original reads from the per-clip `.txt` file as a
// flat list of 11 floats (utils/file_read.py's read_all), indexed positionally in
// AIS_utils.py. The index each property corresponds to is noted so a loader reading that
// file format stays easy to line up against the original.
public sealed class CameraParameters
{
    public double LongitudeDegrees { get; }        // camera_para[0]  lon_cam
    public double LatitudeDegrees { get; }         // camera_para[1]  lat_cam
    public double BearingDegrees { get; }          // camera_para[2]  shoot_hdir
    public double TiltDegrees { get; }             // camera_para[3]  shoot_vdir (downward)
    public double HeightMeters { get; }            // camera_para[4]  height_cam (above the water)
    public double HorizontalFovDegrees { get; }    // camera_para[5]  FOV_hor
    public double VerticalFovDegrees { get; }      // camera_para[6]  FOV_ver
    public double FocalLengthX { get; }            // camera_para[7]  f_x
    public double FocalLengthY { get; }            // camera_para[8]  f_y
    public double PrincipalPointX { get; }         // camera_para[9]  u0
    public double PrincipalPointY { get; }         // camera_para[10] v0

    public CameraParameters(
        double longitudeDegrees,
        double latitudeDegrees,
        double bearingDegrees,
        double tiltDegrees,
        double heightMeters,
        double horizontalFovDegrees,
        double verticalFovDegrees,
        double focalLengthX,
        double focalLengthY,
        double principalPointX,
        double principalPointY)
    {
        LongitudeDegrees = longitudeDegrees;
        LatitudeDegrees = latitudeDegrees;
        BearingDegrees = bearingDegrees;
        TiltDegrees = tiltDegrees;
        HeightMeters = heightMeters;
        HorizontalFovDegrees = horizontalFovDegrees;
        VerticalFovDegrees = verticalFovDegrees;
        FocalLengthX = focalLengthX;
        FocalLengthY = focalLengthY;
        PrincipalPointX = principalPointX;
        PrincipalPointY = principalPointY;
    }
}
