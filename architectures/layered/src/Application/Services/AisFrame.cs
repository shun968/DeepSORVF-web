using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Services;

// The two AIS collections one frame of AISPRO produces: the vessels the camera can see
// right now (AIS_vis_cur), and the trailing window of projected positions that trajectory
// matching compares against (AIS_vis).
public sealed record AisFrame(
    IReadOnlyList<ProjectedAisRecord> Visible,
    IReadOnlyList<ProjectedAisRecord> History);
