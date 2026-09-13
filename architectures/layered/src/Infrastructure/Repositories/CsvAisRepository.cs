using System.Globalization;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Infrastructure.Repositories;

// Ported from DeepSORVF's utils/file_read.py + utils/AIS_utils.py file layout: one CSV
// per second, named by the second it covers, holding every AIS message received in that
// second. Columns are found by header name (mmsi,lon,lat,speed,course,heading,type,timestamp,
// with timestamp in unix milliseconds), so the FVessel files — which start with the unnamed
// pandas index column the original skips via usecols=[1..8] — read the same as a plain
// header. A missing file for the requested second returns an empty list rather than
// dead-reckoning a position forward from the previous second.
public sealed class CsvAisRepository : IAisRepository
{
    private const string FileNameFormat = "yyyy_MM_dd_HH_mm_ss";

    private static readonly string[] RequiredColumns =
        ["mmsi", "lon", "lat", "speed", "course", "heading", "type", "timestamp"];

    public IReadOnlyList<AisRecord> GetRecordsAt(string aisDirectoryPath, DateTimeOffset timestampUtc)
    {
        var filePath = Path.Combine(aisDirectoryPath, $"{timestampUtc.ToString(FileNameFormat, CultureInfo.InvariantCulture)}.csv");
        if (!File.Exists(filePath))
        {
            return Array.Empty<AisRecord>();
        }

        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
        {
            return Array.Empty<AisRecord>();
        }

        var columns = ColumnIndexes(lines[0], filePath);
        var records = new List<AisRecord>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            records.Add(ParseRecord(lines[i].Split(','), columns));
        }

        return records;
    }

    private static Dictionary<string, int> ColumnIndexes(string headerLine, string filePath)
    {
        var header = headerLine.Split(',');
        var columns = new Dictionary<string, int>();
        for (var i = 0; i < header.Length; i++)
        {
            columns[header[i].Trim()] = i;
        }

        var missing = RequiredColumns.Where(name => !columns.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            throw new FormatException($"AIS file '{filePath}' is missing column(s): {string.Join(", ", missing)}.");
        }

        return columns;
    }

    private static AisRecord ParseRecord(string[] fields, Dictionary<string, int> columns) =>
        new(
            mmsi: long.Parse(fields[columns["mmsi"]], CultureInfo.InvariantCulture),
            longitude: double.Parse(fields[columns["lon"]], CultureInfo.InvariantCulture),
            latitude: double.Parse(fields[columns["lat"]], CultureInfo.InvariantCulture),
            speedKnots: double.Parse(fields[columns["speed"]], CultureInfo.InvariantCulture),
            courseDegrees: double.Parse(fields[columns["course"]], CultureInfo.InvariantCulture),
            headingDegrees: double.Parse(fields[columns["heading"]], CultureInfo.InvariantCulture),
            shipType: int.Parse(fields[columns["type"]], CultureInfo.InvariantCulture),
            timestamp: DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(fields[columns["timestamp"]], CultureInfo.InvariantCulture)));
}
