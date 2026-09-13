namespace LayeredArchitecture.Domain.Entities;

// An AIS record whose position has been projected into image pixel coordinates — one row
// of the Python original's AIS_vis frame (utils/AIS_utils.py's transform).
public sealed class ProjectedAisRecord
{
    public AisRecord Record { get; }
    public int X { get; }
    public int Y { get; }

    public ProjectedAisRecord(AisRecord record, int x, int y)
    {
        Record = record;
        X = x;
        Y = y;
    }
}
