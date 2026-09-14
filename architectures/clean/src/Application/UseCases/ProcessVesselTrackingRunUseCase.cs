using CleanArchitecture.Domain.Geometry;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Application.UseCases;

public sealed record ProcessVesselTrackingRunRequest(
    string AisDirectoryPath,
    string CameraParametersPath,
    DateTimeOffset StartTimeUtc,
    int FrameCount,
    TimeSpan FrameInterval,
    string? VideoPath = null,
    DateTimeOffset? VideoStartTime = null);

// ImageWidth/ImageHeight are the size of the image the frames were projected into.
public sealed record ProcessVesselTrackingRunResponse(
    int ImageWidth,
    int ImageHeight,
    IReadOnlyList<ProcessVideoFrameResponse> Frames);

// Runs the per-frame use case over a sequence of frames, after reading the camera
// calibration the whole run shares.
public sealed class ProcessVesselTrackingRunUseCase
{
    private readonly ICameraParametersReader _cameraParametersReader;
    private readonly ProcessVideoFrameUseCase _processVideoFrame;

    public ProcessVesselTrackingRunUseCase(
        ICameraParametersReader cameraParametersReader,
        ProcessVideoFrameUseCase processVideoFrame)
    {
        _cameraParametersReader = cameraParametersReader;
        _processVideoFrame = processVideoFrame;
    }

    public ProcessVesselTrackingRunResponse Execute(ProcessVesselTrackingRunRequest request)
    {
        var parameters = _cameraParametersReader.Read(request.CameraParametersPath);
        var camera = new CameraGeometry(parameters);

        // The principal point sits at the image centre, so doubling it recovers the frame
        // size, and its smaller component is the min(width, height) / 2 gate the paper's
        // fusion uses.
        var imageWidth = (int)(parameters.PrincipalPointX * 2);
        var imageHeight = (int)(parameters.PrincipalPointY * 2);
        var maxMatchDistancePixels = Math.Min(parameters.PrincipalPointX, parameters.PrincipalPointY);

        // The video starts with the run unless told otherwise, and each frame's picture is taken
        // from the moment that frame represents.
        var videoStartTime = request.VideoStartTime ?? request.StartTimeUtc;
        var frames = new List<ProcessVideoFrameResponse>(request.FrameCount);
        for (var frameIndex = 0; frameIndex < request.FrameCount; frameIndex++)
        {
            var timestamp = request.StartTimeUtc + (request.FrameInterval * frameIndex);
            frames.Add(_processVideoFrame.Execute(new ProcessVideoFrameRequest(
                request.AisDirectoryPath,
                camera,
                maxMatchDistancePixels,
                frameIndex,
                timestamp,
                imageWidth,
                imageHeight,
                request.VideoPath,
                timestamp - videoStartTime)));
        }

        return new ProcessVesselTrackingRunResponse(imageWidth, imageHeight, frames);
    }
}
