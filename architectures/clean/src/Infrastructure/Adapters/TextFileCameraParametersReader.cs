using System.Globalization;
using CleanArchitecture.Domain.Geometry;
using CleanArchitecture.Domain.Ports;

namespace CleanArchitecture.Infrastructure.Adapters;

// Reads the per-clip calibration file from utils/file_read.py: one line of 11 values, e.g.
//   [121.5,29.87,90,5,20,55,35,1500,1500,960,540]
public sealed class TextFileCameraParametersReader : ICameraParametersReader
{
    private const int ExpectedValueCount = 11;

    public CameraParameters Read(string cameraParametersPath)
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
