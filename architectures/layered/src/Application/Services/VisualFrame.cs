using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// The two visual collections one frame of VISPRO produces: the tracks visible right now
// (Vis_cur) and the trailing window of where they have been (Vis_tra), which trajectory
// matching compares against the AIS side.
public sealed record VisualFrame(
    IReadOnlyList<VisualTrack> Current,
    IReadOnlyList<VisualTrack> History);
