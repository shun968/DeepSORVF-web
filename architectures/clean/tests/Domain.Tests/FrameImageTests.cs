using CleanArchitecture.Domain.Entities;
using Xunit;

namespace CleanArchitecture.Domain.Tests;

public class FrameImageTests
{
    [Fact]
    public void Constructor_KeepsTheSizeAndPixels()
    {
        var pixels = new byte[2 * 3 * 3];
        pixels[0] = 7;

        var image = new FrameImage(2, 3, pixels);

        Assert.Equal(2, image.Width);
        Assert.Equal(3, image.Height);
        Assert.Equal(7, image.Rgb.Span[0]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Constructor_WithANonPositiveSize_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameImage(width, height, Array.Empty<byte>()));
    }

    [Fact]
    public void Constructor_WithTheWrongNumberOfBytes_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FrameImage(2, 2, new byte[11]));
    }

    [Fact]
    public void VideoFrame_KeepsThePictureItWasGiven()
    {
        var image = new FrameImage(1, 1, new byte[3]);

        Assert.Same(image, new VideoFrame(0, DateTimeOffset.UnixEpoch, 1, 1, image).Image);
        Assert.Null(new VideoFrame(0, DateTimeOffset.UnixEpoch, 1, 1).Image);
    }
}
