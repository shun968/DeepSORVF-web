using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Geometry;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Application.Services;

// Ported from utils/AIS_utils.py's AISPRO: reads the AIS messages covering one second,
// drops implausible ones, carries vessels forward by dead reckoning for the seconds no
// message covers, and projects whatever the camera can see into pixel coordinates.
//
// Like AISPRO this is stateful — it remembers the previous second's vessels, both to spot
// implausible jumps and to keep a vessel alive while its messages are missing. It is
// registered per request to match the original's per-run AISPRO instance, so one instance
// must process a run's frames in chronological order.
//
// Not ported yet: AIS_vis, the two-minute history of projected positions. Only the
// trajectory matching this port has still to implement (FUSPRO's DTW) reads it, so keeping
// it here now would be state nothing consumes.
public sealed class AisService
{
    // AISPRO.max_dis: AIS positions further than two nautical miles from the camera are dropped.
    private const double MaxDistanceMeters = 2 * 1852;

    // data_coarse_process's second block: a vessel that appears to have jumped this far in
    // one second is a decoding artefact, not a vessel.
    private const double MaxJumpDegrees = 1;
    private const double MaxSpeedChangeKnots = 7;

    private readonly IAisRepository _aisRepository;
    private IReadOnlyList<AisRecord> _previousSecond = [];

    public AisService(IAisRepository aisRepository)
    {
        _aisRepository = aisRepository;
    }

    public IReadOnlyList<ProjectedAisRecord> Process(
        string aisDirectoryPath,
        CameraGeometry camera,
        DateTimeOffset timestampUtc)
    {
        var received = ReadPlausibleRecords(aisDirectoryPath, camera, timestampUtc);
        var current = DeadReckonToCurrentSecond(received, timestampUtc);
        _previousSecond = current;

        return Project(current, camera);
    }

    private List<AisRecord> ReadPlausibleRecords(
        string aisDirectoryPath,
        CameraGeometry camera,
        DateTimeOffset timestampUtc)
    {
        var previousByMmsi = LastRecordPerMmsi(_previousSecond);

        return _aisRepository.GetRecordsAt(aisDirectoryPath, timestampUtc)
            .Where(record => record.IsValid)
            .Where(record => !HasImplausibleJump(record, previousByMmsi))
            .Where(record => camera.DistanceMeters(record.Longitude, record.Latitude) <= MaxDistanceMeters)
            .ToList();
    }

    private List<AisRecord> DeadReckonToCurrentSecond(List<AisRecord> received, DateTimeOffset timestampUtc)
    {
        var current = new List<AisRecord>(received.Count);
        var seen = new HashSet<long>();

        foreach (var record in received)
        {
            current.Add(record.Timestamp.ToUnixTimeSeconds() == timestampUtc.ToUnixTimeSeconds()
                ? record
                : record.PredictAt(timestampUtc));
            seen.Add(record.Mmsi);
        }

        foreach (var previous in _previousSecond.Where(previous => !seen.Contains(previous.Mmsi)))
        {
            current.Add(previous.PredictAt(timestampUtc));
        }

        return current;
    }

    private static List<ProjectedAisRecord> Project(List<AisRecord> records, CameraGeometry camera)
    {
        var projected = new List<ProjectedAisRecord>(records.Count);

        foreach (var record in records.Where(record =>
            camera.Classify(record.Longitude, record.Latitude) == AisVisibility.Transform))
        {
            var (x, y) = camera.Project(record.Longitude, record.Latitude);
            projected.Add(new ProjectedAisRecord(record, x, y));
        }

        return projected;
    }

    // A second's worth of messages can hold several for the same vessel; the original
    // compares against the last of them (`temp['lon'].values[-1]`).
    private static Dictionary<long, AisRecord> LastRecordPerMmsi(IReadOnlyList<AisRecord> records)
    {
        var byMmsi = new Dictionary<long, AisRecord>();
        foreach (var record in records)
        {
            byMmsi[record.Mmsi] = record;
        }

        return byMmsi;
    }

    private static bool HasImplausibleJump(AisRecord record, Dictionary<long, AisRecord> previousByMmsi) =>
        previousByMmsi.TryGetValue(record.Mmsi, out var previous)
        && (Math.Abs(record.Longitude - previous.Longitude) >= MaxJumpDegrees
            || Math.Abs(record.Latitude - previous.Latitude) >= MaxJumpDegrees
            || Math.Abs(record.SpeedKnots - previous.SpeedKnots) >= MaxSpeedChangeKnots);
}
