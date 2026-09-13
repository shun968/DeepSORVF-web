using System.Globalization;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Infrastructure.Adapters;
using Xunit;

namespace CleanArchitecture.Infrastructure.Tests;

public class AdapterTests : IDisposable
{
    private static readonly DateTimeOffset Timestamp = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Directory.CreateTempSubdirectory("clean-adapters-").FullName;

    [Fact]
    public void CsvAisReader_ParsesEveryRowOfTheSecondsFile()
    {
        WriteAisCsv(Timestamp, ["431234567,121.5,29.87,5.2,45,47,30,1609502400000"]);

        var records = new CsvAisReader().ReadAt(_directory, Timestamp);

        var record = Assert.Single(records);
        Assert.Equal(431234567, record.Mmsi);
        Assert.Equal(121.5, record.Longitude);
        Assert.Equal(29.87, record.Latitude);
        Assert.Equal(5.2, record.SpeedKnots);
        Assert.Equal(45, record.CourseDegrees);
        Assert.Equal(47, record.HeadingDegrees);
        Assert.Equal(30, record.ShipType);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1609502400000), record.Timestamp);
    }

    [Fact]
    public void CsvAisReader_WithNoFileForThatSecond_ReturnsEmpty()
    {
        Assert.Empty(new CsvAisReader().ReadAt(_directory, Timestamp));
    }

    [Fact]
    public void CsvAisReader_IgnoresSubSecondComponentWhenChoosingTheFile()
    {
        WriteAisCsv(Timestamp, ["431234567,121.5,29.87,5.2,45,47,30,1609502400000"]);

        Assert.Single(new CsvAisReader().ReadAt(_directory, Timestamp.AddMilliseconds(750)));
    }

    [Fact]
    public void TextFileCameraParametersReader_ReadsTheElevenValuesInOrder()
    {
        var path = WriteCameraFile("[121.5,29.87,90,5,20,55,35,1500,1501,960,540]\n");

        var parameters = new TextFileCameraParametersReader().Read(path);

        Assert.Equal(121.5, parameters.LongitudeDegrees);
        Assert.Equal(29.87, parameters.LatitudeDegrees);
        Assert.Equal(90, parameters.BearingDegrees);
        Assert.Equal(5, parameters.TiltDegrees);
        Assert.Equal(20, parameters.HeightMeters);
        Assert.Equal(55, parameters.HorizontalFovDegrees);
        Assert.Equal(35, parameters.VerticalFovDegrees);
        Assert.Equal(1500, parameters.FocalLengthX);
        Assert.Equal(1501, parameters.FocalLengthY);
        Assert.Equal(960, parameters.PrincipalPointX);
        Assert.Equal(540, parameters.PrincipalPointY);
    }

    [Fact]
    public void TextFileCameraParametersReader_WithoutBracketsOrTrailingNewline_ReadsTheSameValues()
    {
        var path = WriteCameraFile("121.5,29.87,90,5,20,55,35,1500,1501,960,540");

        Assert.Equal(540, new TextFileCameraParametersReader().Read(path).PrincipalPointY);
    }

    [Fact]
    public void TextFileCameraParametersReader_WithTheWrongNumberOfValues_Throws()
    {
        var path = WriteCameraFile("[121.5,29.87,90]\n");

        Assert.Throws<FormatException>(() => new TextFileCameraParametersReader().Read(path));
    }

    private void WriteAisCsv(DateTimeOffset timestamp, string[] rows)
    {
        var fileName = timestamp.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + ".csv";
        var lines = new List<string> { "mmsi,lon,lat,speed,course,heading,type,timestamp" };
        lines.AddRange(rows);
        File.WriteAllLines(Path.Combine(_directory, fileName), lines);
    }

    private string WriteCameraFile(string contents)
    {
        var path = Path.Combine(_directory, "camera.txt");
        File.WriteAllText(path, contents);

        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
