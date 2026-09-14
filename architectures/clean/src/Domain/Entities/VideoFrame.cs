namespace CleanArchitecture.Domain.Entities;

// One frame handed to the detector: its position in the run, the moment it represents, and
// the decoded picture when the run has a video to take it from. Without one there is nothing
// to look at, so Image is null.
public sealed class VideoFrame
{
    public int Index { get; }
    public DateTimeOffset Timestamp { get; }
    public int Width { get; }
    public int Height { get; }
    public FrameImage? Image { get; }

    public VideoFrame(int index, DateTimeOffset timestamp, int width, int height, FrameImage? image = null)
    {
        Index = index;
        Timestamp = timestamp;
        Width = width;
        Height = height;
        Image = image;
    }
}
