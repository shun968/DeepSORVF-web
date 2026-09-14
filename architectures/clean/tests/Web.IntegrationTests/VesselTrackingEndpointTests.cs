using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Web.Contracts;
using CleanArchitecture.Web.Video;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CleanArchitecture.Web.IntegrationTests;

public class VesselTrackingEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _directory = Directory.CreateTempSubdirectory("clean-web-").FullName;
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
    public async Task CreateRun_WithoutAVideo_ProjectsAisFromDiskButDetectsNothing()
    {
        // 800m due east of the camera, closing on it at 8kt.
        WriteAisCsv(StartTime, ["431234567,121.508287,29.870000,8.0,270,270,70,1609502400000"]);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", Request());

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VesselTrackingRunResponse>();
        Assert.NotNull(body);
        var frame = Assert.Single(body!.Frames);
        var vessel = Assert.Single(frame.Vessels);
        Assert.Equal(431234567, vessel.Mmsi);
        Assert.InRange(vessel.X, 958, 962);
        Assert.True(vessel.Y > 540, $"a vessel below the horizon should project below the principal point, got {vessel.Y}");
        Assert.Empty(frame.Tracks);
        Assert.Empty(frame.Fusions);
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
        var ys = body!.Frames.Select(frame => Assert.Single(frame.Vessels).Y).ToList();
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

    [Fact]
    public async Task CreateRun_ReportsTheImageSizeTheFramesWereProjectedInto()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", Request());

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VesselTrackingRunResponse>();
        Assert.Equal(1920, body!.ImageWidth);
        Assert.Equal(1080, body.ImageHeight);
    }

    [Fact]
    public async Task Root_ServesTheViewerPage()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("viewer.js", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetRunDefaults_ReturnsTheConfiguredValues()
    {
        var client = ClientWithSettings(new()
        {
            ["RunDefaults:AisDataDirectory"] = _aisDirectory,
            ["RunDefaults:StartTime"] = "2022-06-04T12:05:12+08:00",
            ["RunDefaults:FrameCount"] = "5",
        });

        var defaults = await client.GetFromJsonAsync<RunDefaults>("/api/vessel-tracking/run-defaults");

        Assert.Equal(_aisDirectory, defaults!.AisDataDirectory);
        Assert.Equal("2022-06-04T12:05:12+08:00", defaults.StartTime);
        Assert.Equal(5, defaults.FrameCount);
        Assert.Null(defaults.VideoPath);
    }

    [Fact]
    public async Task GetVideo_StreamsTheConfiguredVideoByRange()
    {
        var videoPath = Path.Combine(_directory, "clip.mp4");
        File.WriteAllBytes(videoPath, [0, 1, 2, 3, 4, 5, 6, 7]);
        var client = ClientWithSettings(new() { ["RunDefaults:VideoPath"] = videoPath });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/vessel-tracking/video");
        request.Headers.Range = new RangeHeaderValue(2, 5);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 2, 3, 4, 5 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetVideo_ServesAnMp4WithItsBrokenStartDelayZeroed()
    {
        var mp4 = Mp4Samples.WithBrokenStartDelay();
        var videoPath = Path.Combine(_directory, "broken.mp4");
        File.WriteAllBytes(videoPath, mp4);
        var client = ClientWithSettings(new() { ["RunDefaults:VideoPath"] = videoPath });

        var served = await client.GetByteArrayAsync("/api/vessel-tracking/video");

        var range = Assert.Single(Mp4EditListRepair.FindImplausibleEmptyEdits(new MemoryStream(mp4)));
        Assert.Equal(mp4.Length, served.Length);
        Assert.All(served.Skip((int)range.Offset).Take(range.Length), value => Assert.Equal(0, value));
        Assert.Equal(mp4.Skip((int)range.Offset + range.Length), served.Skip((int)range.Offset + range.Length));
        Assert.Empty(Mp4EditListRepair.FindImplausibleEmptyEdits(new MemoryStream(served)));
    }

    [Fact]
    public async Task GetVideo_WithAnUnknownExtension_ServesItAsBinary()
    {
        var videoPath = Path.Combine(_directory, "clip.unknown-video");
        File.WriteAllBytes(videoPath, [0, 1, 2]);
        var client = ClientWithSettings(new() { ["RunDefaults:VideoPath"] = videoPath });

        var response = await client.GetAsync("/api/vessel-tracking/video");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetVideo_WithoutAConfiguredVideo_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vessel-tracking/video");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVideo_WhenTheConfiguredVideoIsMissing_ReturnsNotFound()
    {
        var client = ClientWithSettings(new() { ["RunDefaults:VideoPath"] = Path.Combine(_directory, "missing.mp4") });

        var response = await client.GetAsync("/api/vessel-tracking/video");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateRun_WithAVideo_DetectsOnItsPicturesAndBindsTheVessel()
    {
        WriteAisCsv(StartTime, ["431234567,121.508287,29.870000,8.0,270,270,70,1609502400000"]);
        var videoFrameReader = new FakeVideoFrameReader();
        var client = ClientWithVideo(videoFrameReader, new FakeDetector(960, 705));

        var response = await client.PostAsJsonAsync(
            "/api/vessel-tracking/runs", Request() with { VideoStartTime = StartTime.AddSeconds(-11) });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VesselTrackingRunResponse>();
        var frame = Assert.Single(body!.Frames);
        Assert.Equal(431234567, Assert.Single(frame.Fusions).Mmsi);
        // The video started 11 seconds before the run, so the first frame is 11 seconds in.
        Assert.Equal([TimeSpan.FromSeconds(11)], videoFrameReader.Positions);
    }

    [Fact]
    public async Task CreateRun_WithAVideoButNoExportedModel_ReturnsBadRequestSayingHowToExportIt()
    {
        var client = ClientWithVideo(
            new FakeVideoFrameReader(),
            detector: null,
            new() { ["Detection:ModelPath"] = Path.Combine(_directory, "missing.onnx") });

        var response = await client.PostAsJsonAsync("/api/vessel-tracking/runs", Request());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("export-yolox-onnx.py", await response.Content.ReadAsStringAsync());
    }

    // A client whose configured video exists, with the frame reader (and optionally the
    // detector) replaced so no real decoding or model is needed.
    private HttpClient ClientWithVideo(
        IVideoFrameReader videoFrameReader,
        IDetector? detector,
        Dictionary<string, string?>? settings = null)
    {
        var videoPath = Path.Combine(_directory, "clip.mp4");
        File.WriteAllBytes(videoPath, [0]);
        var configuration = new Dictionary<string, string?>(settings ?? []) { ["RunDefaults:VideoPath"] = videoPath };

        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(videoFrameReader);
                    if (detector is not null)
                    {
                        services.AddSingleton(detector);
                    }
                });
            })
            .CreateClient();
    }

    private sealed class FakeVideoFrameReader : IVideoFrameReader
    {
        public List<TimeSpan> Positions { get; } = [];

        public FrameImage? ReadAt(string videoPath, TimeSpan position)
        {
            Positions.Add(position);
            return new FrameImage(2, 2, new byte[2 * 2 * 3]);
        }
    }

    // Reports one vessel-sized box at a fixed spot on every frame that has a picture.
    private sealed class FakeDetector(double centreX, double centreY) : IDetector
    {
        public IReadOnlyList<Detection> Detect(VideoFrame frame) =>
            frame.Image is null ? [] : [new Detection(centreX - 30, centreY - 20, centreX + 30, centreY + 20, frame.Timestamp)];
    }

    private HttpClient ClientWithSettings(Dictionary<string, string?> settings) =>
        _factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)))
            .CreateClient();

    private void WriteAisCsv(DateTimeOffset timestamp, string[] rows)
    {
        var fileName = timestamp.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + ".csv";
        var lines = new List<string> { "mmsi,lon,lat,speed,course,heading,type,timestamp" };
        lines.AddRange(rows);
        File.WriteAllLines(Path.Combine(_aisDirectory, fileName), lines);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
