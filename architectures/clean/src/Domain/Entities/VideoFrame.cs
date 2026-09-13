namespace CleanArchitecture.Domain.Entities;

// One frame handed to the detector. There is no video decoder in this port, so a frame is
// only its position in the run and the moment it represents — enough for a detector to be
// asked "what do you see here?" without the port having to know whether the answer comes
// from pixels or from a stand-in.
public sealed class VideoFrame
{
    public int Index { get; }
    public DateTimeOffset Timestamp { get; }
    public int Width { get; }
    public int Height { get; }

    public VideoFrame(int index, DateTimeOffset timestamp, int width, int height)
    {
        Index = index;
        Timestamp = timestamp;
        Width = width;
        Height = height;
    }
}
