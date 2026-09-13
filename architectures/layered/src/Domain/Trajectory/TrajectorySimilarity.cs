namespace LayeredArchitecture.Domain.Trajectory;

// Ported from utils/FUS_utils.py's DTW_fast and angle: how unlike two trajectories are,
// as a DTW distance amplified by how far apart their directions of travel point.
public static class TrajectorySimilarity
{
    // angle() reads back this far along the first trajectory to work out its heading,
    // rather than using its whole span.
    private const int DirectionWindow = 10;

    public static double Distance(IReadOnlyList<TrajectoryPoint> first, IReadOnlyList<TrajectoryPoint> second)
    {
        var theta = 0.0;
        if (first.Count > 1 && second.Count > 1)
        {
            theta = AngleBetween(first, second);
            first = ReduceByHalf(first);
            second = ReduceByHalf(second);
        }

        // Trajectories heading opposite ways are pushed apart exponentially, so a vessel
        // that merely passes close to another's track does not steal its identity.
        return DynamicTimeWarping.Distance(first, second) * Math.Exp(theta);
    }

    public static double AngleBetween(IReadOnlyList<TrajectoryPoint> first, IReadOnlyList<TrajectoryPoint> second)
    {
        var firstStart = first.Count >= DirectionWindow ? first[^DirectionWindow] : first[0];
        var firstAngle = Math.Atan2(first[^1].Y - firstStart.Y, first[^1].X - firstStart.X);

        // The original branches on len(v2) >= 5 here too, but both arms compute the same
        // full-span difference, so the second trajectory always uses all of its points.
        var secondAngle = Math.Atan2(second[^1].Y - second[0].Y, second[^1].X - second[0].X);

        if (firstAngle * secondAngle >= 0)
        {
            return Math.Abs(firstAngle - secondAngle);
        }

        var included = Math.Abs(firstAngle) + Math.Abs(secondAngle);

        return included > Math.PI ? (Math.PI * 2) - included : included;
    }

    // Halves a trajectory by averaging adjacent pairs, dropping a trailing odd point.
    public static IReadOnlyList<TrajectoryPoint> ReduceByHalf(IReadOnlyList<TrajectoryPoint> trajectory)
    {
        var reduced = new List<TrajectoryPoint>(trajectory.Count / 2);
        for (var i = 0; i + 1 < trajectory.Count; i += 2)
        {
            reduced.Add(new TrajectoryPoint(
                (trajectory[i].X + trajectory[i + 1].X) / 2,
                (trajectory[i].Y + trajectory[i + 1].Y) / 2));
        }

        return reduced;
    }
}
