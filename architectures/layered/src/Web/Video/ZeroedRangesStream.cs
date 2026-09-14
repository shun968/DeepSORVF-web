namespace LayeredArchitecture.Web.Video;

// A read-only view of a stream that reads the given byte ranges back as zeros: how the video
// endpoint serves an MP4 with Mp4EditListRepair's fixes applied without touching the file.
public sealed class ZeroedRangesStream : Stream
{
    private readonly Stream _inner;
    private readonly IReadOnlyList<ByteRange> _ranges;

    public ZeroedRangesStream(Stream inner, IReadOnlyList<ByteRange> ranges)
    {
        _inner = inner;
        _ranges = ranges;
    }

    public override bool CanRead => true;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var start = _inner.Position;
        var read = _inner.Read(buffer);
        foreach (var range in _ranges)
        {
            var from = Math.Max(start, range.Offset);
            var to = Math.Min(start + read, range.Offset + range.Length);
            if (from < to)
            {
                buffer.Slice((int)(from - start), (int)(to - from)).Clear();
            }
        }

        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void Flush()
    {
        // Read-only: there is nothing to flush.
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
