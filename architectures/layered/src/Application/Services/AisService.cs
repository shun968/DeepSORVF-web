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
public sealed class AisService
{
    // AISPRO.max_dis: AIS positions further than two nautical miles from the camera are dropped.
    private const double MaxDistanceMeters = 2 * 1852;

    // data_coarse_process's second block: a vessel that appears to have jumped this far in
    // one second is a decoding artefact, not a vessel.
    private const double MaxJumpDegrees = 1;
    private const double MaxSpeedChangeKnots = 7;

    // AISPRO.time_lim: projected positions are kept for two minutes so trajectory matching
    // has something to compare.
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(2);

    private readonly IAisRepository _aisRepository;
    private readonly List<ProjectedAisRecord> _history = [];
    private IReadOnlyList<AisRecord> _previousSecond = [];

    public AisService(IAisRepository aisRepository)
    {
        _aisRepository = aisRepository;
    }

    public AisFrame Process(
        string aisDirectoryPath,
        CameraGeometry camera,
        DateTimeOffset timestampUtc)
    {
        var received = ReadPlausibleRecords(aisDirectoryPath, camera, timestampUtc);
        var current = DeadReckonToCurrentSecond(received, timestampUtc);
        _previousSecond = current;

        var visible = Project(current, camera);
        _history.AddRange(visible);
        _history.RemoveAll(entry => entry.Record.Timestamp < timestampUtc - HistoryWindow);

        return new AisFrame(visible, _history.ToList());
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

        current.AddRange(_previousSecond
            .Where(previous => !seen.Contains(previous.Mmsi))
            .Select(previous => previous.PredictAt(timestampUtc)));

        return current;
    }

    private List<ProjectedAisRecord> Project(List<AisRecord> records, CameraGeometry camera)
    {
        var projected = new List<ProjectedAisRecord>(records.Count);

        foreach (var record in records)
        {
            switch (camera.Classify(record.Longitude, record.Latitude))
            {
                case AisVisibility.Transform:
                    var (x, y) = camera.Project(record.Longitude, record.Latitude);
                    projected.Add(new ProjectedAisRecord(record, x, y));
                    break;

                // A vessel that has left the frame sideways takes its trajectory with it,
                // so a track appearing at the edge later cannot match its stale history.
                case AisVisibility.RemoveVisualTrack:
                    _history.RemoveAll(entry => entry.Record.Mmsi == record.Mmsi);
                    break;

                default:
                    break;
            }
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
