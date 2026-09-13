using CleanArchitecture.Domain.Geometry;

namespace CleanArchitecture.Domain.Entities;

public sealed class AisRecord
{
    public long Mmsi { get; }
    public double Longitude { get; }
    public double Latitude { get; }
    public double SpeedKnots { get; }
    public double CourseDegrees { get; }
    public double HeadingDegrees { get; }
    public int ShipType { get; }
    public DateTimeOffset Timestamp { get; }

    public AisRecord(
        long mmsi,
        double longitude,
        double latitude,
        double speedKnots,
        double courseDegrees,
        double headingDegrees,
        int shipType,
        DateTimeOffset timestamp)
    {
        Mmsi = mmsi;
        Longitude = longitude;
        Latitude = latitude;
        SpeedKnots = speedKnots;
        CourseDegrees = courseDegrees;
        HeadingDegrees = headingDegrees;
        ShipType = shipType;
        Timestamp = timestamp;
    }

    // Ported from DeepSORVF's utils/AIS_utils.py: data_coarse_process's first block, the
    // part that needs no camera parameters or previous-second state. lon in [0,180] /
    // lat in [0,90] reflects the original dataset's Northern/Eastern-hemisphere deployment,
    // and speed > 0.3kt is a "moving vessel only" rule rather than a sentinel check.
    public bool IsValid =>
        Mmsi is >= 100_000_000 and <= 999_999_999
        && Longitude is >= 0 and <= 180
        && Latitude is >= 0 and <= 90
        && CourseDegrees is >= 0 and < 360
        && HeadingDegrees is >= 0 and < 360
        && SpeedKnots > 0.3;

    // Dead reckoning, ported from data_pre: a vessel is carried forward along its last
    // known course at its last known speed for the seconds no message covers.
    public AisRecord PredictAt(DateTimeOffset timestampUtc)
    {
        const double MetersPerNauticalMile = 1852;

        var distanceMeters = SpeedKnots * (timestampUtc - Timestamp).TotalHours * MetersPerNauticalMile;
        var (latitude, longitude) = GeoMath.Destination(Latitude, Longitude, CourseDegrees, distanceMeters);

        return new AisRecord(Mmsi, longitude, latitude, SpeedKnots, CourseDegrees, HeadingDegrees, ShipType, timestampUtc);
    }
}
