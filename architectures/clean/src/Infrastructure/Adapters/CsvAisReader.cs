using System.Globalization;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Adapters;

// Reads the per-second CSV layout utils/file_read.py expects: one file per second named
// after it, holding mmsi,lon,lat,speed,course,heading,type,timestamp (unix milliseconds).
// Columns are found by header name, so the FVessel files — which start with the unnamed
// pandas index column the original skips via usecols=[1..8] — read the same as a plain
// header. A second with no file simply has no messages.
public sealed class CsvAisReader : IAisReader
{
    private const string FileNameFormat = "yyyy_MM_dd_HH_mm_ss";

    private static readonly string[] RequiredColumns =
        ["mmsi", "lon", "lat", "speed", "course", "heading", "type", "timestamp"];

    public IReadOnlyList<AisRecord> ReadAt(string aisDirectoryPath, DateTimeOffset timestampUtc)
    {
        var fileName = timestampUtc.ToString(FileNameFormat, CultureInfo.InvariantCulture);
        var filePath = Path.Combine(aisDirectoryPath, $"{fileName}.csv");
        if (!File.Exists(filePath))
        {
            return [];
        }

        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
        {
            return [];
        }

        var columns = ColumnIndexes(lines[0], filePath);
        var records = new List<AisRecord>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                records.Add(Parse(lines[i].Split(','), columns));
            }
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

    private static AisRecord Parse(string[] fields, Dictionary<string, int> columns) =>
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
