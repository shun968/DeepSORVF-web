using System.Globalization;
using LayeredArchitecture.Infrastructure.Repositories;
using Xunit;

namespace LayeredArchitecture.Infrastructure.Tests;

public class CsvAisRepositoryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ais-repo-tests-").FullName;
    private readonly CsvAisRepository _repository = new();

    [Fact]
    public void GetRecordsAt_WithExistingFile_ParsesAllRows()
    {
        var timestamp = new DateTimeOffset(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
        WriteCsv(timestamp, [
            "431234567,121.5,29.87,5.2,45,47,30,1609502400000",
            "431234568,121.6,29.90,6.1,90,92,60,1609502400500",
        ]);

        var records = _repository.GetRecordsAt(_directory, timestamp);

        Assert.Equal(2, records.Count);
        Assert.Equal(431234567, records[0].Mmsi);
        Assert.Equal(121.5, records[0].Longitude);
        Assert.Equal(29.87, records[0].Latitude);
        Assert.Equal(5.2, records[0].SpeedKnots);
        Assert.Equal(45, records[0].CourseDegrees);
        Assert.Equal(47, records[0].HeadingDegrees);
        Assert.Equal(30, records[0].ShipType);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1609502400000), records[0].Timestamp);
    }

    [Fact]
    public void GetRecordsAt_WithNoFileForThatSecond_ReturnsEmpty()
    {
        var records = _repository.GetRecordsAt(_directory, new DateTimeOffset(2021, 1, 1, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(records);
    }

    [Fact]
    public void GetRecordsAt_IgnoresSubSecondComponentWhenSelectingFile()
    {
        var wholeSecond = new DateTimeOffset(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
        WriteCsv(wholeSecond, ["431234567,121.5,29.87,5.2,45,47,30,1609502400000"]);

        var records = _repository.GetRecordsAt(_directory, wholeSecond.AddMilliseconds(750));

        Assert.Single(records);
    }

    private void WriteCsv(DateTimeOffset timestamp, string[] rows)
    {
        var fileName = timestamp.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + ".csv";
        var lines = new List<string> { "mmsi,lon,lat,speed,course,heading,type,timestamp" };
        lines.AddRange(rows);
        File.WriteAllLines(Path.Combine(_directory, fileName), lines);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
