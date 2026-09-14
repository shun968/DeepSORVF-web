using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Web.Contracts;
using CleanArchitecture.Web.Video;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Web.Controllers;

// Deliberately thin, per issue #2: validate the request, hand it to the use case, map the
// result back. No pipeline knowledge lives here.
[ApiController]
[Route("api/vessel-tracking")]
public sealed class VesselTrackingController : ControllerBase
{
    private readonly ProcessVesselTrackingRunUseCase _useCase;
    private readonly RunDefaults _runDefaults;

    public VesselTrackingController(ProcessVesselTrackingRunUseCase useCase, IOptions<RunDefaults> runDefaults)
    {
        _useCase = useCase;
        _runDefaults = runDefaults.Value;
    }

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
            var response = _useCase.Execute(new ProcessVesselTrackingRunRequest(
                request.AisDataDirectory,
                request.CameraParametersPath,
                request.StartTime,
                request.FrameCount,
                TimeSpan.FromSeconds(request.FrameIntervalSeconds),
                string.IsNullOrEmpty(_runDefaults.VideoPath) ? null : _runDefaults.VideoPath,
                request.VideoStartTime));

            return Ok(VesselTrackingMapper.ToResponse(response));
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
