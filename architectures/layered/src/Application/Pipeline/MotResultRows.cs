using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Application.Pipeline;

// Builds the MOT-Challenge-style rows utils/gen_result.py writes out of the pipeline's
// per-frame results. Boxes are clamped to the frame, as the original does, so a vessel
// half out of shot does not report a box reaching outside the image.
public static class MotResultRows
{
    public static IReadOnlyList<MotResultRow> Detections(
        IReadOnlyList<FrameResult> frames,
        double imageWidth,
        double imageHeight) =>
        frames
            .SelectMany(frame => frame.VisualTracks.Select(track =>
                // Raw detections have no identity yet, so the original writes a zero here.
                Row(FrameNumber(frame), 0, track.Box, imageWidth, imageHeight)))
            .ToList();

    public static IReadOnlyList<MotResultRow> Tracks(
        IReadOnlyList<FrameResult> frames,
        double imageWidth,
        double imageHeight) =>
        frames
            .SelectMany(frame => frame.VisualTracks.Select(track =>
                Row(FrameNumber(frame), track.TrackId, track.Box, imageWidth, imageHeight)))
            .ToList();

    public static IReadOnlyList<MotResultRow> Fusions(
        IReadOnlyList<FrameResult> frames,
        double imageWidth,
        double imageHeight) =>
        frames
            .SelectMany(frame => frame.FusedTracks
                .Where(fused => fused.MatchedAis is not null)
                .Select(fused => Row(FrameNumber(frame), fused.MatchedAis!.Mmsi, fused.Box, imageWidth, imageHeight)))
            .ToList();

    // MOT numbers frames from one.
    private static int FrameNumber(FrameResult frame) => frame.FrameIndex + 1;

    private static MotResultRow Row(int frame, long id, DetectionBox box, double imageWidth, double imageHeight)
    {
        var left = Math.Max(box.X1, 0);
        var top = Math.Max(box.Y1, 0);
        var right = Math.Min(box.X2, imageWidth);
        var bottom = Math.Min(box.Y2, imageHeight);

        return new MotResultRow(
            frame,
            id,
            (int)left,
            (int)top,
            (int)Math.Abs(right - left),
            (int)Math.Abs(bottom - top));
    }
}
