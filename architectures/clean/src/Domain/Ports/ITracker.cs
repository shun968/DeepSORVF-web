using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.Ports;

// The seam DeepSORT would sit behind.
public interface ITracker
{
    IReadOnlyList<Track> Track(IReadOnlyList<Detection> detections, DateTimeOffset timestamp);
}
