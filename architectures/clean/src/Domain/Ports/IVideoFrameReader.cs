using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.Ports;

// The seam a video decoder sits behind. A run asks for its frames in time order; the answer
// is null when the video has no frame at that position (before its start or past its end).
public interface IVideoFrameReader
{
    FrameImage? ReadAt(string videoPath, TimeSpan position);
}
