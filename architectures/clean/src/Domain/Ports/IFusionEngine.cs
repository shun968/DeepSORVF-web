using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.Ports;

// The seam the paper's trajectory matching would sit behind.
public interface IFusionEngine
{
    IReadOnlyList<FusionResult> Fuse(
        IReadOnlyList<Track> tracks,
        IReadOnlyList<VisibleVessel> vessels,
        double maxMatchDistancePixels,
        DateTimeOffset timestamp);
}
