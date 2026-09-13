namespace LayeredArchitecture.Domain.Entities;

public sealed class DetectionBox
{
    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }
    public DateTimeOffset Timestamp { get; }

    public DetectionBox(double x1, double y1, double x2, double y2, DateTimeOffset timestamp)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        Timestamp = timestamp;
    }

    public double CenterX => (X1 + X2) / 2;
    public double CenterY => (Y1 + Y2) / 2;
    public double Width => X2 - X1;
    public double Height => Y2 - Y1;
}
