namespace LayeredArchitecture.Application.Pipeline;

// The frames of one run, plus the size of the image they were projected into (the
// calibration's principal point doubled, since it sits at the image centre).
public sealed record VesselTrackingRunResult(double ImageWidth, double ImageHeight, IReadOnlyList<FrameResult> Frames);
