namespace LayeredArchitecture.Domain.Entities;

// One row of the MOT-Challenge-style output utils/gen_result.py writes, so the results can
// be scored with the same tooling as the Python original.
//
// The original writes ten columns, `frame, id, x, y, w, h, 1, 1, 1, 1`; the trailing four
// are literal ones there rather than the confidence, class and visibility the format
// nominally reserves them for, so they are not modelled here.
public sealed class MotResultRow
{
    public int Frame { get; }

    // The vessel's identity for this kind of output: the track ID for tracking rows, the
    // MMSI for fusion rows, and always zero for detection rows, which have no identity yet.
    public long Id { get; }

    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public MotResultRow(int frame, long id, int x, int y, int width, int height)
    {
        Frame = frame;
        Id = id;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
