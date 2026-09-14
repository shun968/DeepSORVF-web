using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Domain.Repositories;

// Reads the picture of a video at a moment. A run asks for its frames in time order; the answer
// is null when the video has no frame at that position (before its start or past its end).
public interface IVideoFrameRepository
{
    FrameImage? ReadAt(string videoPath, TimeSpan position);
}
