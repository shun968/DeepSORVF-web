using LayeredArchitecture.Application.Services;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;
using Moq;
using Xunit;

namespace LayeredArchitecture.Application.Tests;

public class AisServiceTests
{
    private static AisRecord Valid(long mmsi) =>
        new(mmsi, 121.5, 29.87, 5.2, 45, 47, 30, DateTimeOffset.UnixEpoch);

    private static AisRecord Invalid(long mmsi) =>
        new(mmsi, 121.5, 29.87, 0.1, 45, 47, 30, DateTimeOffset.UnixEpoch);

    [Fact]
    public void GetValidRecordsAt_FiltersOutInvalidRecords()
    {
        var directory = "/ais";
        var timestamp = DateTimeOffset.UnixEpoch;
        var repositoryMock = new Mock<IAisRepository>();
        repositoryMock
            .Setup(repository => repository.GetRecordsAt(directory, timestamp))
            .Returns([Valid(431234561), Invalid(431234562), Valid(431234563)]);

        var service = new AisService(repositoryMock.Object);

        var result = service.GetValidRecordsAt(directory, timestamp);

        Assert.Equal([431234561L, 431234563L], result.Select(record => record.Mmsi));
    }
}
