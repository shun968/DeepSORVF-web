using LayeredArchitecture.Domain.Trajectory;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class TrajectorySimilarityTests
{
    private static IReadOnlyList<TrajectoryPoint> Line(int count, double startX, double stepX, double y) =>
        Enumerable.Range(0, count).Select(i => new TrajectoryPoint(startX + (i * stepX), y)).ToList();

    [Fact]
    public void AngleBetween_ForTrajectoriesHeadingTheSameWay_IsZero()
    {
        Assert.Equal(0, TrajectorySimilarity.AngleBetween(Line(4, 0, 10, 0), Line(4, 0, 10, 50)), 9);
    }

    [Fact]
    public void AngleBetween_ForOpposedTrajectories_IsPi()
    {
        var eastward = Line(4, 0, 10, 0);
        var westward = Line(4, 100, -10, 50);

        Assert.Equal(Math.PI, TrajectorySimilarity.AngleBetween(eastward, westward), 9);
    }

    [Fact]
    public void AngleBetween_ForPerpendicularTrajectories_IsAQuarterTurn()
    {
        var eastward = Line(4, 0, 10, 0);
        var northward = new List<TrajectoryPoint> { new(0, 0), new(0, 10), new(0, 20) };

        Assert.Equal(Math.PI / 2, TrajectorySimilarity.AngleBetween(eastward, northward), 9);
    }

    [Fact]
    public void AngleBetween_WhenTheAnglesStraddleZero_TakesTheShorterTurn()
    {
        // Headings of -135° and +135° are 90° apart the short way round, not 270°.
        var southWest = new List<TrajectoryPoint> { new(0, 0), new(-10, -10) };
        var northWest = new List<TrajectoryPoint> { new(0, 0), new(-10, 10) };

        Assert.Equal(Math.PI / 2, TrajectorySimilarity.AngleBetween(southWest, northWest), 9);
    }

    [Fact]
    public void AngleBetween_WhenTheAnglesStraddleZeroButStayWithinHalfATurn_TakesTheirSum()
    {
        // Headings of -45° and +45° are already 90° apart, so there is nothing to wrap.
        var southEast = new List<TrajectoryPoint> { new(0, 0), new(10, -10) };
        var northEast = new List<TrajectoryPoint> { new(0, 0), new(10, 10) };

        Assert.Equal(Math.PI / 2, TrajectorySimilarity.AngleBetween(southEast, northEast), 9);
    }

    [Fact]
    public void AngleBetween_WithALongFirstTrajectory_UsesOnlyItsRecentHeading()
    {
        // The first ten points go east, then it turns north; only the northward leg counts.
        var turning = Line(10, 0, 10, 0).Concat(
            Enumerable.Range(1, 10).Select(i => new TrajectoryPoint(90, i * 10))).ToList();
        var northward = new List<TrajectoryPoint> { new(0, 0), new(0, 10) };

        Assert.Equal(0, TrajectorySimilarity.AngleBetween(turning, northward), 9);
    }

    [Fact]
    public void ReduceByHalf_AveragesAdjacentPairs()
    {
        var reduced = TrajectorySimilarity.ReduceByHalf([new(0, 0), new(10, 20), new(30, 30), new(50, 70)]);

        Assert.Equal(2, reduced.Count);
        Assert.Equal(5, reduced[0].X);
        Assert.Equal(10, reduced[0].Y);
        Assert.Equal(40, reduced[1].X);
        Assert.Equal(50, reduced[1].Y);
    }

    [Fact]
    public void ReduceByHalf_DropsATrailingOddPoint()
    {
        var reduced = TrajectorySimilarity.ReduceByHalf([new(0, 0), new(10, 0), new(99, 99)]);

        Assert.Single(reduced);
        Assert.Equal(5, reduced[0].X);
    }

    [Fact]
    public void Distance_ForIdenticalTrajectories_IsZero()
    {
        var trajectory = Line(6, 0, 10, 0);

        Assert.Equal(0, TrajectorySimilarity.Distance(trajectory, trajectory), 9);
    }

    [Fact]
    public void Distance_GrowsWithSeparation()
    {
        var reference = Line(6, 0, 10, 0);
        var near = Line(6, 0, 10, 5);
        var far = Line(6, 0, 10, 100);

        Assert.True(
            TrajectorySimilarity.Distance(reference, near) < TrajectorySimilarity.Distance(reference, far),
            "a trajectory further away should score as less similar");
    }

    [Fact]
    public void Distance_PenalisesTrajectoriesRunningInOppositeDirections()
    {
        // Same points, so the raw DTW distance is identical; only the heading differs.
        var eastward = Line(6, 0, 10, 0);
        var westward = Line(6, 50, -10, 0);
        var alsoEastward = Line(6, 0, 10, 0);

        Assert.True(
            TrajectorySimilarity.Distance(eastward, westward) > TrajectorySimilarity.Distance(eastward, alsoEastward),
            "an opposed trajectory should be pushed further away than a matching one");
    }

    [Fact]
    public void Distance_WithSinglePointTrajectories_FallsBackToPlainDistance()
    {
        var distance = TrajectorySimilarity.Distance([new(0, 0)], [new(3, 4)]);

        Assert.Equal(5, distance, 9);
    }
}
