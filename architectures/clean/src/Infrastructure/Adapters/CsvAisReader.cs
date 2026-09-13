using System.Globalization;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Adapters;

// Reads the per-second CSV layout utils/file_read.py expects: one file per second named
// after it, holding mmsi,lon,lat,speed,course,heading,type,timestamp (unix milliseconds).
// A second with no file simply has no messages.
public sealed class CsvAisReader : IAisReader
{
    private const string FileNameFormat = "yyyy_MM_dd_HH_mm_ss";

    public IReadOnlyList<AisRecord> ReadAt(string aisDirectoryPath, DateTimeOffset timestampUtc)
    {
        var fileName = timestampUtc.ToString(FileNameFormat, CultureInfo.InvariantCulture);
        var filePath = Path.Combine(aisDirectoryPath, $"{fileName}.csv");
        if (!File.Exists(filePath))
        {
            return [];
        }

        var lines = File.ReadAllLines(filePath);
        var records = new List<AisRecord>(Math.Max(0, lines.Length - 1));
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                records.Add(Parse(lines[i]));
            }
        }

        return records;
    }

    private static AisRecord Parse(string line)
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
