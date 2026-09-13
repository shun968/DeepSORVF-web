namespace LayeredArchitecture.Domain.Trajectory;

// One point of a trajectory in image pixel coordinates — the (x, y) columns the Python
// original slices out of AIS_vis and Vis_tra before comparing trajectories.
public readonly struct TrajectoryPoint
{
    public double X { get; }
    public double Y { get; }

    public TrajectoryPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}
