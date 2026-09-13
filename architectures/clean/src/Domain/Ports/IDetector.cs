using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.Ports;

// The seam YOLOX would sit behind. Nothing above this line knows whether the boxes come
// from a neural network, a recording, or a stand-in.
public interface IDetector
{
    IReadOnlyList<Detection> Detect(VideoFrame frame);
}
