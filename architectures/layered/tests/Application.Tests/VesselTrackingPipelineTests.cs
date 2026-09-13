using LayeredArchitecture.Application.Pipeline;
using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;
using Moq;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

// Exercises the real AisService/DetectionService/TrackingService/FusionService together
// (only IAisRepository, the actual DI boundary, is faked) since none of those services
// expose virtual members or interfaces to mock individually — this verifies the
// orchestration wiring end-to-end rather than the call sequence in isolation.
public class VesselTrackingPipelineTests
{
    private const string AisDirectory = "/ais";

    private static VesselTrackingPipeline CreatePipeline(IAisRepository aisRepository) =>
        new(new AisService(aisRepository), new DetectionService(), new TrackingService(), new FusionService());

    [Fact]
    public void ProcessFrame_CombinesAisAndVisualOutputIntoOneFrameResult()
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        var aisRepositoryMock = new Mock<IAisRepository>();
        aisRepositoryMock
            .Setup(repository => repository.GetRecordsAt(AisDirectory, timestamp))
            .Returns([new AisRecord(431234567, 121.5, 29.87, 5.2, 45, 47, 30, timestamp)]);

        var pipeline = CreatePipeline(aisRepositoryMock.Object);

        var result = pipeline.ProcessFrame(AisDirectory, frameIndex: 0, timestamp);

        Assert.Equal(0, result.FrameIndex);
        Assert.Equal(timestamp, result.Timestamp);
        Assert.Single(result.AisRecords);
        Assert.Equal(2, result.VisualTracks.Count);
        Assert.Equal(2, result.FusedTracks.Count);
        Assert.Equal(431234567, result.FusedTracks[0].MatchedAis!.Mmsi);
        Assert.Null(result.FusedTracks[1].MatchedAis);
    }

    [Fact]
    public void ProcessFrames_ProducesOneResultPerFrameWithAdvancingTimestamps()
    {
        var aisRepositoryMock = new Mock<IAisRepository>();
        aisRepositoryMock
            .Setup(repository => repository.GetRecordsAt(AisDirectory, It.IsAny<DateTimeOffset>()))
            .Returns([]);
        var pipeline = CreatePipeline(aisRepositoryMock.Object);
        var start = DateTimeOffset.UnixEpoch;
        var interval = TimeSpan.FromSeconds(1);

        var results = pipeline.ProcessFrames(AisDirectory, start, frameCount: 3, interval);

        Assert.Equal(3, results.Count);
        Assert.Equal([0, 1, 2], results.Select(frame => frame.FrameIndex));
        Assert.Equal([start, start + interval, start + (interval * 2)], results.Select(frame => frame.Timestamp));
    }
}
