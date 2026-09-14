using System.Buffers.Binary;
using LayeredArchitecture.Web.Video;
using Xunit;
using static LayeredArchitecture.Web.IntegrationTests.Mp4Samples;

namespace LayeredArchitecture.Web.IntegrationTests;

public class Mp4EditListRepairTests
{
    private static IReadOnlyList<ByteRange> Find(byte[] mp4) =>
        Mp4EditListRepair.FindImplausibleEmptyEdits(new MemoryStream(mp4));

    [Fact]
    public void FindImplausibleEmptyEdits_FindsAnEmptyEditLongerThanTheMovie()
    {
        var mp4 = WithBrokenStartDelay();
        var stream = new MemoryStream(mp4);

        var range = Assert.Single(Mp4EditListRepair.FindImplausibleEmptyEdits(stream));

        Assert.Equal(4, range.Length);
        Assert.Equal(BrokenStartDelay, BinaryPrimitives.ReadUInt32BigEndian(mp4.AsSpan((int)range.Offset)));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void FindImplausibleEmptyEdits_LeavesAnOrdinaryStartDelayAlone()
    {
        var mp4 = File(MovieHeader(MovieDuration), Track(EditList(Edit(3600, -1), Edit(MovieDuration, 0))));

        Assert.Empty(Find(mp4));
    }

    [Fact]
    public void FindImplausibleEmptyEdits_ReadsVersion1BoxesAndLargeSizes()
    {
        byte[] mp4 =
        [
            .. Box("ftyp", Ascii("isom"), U32(0)),
            .. LargeBox("moov", MovieHeaderVersion1(MovieDuration), Track(EditListVersion1(EditVersion1(ulong.MaxValue - 1, -1)))),
        ];

        var range = Assert.Single(Find(mp4));

        Assert.Equal(8, range.Length);
        Assert.Equal(ulong.MaxValue - 1, BinaryPrimitives.ReadUInt64BigEndian(mp4.AsSpan((int)range.Offset)));
    }

    [Fact]
    public void FindImplausibleEmptyEdits_WithoutAMovieBox_FindsNothing()
    {
        // The last box has size zero, meaning it runs to the end of the file.
        byte[] mp4 = [.. Box("ftyp", Ascii("isom"), U32(0)), .. U32(0), .. Ascii("mdat"), .. new byte[32]];

        Assert.Empty(Find(mp4));
    }

    [Fact]
    public void FindImplausibleEmptyEdits_StopsAtABoxThatOverrunsTheFile()
    {
        byte[] mp4 = [.. Box("ftyp", Ascii("isom"), U32(0)), .. U32(1000), .. Ascii("moov")];

        Assert.Empty(Find(mp4));
    }

    [Fact]
    public void FindImplausibleEmptyEdits_StopsAtATruncatedLargeSizeHeader()
    {
        byte[] mp4 = [.. U32(1), .. Ascii("moov"), 0, 0];

        Assert.Empty(Find(mp4));
    }

    [Fact]
    public void FindImplausibleEmptyEdits_WithAnUnreadableMovieHeader_FindsNothing()
    {
        var mp4 = File(Box("mvhd", Bytes(0, 0, 0, 0)), Track(EditList(Edit(BrokenStartDelay, -1))));

        Assert.Empty(Find(mp4));
    }

    [Fact]
    public void FindImplausibleEmptyEdits_ReadsOnlyTheEntriesAnEditListActuallyHolds()
    {
        // One edit list claims two entries but holds one; another is too short to hold a count.
        var claimsTwo = Box("elst", Bytes(0, 0, 0, 0), U32(2), Edit(BrokenStartDelay, -1));
        var truncated = Box("elst", Bytes(0, 0, 0, 0));
        var mp4 = File(MovieHeader(MovieDuration), Track(claimsTwo), Track(truncated));

        Assert.Single(Find(mp4));
    }
}
