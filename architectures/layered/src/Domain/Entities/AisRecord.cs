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
    // is a "moving vessel only" business rule, not a sentinel check. Course/heading are
    // range-checked (rather than excluding the Python encoder's exact -1/360 sentinels)
    // to avoid floating-point equality comparisons while rejecting the same values, since
    // a valid compass bearing is always in [0, 360).
    public bool IsValid =>
        Mmsi is >= 100_000_000 and <= 999_999_999
        && Longitude is >= 0 and <= 180
        && Latitude is >= 0 and <= 90
        && CourseDegrees is >= 0 and < 360
        && HeadingDegrees is >= 0 and < 360
        && SpeedKnots > 0.3;
}
