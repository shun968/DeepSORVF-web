using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class MotResultRowTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var row = new MotResultRow(3, 431234567, 930, 680, 60, 40);

        Assert.Equal(3, row.Frame);
        Assert.Equal(431234567, row.Id);
        Assert.Equal(930, row.X);
        Assert.Equal(680, row.Y);
        Assert.Equal(60, row.Width);
        Assert.Equal(40, row.Height);
    }
}
