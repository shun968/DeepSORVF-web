using System.Buffers.Binary;
using System.Text;

namespace CleanArchitecture.Web.Video;

// Some MP4s carry an edit list whose leading empty edit (a start delay) has a nonsensical
// length. clip-01's is -0.04s written as an unsigned 32-bit value, about 13 hours at its 90kHz
// timescale (ffmpeg reports "start: 47721.818844"). Browsers' demuxers honour it, so the
// video's timeline begins 47,721s in and playback jumps there the moment it starts, which the
// viewer page cannot line its frames up with.
//
// This finds such edits (empty edits longer than the whole movie) so the video endpoint can
// serve them with a length of zero. The file on disk is left as it is.
public static class Mp4EditListRepair
{
    private const long EmptyEdit = -1;
    private const int MaxBoxBodyBytes = 1 << 16;

    // The byte ranges holding those edits' segment durations. The stream is left at the
    // position it was given at, so it can be served from there straight after.
    public static IReadOnlyList<ByteRange> FindImplausibleEmptyEdits(Stream mp4)
    {
        var position = mp4.Position;
        try
        {
            return FindIn(mp4);
        }
        finally
        {
            mp4.Position = position;
        }
    }

    private static List<ByteRange> FindIn(Stream mp4)
    {
        if (FindChild(mp4, 0, mp4.Length, "moov") is not { } movie
            || FindChild(mp4, movie.BodyStart, movie.End, "mvhd") is not { } movieHeader)
        {
            return [];
        }

        var movieDuration = ReadMovieDuration(mp4, movieHeader);
        var ranges = new List<ByteRange>();
        var tracks = Children(mp4, movie.BodyStart, movie.End).Where(box => box.Type == "trak").ToList();
        foreach (var track in tracks)
        {
            if (FindChild(mp4, track.BodyStart, track.End, "edts") is { } edits
                && FindChild(mp4, edits.BodyStart, edits.End, "elst") is { } editList)
            {
                ranges.AddRange(EmptyEditsLongerThan(mp4, editList, movieDuration));
            }
        }

        return ranges;
    }

    private static Mp4Box? FindChild(Stream mp4, long start, long end, string type)
    {
        foreach (var box in Children(mp4, start, end))
        {
            if (box.Type == type)
            {
                return box;
            }
        }

        return null;
    }

    private static IEnumerable<Mp4Box> Children(Stream mp4, long start, long end)
    {
        var header = new byte[16];
        var position = start;
        while (position + 8 <= end)
        {
            mp4.Position = position;
            mp4.ReadExactly(header, 0, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var headerLength = 8;
            if (size == 1)
            {
                // The real size follows as a 64-bit "largesize".
                if (position + 16 > end)
                {
                    yield break;
                }

                mp4.ReadExactly(header, 8, 8);
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
                headerLength = 16;
            }
            else if (size == 0)
            {
                // A box of size zero runs to the end of its parent.
                size = end - position;
            }

            if (size < headerLength || size > end - position)
            {
                yield break;
            }

            yield return new Mp4Box(Encoding.ASCII.GetString(header, 4, 4), position + headerLength, position + size);
            position += size;
        }
    }

    // mvhd: version(1) flags(3), then creation time, modification time, timescale and duration:
    // 32-bit each in version 0, with the times and duration 64-bit in version 1.
    private static ulong ReadMovieDuration(Stream mp4, Mp4Box movieHeader)
    {
        var body = ReadBody(mp4, movieHeader);
        if (body.Length >= 32 && body[0] == 1)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(body.AsSpan(24));
        }

        if (body.Length >= 20 && body[0] == 0)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16));
        }

        // Unreadable: treat the movie as unbounded, so that no edit counts as implausible.
        return ulong.MaxValue;
    }

    // elst: version(1) flags(3) entry_count(4), then for each entry segment_duration and
    // media_time (32-bit in version 0, 64-bit in version 1) followed by a 4-byte media rate.
    private static List<ByteRange> EmptyEditsLongerThan(Stream mp4, Mp4Box editList, ulong movieDuration)
    {
        var ranges = new List<ByteRange>();
        var body = ReadBody(mp4, editList);
        if (body.Length < 8)
        {
            return ranges;
        }

        var fieldLength = body[0] == 1 ? 8 : 4;
        var entryLength = (fieldLength * 2) + 4;
        var entryCount = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(4));
        for (var index = 0L; index < entryCount && 8 + ((index + 1) * entryLength) <= body.Length; index++)
        {
            var entryOffset = (int)(8 + (index * entryLength));
            var entry = body.AsSpan(entryOffset);
            var segmentDuration = fieldLength == 8
                ? BinaryPrimitives.ReadUInt64BigEndian(entry)
                : BinaryPrimitives.ReadUInt32BigEndian(entry);
            var mediaTime = fieldLength == 8
                ? BinaryPrimitives.ReadInt64BigEndian(entry[fieldLength..])
                : BinaryPrimitives.ReadInt32BigEndian(entry[fieldLength..]);
            if (mediaTime == EmptyEdit && segmentDuration > movieDuration)
            {
                ranges.Add(new ByteRange(editList.BodyStart + entryOffset, fieldLength));
            }
        }

        return ranges;
    }

    private static byte[] ReadBody(Stream mp4, Mp4Box box)
    {
        var body = new byte[Math.Min(box.End - box.BodyStart, MaxBoxBodyBytes)];
        mp4.Position = box.BodyStart;
        mp4.ReadExactly(body);
        return body;
    }

    private readonly record struct Mp4Box(string Type, long BodyStart, long End);
}
