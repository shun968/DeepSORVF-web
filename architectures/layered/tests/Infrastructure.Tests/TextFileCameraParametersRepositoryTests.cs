using LayeredArchitecture.Infrastructure.Repositories;
using Xunit;

namespace LayeredArchitecture.Infrastructure.Tests;

public class TextFileCameraParametersRepositoryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("camera-params-tests-").FullName;
    private readonly TextFileCameraParametersRepository _repository = new();

    [Fact]
    public void Load_ReadsTheElevenValuesInOrder()
    {
        var path = WriteFile("[121.5,29.87,90,5,20,55,35,1500,1501,960,540]\n");

        var parameters = _repository.Load(path);

        Assert.Equal(121.5, parameters.LongitudeDegrees);
        Assert.Equal(29.87, parameters.LatitudeDegrees);
        Assert.Equal(90, parameters.BearingDegrees);
        Assert.Equal(5, parameters.TiltDegrees);
        Assert.Equal(20, parameters.HeightMeters);
        Assert.Equal(55, parameters.HorizontalFovDegrees);
        Assert.Equal(35, parameters.VerticalFovDegrees);
        Assert.Equal(1500, parameters.FocalLengthX);
        Assert.Equal(1501, parameters.FocalLengthY);
        Assert.Equal(960, parameters.PrincipalPointX);
        Assert.Equal(540, parameters.PrincipalPointY);
    }

    [Fact]
    public void Load_WithoutBracketsOrTrailingNewline_ReadsTheSameValues()
    {
        var path = WriteFile("121.5,29.87,90,5,20,55,35,1500,1501,960,540");

        Assert.Equal(540, _repository.Load(path).PrincipalPointY);
    }

    [Fact]
    public void Load_WithTheWrongNumberOfValues_Throws()
    {
        var path = WriteFile("[121.5,29.87,90]\n");

        Assert.Throws<FormatException>(() => _repository.Load(path));
    }

    private string WriteFile(string contents)
    {
        var path = Path.Combine(_directory, "camera.txt");
        File.WriteAllText(path, contents);

        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
