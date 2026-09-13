using CleanArchitecture.Domain.Geometry;

namespace CleanArchitecture.Domain.Ports;

public interface ICameraParametersReader
{
    CameraParameters Read(string cameraParametersPath);
}
