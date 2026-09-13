using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Geometry;

namespace CleanArchitecture.Domain.Services;

// The AIS rules from utils/AIS_utils.py's AISPRO, with the file reading left outside:
// drop implausible messages, carry vessels forward by dead reckoning for the seconds no
// message covers, and keep whatever the camera can see.
//
// Stateful, because deciding whether a message is plausible and keeping a vessel alive both
// need the previous second. One instance handles one run, in chronological order.
public sealed class AisSightingService
{
    // AISPRO.max_dis: vessels further than two nautical miles from the camera are dropped.
    private const double MaxDistanceMeters = 2 * 1852;

    // data_coarse_process's second block: a vessel cannot really move this far in a second.
    private const double MaxJumpDegrees = 1;
    private const double MaxSpeedChangeKnots = 7;

    private IReadOnlyList<AisRecord> _previousSecond = [];

    public IReadOnlyList<VisibleVessel> Assemble(
        IReadOnlyList<AisRecord> received,
        CameraGeometry camera,
        DateTimeOffset timestampUtc)
    {
        var plausible = Plausible(received, camera);
        var current = CarryForward(plausible, timestampUtc);
        _previousSecond = current;

        return Project(current, camera);
    }

    private List<AisRecord> Plausible(IReadOnlyList<AisRecord> received, CameraGeometry camera)
    {
        var previousByMmsi = new Dictionary<long, AisRecord>();
        foreach (var record in _previousSecond)
        {
            // A second can carry several messages for one vessel; the original compares
            // against the last of them.
            previousByMmsi[record.Mmsi] = record;
        }

        return received
            .Where(record => record.IsValid)
            .Where(record => !HasJumped(record, previousByMmsi))
            .Where(record => camera.DistanceMeters(record.Longitude, record.Latitude) <= MaxDistanceMeters)
            .ToList();
    }

    private List<AisRecord> CarryForward(List<AisRecord> received, DateTimeOffset timestampUtc)
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

        foreach (var previous in _previousSecond)
        {
            if (!seen.Contains(previous.Mmsi))
            {
                current.Add(previous.PredictAt(timestampUtc));
            }
        }

        return current;
    }

    private static List<VisibleVessel> Project(List<AisRecord> records, CameraGeometry camera)
    {
        var visible = new List<VisibleVessel>(records.Count);

        foreach (var record in records)
        {
            if (camera.Classify(record.Longitude, record.Latitude) != AisVisibility.Transform)
            {
                continue;
            }

            var (x, y) = camera.Project(record.Longitude, record.Latitude);
            visible.Add(new VisibleVessel(record, x, y));
        }

        return visible;
    }

    private static bool HasJumped(AisRecord record, Dictionary<long, AisRecord> previousByMmsi) =>
        previousByMmsi.TryGetValue(record.Mmsi, out var previous)
        && (Math.Abs(record.Longitude - previous.Longitude) >= MaxJumpDegrees
            || Math.Abs(record.Latitude - previous.Latitude) >= MaxJumpDegrees
            || Math.Abs(record.SpeedKnots - previous.SpeedKnots) >= MaxSpeedChangeKnots);
}
