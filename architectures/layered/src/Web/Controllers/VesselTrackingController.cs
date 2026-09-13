using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Web.Contracts;
using LayeredArchitecture.Web.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace LayeredArchitecture.Web.Controllers;

[ApiController]
[Route("api/vessel-tracking")]
public sealed class VesselTrackingController : ControllerBase
{
    private readonly VesselTrackingPipeline _pipeline;

    public VesselTrackingController(VesselTrackingPipeline pipeline)
    {
        _pipeline = pipeline;
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

        var frames = _pipeline.ProcessFrames(
            request.AisDataDirectory,
            request.CameraParametersPath,
            request.StartTime,
            request.FrameCount,
            TimeSpan.FromSeconds(request.FrameIntervalSeconds),
            request.ResultDirectory);

        return Ok(FrameResultMapper.ToResponse(frames));
    }
}
