using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Web.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchitecture.Web.Controllers;

// Deliberately thin, per issue #2: validate the request, hand it to the use case, map the
// result back. No pipeline knowledge lives here.
[ApiController]
[Route("api/vessel-tracking")]
public sealed class VesselTrackingController : ControllerBase
{
    private readonly ProcessVesselTrackingRunUseCase _useCase;

    public VesselTrackingController(ProcessVesselTrackingRunUseCase useCase)
    {
        _useCase = useCase;
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

        var response = _useCase.Execute(new ProcessVesselTrackingRunRequest(
            request.AisDataDirectory,
            request.CameraParametersPath,
            request.StartTime,
            request.FrameCount,
            TimeSpan.FromSeconds(request.FrameIntervalSeconds)));

        return Ok(VesselTrackingMapper.ToResponse(response));
    }
}
