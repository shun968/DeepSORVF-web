using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// Mock per issue #1: real detection (YOLOX) needs model weights this repository does not
// carry, and there is no video decoder in this port to feed it anyway.
//
// The boxes are placed on the AIS positions the camera can see, offset by a few pixels, so
// the tracking and fusion stages downstream have something realistic to work on. That
// makes detection circular with fusion by construction — matching one of these boxes to
// its AIS record proves the pipeline is wired up, not that fusion works on real footage.
public sealed class DetectionService
{
    private const int BoxHalfWidth = 30;
    private const int BoxHalfHeight = 20;
    private const int OffsetFromAisX = 6;
    private const int OffsetFromAisY = 4;

    // Stands in for a vessel with no AIS transponder, so every frame also carries one
    // track that cannot be bound to an MMSI.
    private const int UnknownVesselX = 200;
    private const int UnknownVesselY = 700;

    public IReadOnlyList<DetectionBox> Detect(
        IReadOnlyList<ProjectedAisRecord> visibleAis,
        DateTimeOffset timestamp)
    {
        var boxes = new List<DetectionBox>(visibleAis.Count + 1);

        foreach (var ais in visibleAis)
        {
            boxes.Add(CreateBox(ais.X + OffsetFromAisX, ais.Y + OffsetFromAisY, timestamp));
        }

        boxes.Add(CreateBox(UnknownVesselX, UnknownVesselY, timestamp));

        return boxes;
    }

    private static DetectionBox CreateBox(int centreX, int centreY, DateTimeOffset timestamp) =>
        new(
            centreX - BoxHalfWidth,
            centreY - BoxHalfHeight,
            centreX + BoxHalfWidth,
            centreY + BoxHalfHeight,
            timestamp);
}
