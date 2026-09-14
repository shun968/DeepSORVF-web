using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Infrastructure.Repositories;
using OpenCvSharp;
using Xunit;

namespace LayeredArchitecture.Infrastructure.Tests;

public class OpenCvVideoFrameRepositoryTests : IDisposable
{
    private const int FramesPerSecond = 10;
    private const int FrameCount = 50;
    private readonly string _directory = Directory.CreateTempSubdirectory("layered-video-").FullName;
    private readonly string _videoPath;

    public OpenCvVideoFrameRepositoryTests()
    {
        _videoPath = WriteVideo("frames.avi");
    }

    // Five seconds at 10fps, with frame n filled with red level 5n so a frame read back says
    // which one it is.
    private string WriteVideo(string name)
    {
        var path = Path.Combine(_directory, name);
        using var writer = new VideoWriter(path, FourCC.MJPG, FramesPerSecond, new OpenCvSharp.Size(32, 24));
        for (var index = 0; index < FrameCount; index++)
        {
            using var frame = new Mat(24, 32, MatType.CV_8UC3, new Scalar(0, 0, 5 * index));
            writer.Write(frame);
        }

        return path;
    }

    private static int FrameNumber(FrameImage? image) => (int)Math.Round(image!.Rgb.Span[0] / 5.0);

    [Fact]
    public void ReadAt_ReturnsTheFrameAtEachPositionReadingForward()
    {
        using var reader = new OpenCvVideoFrameRepository();

        var frames = new[] { 0.0, 1.0, 2.5 }
            .Select(seconds => reader.ReadAt(_videoPath, TimeSpan.FromSeconds(seconds)))
            .ToList();

        Assert.Equal([0, 10, 25], frames.Select(FrameNumber));
        Assert.All(frames, frame => Assert.Equal((32, 24), (frame!.Width, frame.Height)));
    }

    [Fact]
    public void ReadAt_APositionBeforeTheLastRead_GoesBackForIt()
    {
        using var reader = new OpenCvVideoFrameRepository();
        reader.ReadAt(_videoPath, TimeSpan.FromSeconds(3));

        Assert.Equal(10, FrameNumber(reader.ReadAt(_videoPath, TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void ReadAt_AnotherVideo_OpensThatOne()
    {
        var otherPath = WriteVideo("other.avi");
        using var reader = new OpenCvVideoFrameRepository();
        reader.ReadAt(_videoPath, TimeSpan.FromSeconds(1));

        Assert.Equal(20, FrameNumber(reader.ReadAt(otherPath, TimeSpan.FromSeconds(2))));
    }

    [Fact]
    public void ReadAt_BeforeTheStartOrPastTheEnd_ReturnsNull()
    {
        using var reader = new OpenCvVideoFrameRepository();

        Assert.Null(reader.ReadAt(_videoPath, TimeSpan.FromSeconds(-1)));
        Assert.Null(reader.ReadAt(_videoPath, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void ReadAt_AFileThatCannotBeOpened_Throws()
    {
        using var reader = new OpenCvVideoFrameRepository();

        Assert.Throws<FileNotFoundException>(() => reader.ReadAt(Path.Combine(_directory, "missing.avi"), TimeSpan.Zero));
    }

    [Fact]
    public void Dispose_BeforeReadingAnything_DoesNothing()
    {
        var reader = new OpenCvVideoFrameRepository();

        reader.Dispose();

        Assert.Throws<FileNotFoundException>(() => reader.ReadAt(Path.Combine(_directory, "missing.avi"), TimeSpan.Zero));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
