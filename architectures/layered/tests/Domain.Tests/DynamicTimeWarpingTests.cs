using LayeredArchitecture.Domain.Trajectory;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class DynamicTimeWarpingTests
{
    [Fact]
    public void Distance_ForIdenticalSeries_IsZero()
    {
        List<TrajectoryPoint> series = [new(0, 0), new(1, 1), new(2, 2)];

        Assert.Equal(0, DynamicTimeWarping.Distance(series, series));
    }

    [Fact]
    public void Distance_ForSinglePoints_IsTheEuclideanDistance()
    {
        Assert.Equal(5, DynamicTimeWarping.Distance([new(0, 0)], [new(3, 4)]));
    }

    [Fact]
    public void Distance_IsSymmetric()
    {
        List<TrajectoryPoint> first = [new(0, 0), new(2, 0), new(4, 0)];
        List<TrajectoryPoint> second = [new(0, 1), new(3, 1)];

        Assert.Equal(
            DynamicTimeWarping.Distance(first, second),
            DynamicTimeWarping.Distance(second, first),
            9);
    }

    [Fact]
    public void Distance_AlignsSeriesOfDifferentLengthsByRepeatingPoints()
    {
        // The lone point has to align with all three, so the cost is three times its offset.
        List<TrajectoryPoint> longer = [new(0, 0), new(0, 0), new(0, 0)];
        List<TrajectoryPoint> shorter = [new(0, 2)];

        Assert.Equal(6, DynamicTimeWarping.Distance(longer, shorter), 9);
    }

    [Fact]
    public void Distance_ToleratesTimeShiftsBetterThanPointwiseComparison()
    {
        // The same path, sampled with a pause in the middle: DTW should see them as equal.
        List<TrajectoryPoint> steady = [new(0, 0), new(1, 0), new(2, 0), new(3, 0)];
        List<TrajectoryPoint> paused = [new(0, 0), new(1, 0), new(1, 0), new(2, 0), new(3, 0)];

        Assert.Equal(0, DynamicTimeWarping.Distance(steady, paused), 9);
    }
}
