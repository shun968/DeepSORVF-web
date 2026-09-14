namespace LayeredArchitecture.Web.Contracts;

// The "RunDefaults" configuration section: what the viewer page (wwwroot/index.html)
// pre-fills its form with, and the video it draws a run over. `task run` sets these through
// environment variables (RunDefaults__AisDataDirectory, ...) so the page opens on the
// bundled sample data.
public sealed class RunDefaults
{
    public const string SectionName = "RunDefaults";

    public string? AisDataDirectory { get; set; }
    public string? CameraParametersPath { get; set; }

    // Kept as text so the page shows the offset exactly as it was given.
    public string? StartTime { get; set; }
    public int? FrameCount { get; set; }
    public int? FrameIntervalSeconds { get; set; }
    public string? ResultDirectory { get; set; }
    public string? VideoPath { get; set; }

    // When the video starts, if not at StartTime. Text for the same reason as StartTime.
    public string? VideoStartTime { get; set; }
}
