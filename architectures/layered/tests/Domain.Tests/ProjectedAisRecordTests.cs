using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class ProjectedAisRecordTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var record = new AisRecord(431234567, 121.5, 29.87, 5.2, 45, 47, 30, DateTimeOffset.UnixEpoch);

        var projected = new ProjectedAisRecord(record, 960, 700);

        Assert.Same(record, projected.Record);
        Assert.Equal(960, projected.X);
        Assert.Equal(700, projected.Y);
    }
}
