namespace CleanArchitecture.Domain.Entities;

// An AIS vessel the camera can see, with the pixel coordinates its position projects to.
public sealed class VisibleVessel
{
    public AisRecord Record { get; }
    public int X { get; }
    public int Y { get; }

    public VisibleVessel(AisRecord record, int x, int y)
    {
        Record = record;
        X = x;
        Y = y;
    }
}
