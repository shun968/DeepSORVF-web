using System.Globalization;
using LayeredArchitecture.Domain.Geometry;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Infrastructure.Repositories;

// Reads the per-clip camera calibration file described in utils/file_read.py: a single
// line holding the 11 values in a fixed order, e.g.
//   [121.5,29.87,90,5,20,55,35,1500,1500,960,540]
//
// The Python original slices the raw line positionally (`readlines()[0][1:-2]`), which
// silently eats a digit if the file has no trailing newline. This trims the brackets and
// surrounding whitespace instead — same input, one less way to misread it.
public sealed class TextFileCameraParametersRepository : ICameraParametersRepository
{
    private const int ExpectedValueCount = 11;

    public CameraParameters Load(string cameraParametersPath)
    {
        var contents = File.ReadAllText(cameraParametersPath).Trim().Trim('[', ']');
        var values = contents
            .Split(',')
            .Select(value => double.Parse(value.Trim(), CultureInfo.InvariantCulture))
            .ToArray();

        if (values.Length != ExpectedValueCount)
        {
            throw new FormatException(
                $"Expected {ExpectedValueCount} camera parameters in '{cameraParametersPath}' but found {values.Length}.");
        }

        return new CameraParameters(
            longitudeDegrees: values[0],
            latitudeDegrees: values[1],
            bearingDegrees: values[2],
            tiltDegrees: values[3],
            heightMeters: values[4],
            horizontalFovDegrees: values[5],
            verticalFovDegrees: values[6],
            focalLengthX: values[7],
            focalLengthY: values[8],
            principalPointX: values[9],
            principalPointY: values[10]);
    }
}
