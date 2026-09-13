using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using LayeredArchitecture.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LayeredArchitecture.Web.IntegrationTests;

public class VesselTrackingEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _directory = Directory.CreateTempSubdirectory("web-integration-").FullName;
    private readonly string _aisDirectory;
    private readonly string _cameraPath;

    public VesselTrackingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _aisDirectory = Path.Combine(_directory, "ais");
        Directory.CreateDirectory(_aisDirectory);
        _cameraPath = Path.Combine(_directory, "camera.txt");
        File.WriteAllText(_cameraPath, "[121.5,29.87,90,5,20,55,35,1500,1500,960,540]\n");
    }

    private VesselTrackingRunRequest Request(int frameCount = 1, int frameIntervalSeconds = 1) => new()
    {
        AisDataDirectory = _aisDirectory,
        CameraParametersPath = _cameraPath,
        StartTime = StartTime,
        FrameCount = frameCount,
        FrameIntervalSeconds = frameIntervalSeconds,
    };

    [Fact]
    public async Task CreateRun_ProjectsAisFromDiskAndBindsItToATrack()
    {
        // 800m due east of the camera, closing on it at 8kt.
        WriteAisCsv(StartTime, ["431234567,121.508287,29.870000,8.0,270,270,70,1609502400000"]);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", Request());

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VesselTrackingRunResponse>();
        Assert.NotNull(body);
        var frame = Assert.Single(body!.Frames);
        var ais = Assert.Single(frame.AisRecords);
        Assert.Equal(431234567, ais.Mmsi);
        Assert.InRange(ais.X, 958, 962);
        Assert.True(ais.Y > 540, $"a vessel below the horizon should project below the principal point, got {ais.Y}");
        Assert.Equal(431234567, frame.FusedTracks[0].Mmsi);
        Assert.Null(frame.FusedTracks[1].Mmsi);
    }

    [Fact]
    public async Task CreateRun_CarriesTheVesselForwardAcrossFramesWithoutFurtherMessages()
    {
        WriteAisCsv(StartTime, ["431234567,121.508287,29.870000,8.0,270,270,70,1609502400000"]);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vessel-tracking/runs",
            Request(frameCount: 3, frameIntervalSeconds: 60));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VesselTrackingRunResponse>();
        var ys = body!.Frames.Select(frame => Assert.Single(frame.AisRecords).Y).ToList();
        Assert.True(ys[0] < ys[1] && ys[1] < ys[2], $"expected the vessel to descend the frame, got {string.Join(", ", ys)}");
    }

    [Fact]
    public async Task CreateRun_WithMissingAisDirectory_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = Request() with { AisDataDirectory = Path.Combine(_directory, "does-not-exist") };

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRun_WithMissingCameraParameters_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = Request() with { CameraParametersPath = Path.Combine(_directory, "missing.txt") };

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRun_WithNonPositiveFrameCount_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", Request(frameCount: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRun_WithNonPositiveFrameInterval_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", Request(frameIntervalSeconds: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private void WriteAisCsv(DateTimeOffset timestamp, string[] rows)
    {
        var fileName = timestamp.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + ".csv";
        var lines = new List<string> { "mmsi,lon,lat,speed,course,heading,type,timestamp" };
        lines.AddRange(rows);
        File.WriteAllLines(Path.Combine(_aisDirectory, fileName), lines);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
