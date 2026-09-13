namespace LayeredArchitecture.Domain.Entities;

public sealed class VisualTrack
{
    public int TrackId { get; }
    public DetectionBox Box { get; }

    public VisualTrack(int trackId, DetectionBox box)
    {
        TrackId = trackId;
        Box = box;
    }
}
