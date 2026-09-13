using System.Globalization;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Infrastructure.Repositories;

// Ported from DeepSORVF's utils/file_read.py + utils/AIS_utils.py file layout: one CSV
// per second, named by the second it covers, holding every AIS message received in that
// second. Header: mmsi,lon,lat,speed,course,heading,type,timestamp (timestamp is unix
// milliseconds, matching the original ais['timestamp'] column before Python's own
// round(.../1000) conversion). Unlike the Python original, this does not carry a leading
// pandas index column, and a missing file for the requested second returns an empty list
// rather than dead-reckoning a position forward from the previous second.
public sealed class CsvAisRepository : IAisRepository
{
    private const string FileNameFormat = "yyyy_MM_dd_HH_mm_ss";

    public IReadOnlyList<AisRecord> GetRecordsAt(string aisDirectoryPath, DateTimeOffset timestampUtc)
    {
        var filePath = Path.Combine(aisDirectoryPath, $"{timestampUtc.ToString(FileNameFormat, CultureInfo.InvariantCulture)}.csv");
        if (!File.Exists(filePath))
        {
            return Array.Empty<AisRecord>();
        }

        var lines = File.ReadAllLines(filePath);
        var records = new List<AisRecord>(Math.Max(0, lines.Length - 1));
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            records.Add(ParseRecord(lines[i]));
        }

        return records;
    }

    private static AisRecord ParseRecord(string line)
    {
        var fields = line.Split(',');
        return new AisRecord(
            mmsi: long.Parse(fields[0], CultureInfo.InvariantCulture),
            longitude: double.Parse(fields[1], CultureInfo.InvariantCulture),
            latitude: double.Parse(fields[2], CultureInfo.InvariantCulture),
            speedKnots: double.Parse(fields[3], CultureInfo.InvariantCulture),
            courseDegrees: double.Parse(fields[4], CultureInfo.InvariantCulture),
            headingDegrees: double.Parse(fields[5], CultureInfo.InvariantCulture),
            shipType: int.Parse(fields[6], CultureInfo.InvariantCulture),
            timestamp: DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(fields[7], CultureInfo.InvariantCulture)));
    }
}
