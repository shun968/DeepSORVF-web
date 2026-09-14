using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Domain.Repositories;

// The detector (YOLOX) the Application layer asks what vessels a picture shows. Declared here,
// next to the data-access interfaces, because like them it is implemented in Infrastructure.
public interface IVesselDetector
{
    IReadOnlyList<DetectionBox> Detect(FrameImage image, DateTimeOffset timestamp);
}
