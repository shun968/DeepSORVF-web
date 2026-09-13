using LayeredArchitecture.Domain.Geometry;

namespace LayeredArchitecture.Domain.Entities;

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

    // Ported from DeepSORVF's utils/AIS_utils.py: data_coarse_process's "1.清洗异常数据"
    // block only (the sub-check that needs no camera_para / previous-second state).
    // Not a general geometric law: lon in [0,180] / lat in [0,90] reflects the original
    // FVessel dataset's fixed Northern/Eastern-hemisphere deployment, and speed > 0.3kt
    // is a "moving vessel only" business rule, not a sentinel check. Course is range-checked
    // to [0, 360), which rejects the original's -1 and 360 sentinels without an exact
    // floating-point comparison. Heading only rejects negatives (the -1 sentinel): the
    // original keeps heading 511, AIS's "not available", and real FVessel data is full of it
    // (1877 of clip-01's 3046 rows), so an upper bound here would discard most valid messages.
    public bool IsValid =>
        Mmsi is >= 100_000_000 and <= 999_999_999
        && Longitude is >= 0 and <= 180
        && Latitude is >= 0 and <= 90
        && CourseDegrees is >= 0 and < 360
        && HeadingDegrees >= 0
        && SpeedKnots > 0.3;

    // Dead reckoning, ported from utils/AIS_utils.py's data_pre: AIS messages arrive
    // irregularly, so a vessel's position is carried forward along its last known course
    // at its last known speed for the seconds no message covers.
    //
    // The original short-circuits speed == 0 to a timestamp-only update; that is left out
    // here because it is the same result (zero speed gives zero distance, and a geodesic
    // of zero length ends where it started) and because data_coarse_process has already
    // dropped anything at or below 0.3kt by this point.
    public AisRecord PredictAt(DateTimeOffset timestampUtc)
    {
        const double MetersPerNauticalMile = 1852;

        var distanceMeters = SpeedKnots * (timestampUtc - Timestamp).TotalHours * MetersPerNauticalMile;
        var (latitude, longitude) = GeoMath.Destination(Latitude, Longitude, CourseDegrees, distanceMeters);

        return new AisRecord(Mmsi, longitude, latitude, SpeedKnots, CourseDegrees, HeadingDegrees, ShipType, timestampUtc);
    }
}
