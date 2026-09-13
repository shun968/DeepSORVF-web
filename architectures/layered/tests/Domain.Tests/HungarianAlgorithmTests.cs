using LayeredArchitecture.Domain.Trajectory;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class HungarianAlgorithmTests
{
    [Fact]
    public void Solve_PicksTheCheapestAssignmentEvenWhenItIsNotEachRowsOwnBest()
    {
        // Row 0 prefers column 0, but giving it to row 1 costs far less overall.
        var costs = new double[,]
        {
            { 1, 2 },
            { 1, 90 },
        };

        Assert.Equal([1, 0], HungarianAlgorithm.Solve(costs));
    }

    [Fact]
    public void Solve_WithASingleCell_AssignsIt()
    {
        Assert.Equal([0], HungarianAlgorithm.Solve(new double[,] { { 7 } }));
    }

    [Fact]
    public void Solve_WithMoreColumnsThanRows_AssignsEveryRow()
    {
        var costs = new double[,]
        {
            { 9, 1, 9 },
            { 9, 9, 2 },
        };

        Assert.Equal([1, 2], HungarianAlgorithm.Solve(costs));
    }

    [Fact]
    public void Solve_WithMoreRowsThanColumns_LeavesTheSurplusRowsUnassigned()
    {
        var costs = new double[,]
        {
            { 5, 9 },
            { 9, 5 },
            { 6, 6 },
        };

        var assignment = HungarianAlgorithm.Solve(costs);

        Assert.Equal(0, assignment[0]);
        Assert.Equal(1, assignment[1]);
        Assert.Equal(-1, assignment[2]);
    }

    [Fact]
    public void Solve_WithNegativeCosts_PrefersThem()
    {
        // Bound pairs are scored far below zero so the assignment keeps them together.
        var costs = new double[,]
        {
            { -300, 1 },
            { 1, 1 },
        };

        Assert.Equal([0, 1], HungarianAlgorithm.Solve(costs));
    }

    [Fact]
    public void Solve_MinimisesTheTotalCostOnALargerMatrix()
    {
        var costs = new double[,]
        {
            { 4, 1, 3 },
            { 2, 0, 5 },
            { 3, 2, 2 },
        };

        var assignment = HungarianAlgorithm.Solve(costs);

        var total = 0.0;
        for (var row = 0; row < 3; row++)
        {
            total += costs[row, assignment[row]];
        }

        Assert.Equal(5, total);
        Assert.Equal(3, assignment.Distinct().Count());
    }

    [Fact]
    public void Solve_WithNoRows_ReturnsNoAssignments()
    {
        Assert.Empty(HungarianAlgorithm.Solve(new double[0, 0]));
    }

    [Fact]
    public void Solve_WithNoColumns_LeavesEveryRowUnassigned()
    {
        Assert.Equal([-1, -1], HungarianAlgorithm.Solve(new double[2, 0]));
    }
}
