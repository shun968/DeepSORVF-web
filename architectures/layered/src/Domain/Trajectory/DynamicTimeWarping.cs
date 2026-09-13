namespace LayeredArchitecture.Domain.Trajectory;

public static class DynamicTimeWarping
{
    // The Python original calls fastdtw, which trades accuracy for speed on long series.
    // The trajectories here are at most a couple of minutes sampled once a second, and are
    // halved again before comparison, so the exact recurrence costs a few thousand cells —
    // cheap enough to prefer over an approximation with a radius parameter to tune.
    public static double Distance(IReadOnlyList<TrajectoryPoint> first, IReadOnlyList<TrajectoryPoint> second)
    {
        var costs = new double[first.Count + 1, second.Count + 1];
        for (var i = 0; i <= first.Count; i++)
        {
            for (var j = 0; j <= second.Count; j++)
            {
                costs[i, j] = double.PositiveInfinity;
            }
        }

        costs[0, 0] = 0;

        for (var i = 1; i <= first.Count; i++)
        {
            for (var j = 1; j <= second.Count; j++)
            {
                var step = EuclideanDistance(first[i - 1], second[j - 1]);
                costs[i, j] = step + Math.Min(costs[i - 1, j], Math.Min(costs[i, j - 1], costs[i - 1, j - 1]));
            }
        }

        return costs[first.Count, second.Count];
    }

    private static double EuclideanDistance(TrajectoryPoint first, TrajectoryPoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
