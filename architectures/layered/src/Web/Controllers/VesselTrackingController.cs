using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Web.Contracts;
using LayeredArchitecture.Web.Video;
using LayeredArchitecture.Web.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace LayeredArchitecture.Web.Controllers;

[ApiController]
[Route("api/vessel-tracking")]
public sealed class VesselTrackingController : ControllerBase
{
    private readonly VesselTrackingPipeline _pipeline;
    private readonly RunDefaults _runDefaults;

    public VesselTrackingController(VesselTrackingPipeline pipeline, IOptions<RunDefaults> runDefaults)
    {
        _pipeline = pipeline;
        _runDefaults = runDefaults.Value;
    }

    // Issue #1's checklist explicitly asks how continuous, long-running frame processing
    // fits a request/response model. Since detection/tracking are instant mocks here,
    // this stays a single synchronous request-response — noted in the issue's own
    // reflection comment that a real video would need an async job/polling design this
    // layering alone doesn't obviously provide, rather than building one just to prove it.
    [HttpPost("runs")]
    public ActionResult<VesselTrackingRunResponse> CreateRun(VesselTrackingRunRequest request)
    {
        if (!Directory.Exists(request.AisDataDirectory))
        {
            return BadRequest($"AIS data directory not found: {request.AisDataDirectory}");
        }

        if (!System.IO.File.Exists(request.CameraParametersPath))
        {
            return BadRequest($"Camera parameters file not found: {request.CameraParametersPath}");
        }

        if (request.FrameCount <= 0)
        {
            return BadRequest("FrameCount must be greater than zero.");
        }

        if (request.FrameIntervalSeconds <= 0)
        {
            return BadRequest("FrameIntervalSeconds must be greater than zero.");
        }

        try
        {
            var run = _pipeline.ProcessFrames(
                request.AisDataDirectory,
                request.CameraParametersPath,
                request.StartTime,
                request.FrameCount,
                TimeSpan.FromSeconds(request.FrameIntervalSeconds),
                request.ResultDirectory,
                string.IsNullOrEmpty(_runDefaults.VideoPath) ? null : _runDefaults.VideoPath,
                request.VideoStartTime);

            return Ok(FrameResultMapper.ToResponse(run));
        }
        catch (FileNotFoundException error)
        {
            // The configured video could not be opened, or the YOLOX model is not exported yet.
            return BadRequest(error.Message);
        }
    }

    // What the viewer page (wwwroot/index.html) pre-fills its form with.
    [HttpGet("run-defaults")]
    public ActionResult<RunDefaults> GetRunDefaults() => _runDefaults;

    // Streams the video configured at startup (RunDefaults:VideoPath) for the viewer page to
    // draw a run over. Only that one file is served: taking the path from the request would
    // let any caller read any file the app can. An MP4 whose edit list holds an implausible
    // start delay is served with that delay read as zero (see Mp4EditListRepair).
    [HttpGet("video")]
    public IActionResult GetVideo()
    {
        var path = _runDefaults.VideoPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        // The FileStreamResult disposes the stream, and with it the file, once the response is sent.
        var file = System.IO.File.OpenRead(path);
        var repairs = Mp4EditListRepair.FindImplausibleEmptyEdits(file);
        return File(new ZeroedRangesStream(file, repairs), contentType, enableRangeProcessing: true);
    }
}
