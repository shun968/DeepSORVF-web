using LayeredArchitecture.Domain.Geometry;

namespace LayeredArchitecture.Domain.Repositories;

public interface ICameraParametersRepository
{
    CameraParameters Load(string cameraParametersPath);
}
