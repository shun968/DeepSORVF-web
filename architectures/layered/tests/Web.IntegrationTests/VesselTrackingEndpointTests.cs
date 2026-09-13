using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using LayeredArchitecture.Web.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LayeredArchitecture.Web.IntegrationTests;

public class VesselTrackingEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _aisDirectory = Directory.CreateTempSubdirectory("web-integration-ais-").FullName;

    public VesselTrackingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateRun_WithRealAisFileOnDisk_ReturnsFusedFrames()
    {
        var startTime = new DateTimeOffset(2021, 1, 1, 12, 0, 0, TimeSpan.Zero);
        WriteAisCsv(startTime, ["431234567,121.5,29.87,5.2,45,47,30,1609502400000"]);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vessel-tracking/runs",
            new VesselTrackingRunRequest
            {
                AisDataDirectory = _aisDirectory,
                StartTime = startTime,
                FrameCount = 2,
                FrameIntervalSeconds = 1,
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VesselTrackingRunResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Frames.Count);
        Assert.Single(body.Frames[0].AisRecords);
        Assert.Equal(431234567, body.Frames[0].AisRecords[0].Mmsi);
        Assert.NotEmpty(body.Frames[0].Tracks);
        Assert.Equal(431234567, body.Frames[0].FusedTracks[0].Mmsi);
    }

    [Fact]
    public async Task CreateRun_WithMissingAisDirectory_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vessel-tracking/runs",
            new VesselTrackingRunRequest
            {
                AisDataDirectory = Path.Combine(_aisDirectory, "does-not-exist"),
                StartTime = DateTimeOffset.UnixEpoch,
                FrameCount = 1,
                FrameIntervalSeconds = 1,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRun_WithNonPositiveFrameCount_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vessel-tracking/runs",
            new VesselTrackingRunRequest
            {
                AisDataDirectory = _aisDirectory,
                StartTime = DateTimeOffset.UnixEpoch,
                FrameCount = 0,
                FrameIntervalSeconds = 1,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private void WriteAisCsv(DateTimeOffset timestamp, string[] rows)
    {
        var fileName = timestamp.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + ".csv";
        var lines = new List<string> { "mmsi,lon,lat,speed,course,heading,type,timestamp" };
        lines.AddRange(rows);
        File.WriteAllLines(Path.Combine(_aisDirectory, fileName), lines);
    }

    public void Dispose() => Directory.Delete(_aisDirectory, recursive: true);
}
