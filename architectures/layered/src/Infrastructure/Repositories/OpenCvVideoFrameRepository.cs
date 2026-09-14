using System.Runtime.InteropServices;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;
using OpenCvSharp;

namespace LayeredArchitecture.Infrastructure.Repositories;

// Decodes frames with OpenCV and the FFmpeg its official Linux runtime bundles.
//
// A run asks for its frames in time order, so the video stays open for the length of one
// request and is read forward from the last frame. For clip-01's 110 one-second frames that
// takes about 7s, against 43s for seeking to each one, since every seek decodes again from the
// nearest key frame. Asking for a position at or before the last frame read reopens the video.
public sealed class OpenCvVideoFrameRepository : IVideoFrameRepository, IDisposable
{
    // A decoded frame counts as the one at a position when it starts within this much of it,
    // which absorbs rounding in the container's timestamps (clip-01's frames are 40ms apart).
    private const double ToleranceMilliseconds = 20;

    private VideoCapture? _capture;
    private string? _openPath;
    private double _lastFrameMilliseconds;

    public FrameImage? ReadAt(string videoPath, TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            return null;
        }

        var target = position.TotalMilliseconds;
        var capture = CaptureFor(videoPath, target);
        while (capture.Grab())
        {
            _lastFrameMilliseconds = capture.PosMsec;
            if (_lastFrameMilliseconds + ToleranceMilliseconds >= target)
            {
                using var frame = new Mat();
                capture.Retrieve(frame);
                return ToImage(frame);
            }
        }

        return null;
    }

    public void Dispose() => _capture?.Dispose();

    private VideoCapture CaptureFor(string videoPath, double target)
    {
        if (_capture is not null && _openPath == videoPath && target > _lastFrameMilliseconds)
        {
            return _capture;
        }

        _capture?.Dispose();
        _openPath = null;
        _capture = new VideoCapture(videoPath);
        if (!_capture.IsOpened())
        {
            throw new FileNotFoundException($"Could not open the video: {videoPath}", videoPath);
        }

        _openPath = videoPath;
        _lastFrameMilliseconds = double.NegativeInfinity;
        if (target > 0)
        {
            _capture.PosMsec = (int)target;
        }

        return _capture;
    }

    private static FrameImage ToImage(Mat bgr)
    {
        using var rgb = new Mat();
        Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);
        var pixels = new byte[rgb.Width * rgb.Height * 3];
        Marshal.Copy(rgb.Data, pixels, 0, pixels.Length);
        return new FrameImage(rgb.Width, rgb.Height, pixels);
    }
}
