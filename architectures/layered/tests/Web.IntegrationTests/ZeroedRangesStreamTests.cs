using LayeredArchitecture.Web.Video;
using Xunit;

namespace LayeredArchitecture.Web.IntegrationTests;

public class ZeroedRangesStreamTests
{
    private static readonly byte[] Data = Enumerable.Range(1, 100).Select(value => (byte)value).ToArray();
    private static readonly ByteRange[] Ranges = [new(10, 4), new(50, 8)];

    private static byte[] Expected()
    {
        var expected = Data.ToArray();
        foreach (var range in Ranges)
        {
            Array.Clear(expected, (int)range.Offset, range.Length);
        }

        return expected;
    }

    [Fact]
    public void Read_ReturnsZerosInsideTheRangesAndTheDataElsewhere()
    {
        using var stream = new ZeroedRangesStream(new MemoryStream(Data), Ranges);
        var result = new List<byte>();
        var buffer = new byte[7];

        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            result.AddRange(buffer.Take(read));
        }

        Assert.Equal(Expected(), result);
    }

    [Fact]
    public async Task ReadAsync_AfterASeek_ReturnsTheSameBytes()
    {
        using var stream = new ZeroedRangesStream(new MemoryStream(Data), Ranges);
        var buffer = new byte[20];

        stream.Seek(45, SeekOrigin.Begin);
        var read = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(20, read);
        Assert.Equal(Expected().Skip(45).Take(20), buffer);
        Assert.Equal(65, stream.Position);
    }

    [Fact]
    public void IsAReadOnlySeekableView_ThatDisposesTheInnerStream()
    {
        var inner = new MemoryStream(Data);
        var stream = new ZeroedRangesStream(inner, Ranges);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(Data.Length, stream.Length);
        stream.Position = 12;
        Assert.Equal(0, stream.ReadByte());
        stream.Flush();
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));

        stream.Dispose();

        Assert.False(inner.CanRead);
    }
}
