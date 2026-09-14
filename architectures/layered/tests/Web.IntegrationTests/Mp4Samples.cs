using System.Buffers.Binary;
using System.Text;

namespace LayeredArchitecture.Web.IntegrationTests;

// Builds just enough of an MP4's box structure for the edit list repair to walk.
internal static class Mp4Samples
{
    public const uint MovieDuration = 10_048_410;

    // clip-01's leading empty edit: -3600 (-0.04s at 90kHz) stored as an unsigned 32-bit value.
    public const uint BrokenStartDelay = 4_294_963_696;

    public static byte[] WithBrokenStartDelay() =>
        File(MovieHeader(MovieDuration), Track(EditList(Edit(BrokenStartDelay, -1), Edit(MovieDuration, 0))), Track(null));

    public static byte[] File(params byte[][] movieChildren) =>
        [.. Box("ftyp", Ascii("isom"), U32(0)), .. Box("moov", movieChildren), .. Box("mdat", new byte[16])];

    public static byte[] Box(string type, params byte[][] content)
    {
        var body = content.SelectMany(part => part).ToArray();
        return [.. U32((uint)(8 + body.Length)), .. Ascii(type), .. body];
    }

    public static byte[] LargeBox(string type, params byte[][] content)
    {
        var body = content.SelectMany(part => part).ToArray();
        return [.. U32(1), .. Ascii(type), .. U64((ulong)(16 + body.Length)), .. body];
    }

    public static byte[] MovieHeader(uint duration) =>
        Box("mvhd", Bytes(0, 0, 0, 0), U32(0), U32(0), U32(90_000), U32(duration), new byte[80]);

    public static byte[] MovieHeaderVersion1(ulong duration) =>
        Box("mvhd", Bytes(1, 0, 0, 0), U64(0), U64(0), U32(90_000), U64(duration), new byte[80]);

    public static byte[] Track(byte[]? editList) =>
        editList is null
            ? Box("trak", Box("tkhd", new byte[84]))
            : Box("trak", Box("tkhd", new byte[84]), Box("edts", editList));

    public static byte[] EditList(params byte[][] entries) =>
        Box("elst", [Bytes(0, 0, 0, 0), U32((uint)entries.Length), .. entries]);

    public static byte[] EditListVersion1(params byte[][] entries) =>
        Box("elst", [Bytes(1, 0, 0, 0), U32((uint)entries.Length), .. entries]);

    public static byte[] Edit(uint segmentDuration, int mediaTime) =>
        [.. U32(segmentDuration), .. I32(mediaTime), 0, 1, 0, 0];

    public static byte[] EditVersion1(ulong segmentDuration, long mediaTime) =>
        [.. U64(segmentDuration), .. I64(mediaTime), 0, 1, 0, 0];

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static byte[] Bytes(params byte[] values) => values;

    public static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    public static byte[] U64(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] I32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] I64(long value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
