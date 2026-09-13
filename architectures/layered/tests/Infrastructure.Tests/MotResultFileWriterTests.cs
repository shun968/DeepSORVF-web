using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;
using LayeredArchitecture.Infrastructure.Repositories;
using Xunit;

namespace LayeredArchitecture.Infrastructure.Tests;

public class MotResultFileWriterTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("mot-writer-tests-").FullName;
    private readonly MotResultFileWriter _writer = new();

    [Fact]
    public void Write_ProducesTheTenColumnMotFormat()
    {
        _writer.Write(_directory, MotResultKind.Fusion, [new MotResultRow(3, 431234567, 930, 680, 60, 40)]);

        var line = Assert.Single(File.ReadAllLines(Path.Combine(_directory, "fusion.txt")));
        Assert.Equal("3,431234567,930,680,60,40,1,1,1,1", line);
    }

    [Fact]
    public void Write_NamesTheFileAfterTheKind()
    {
        _writer.Write(_directory, MotResultKind.Detection, [new MotResultRow(1, 0, 0, 0, 10, 10)]);
        _writer.Write(_directory, MotResultKind.Tracking, [new MotResultRow(1, 2, 0, 0, 10, 10)]);

        Assert.True(File.Exists(Path.Combine(_directory, "detection.txt")));
        Assert.True(File.Exists(Path.Combine(_directory, "tracking.txt")));
    }

    [Fact]
    public void Write_CreatesTheDirectoryIfItIsMissing()
    {
        var nested = Path.Combine(_directory, "results");

        _writer.Write(nested, MotResultKind.Tracking, []);

        Assert.True(File.Exists(Path.Combine(nested, "tracking.txt")));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
