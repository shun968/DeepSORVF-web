namespace LayeredArchitecture.Domain.Trajectory;

// Stands in for scipy.optimize.linear_sum_assignment, which FUSPRO.traj_match uses to pick
// the globally cheapest set of visual-track/AIS pairings rather than letting each track
// grab its own nearest.
//
// This is the Jonker-Volgenant shortest augmenting path formulation: it keeps a potential
// per row and column and repeatedly extends the assignment along the cheapest augmenting
// path. Costs may be negative, which matters here because already-bound pairs are given a
// large negative cost to keep them together.
public static class HungarianAlgorithm
{
    // Returns, for each row, the column it was assigned to, or -1 when it was left
    // unassigned (which happens when there are more rows than columns).
    public static int[] Solve(double[,] costs)
    {
        var rowCount = costs.GetLength(0);
        var columnCount = costs.GetLength(1);
        var assignment = new int[rowCount];
        Array.Fill(assignment, -1);

        if (rowCount == 0 || columnCount == 0)
        {
            return assignment;
        }

        // The algorithm needs at least as many columns as rows.
        var transposed = rowCount > columnCount;
        var cost = transposed ? Transpose(costs) : costs;
        var rows = cost.GetLength(0);
        var columns = cost.GetLength(1);

        var rowPotential = new double[rows + 1];
        var columnPotential = new double[columns + 1];
        // columnRow[j] is the row currently assigned to column j, 1-based, 0 for none.
        var columnRow = new int[columns + 1];
        var previousColumn = new int[columns + 1];

        for (var row = 1; row <= rows; row++)
        {
            columnRow[0] = row;
            var column = 0;
            var minimalCost = new double[columns + 1];
            Array.Fill(minimalCost, double.PositiveInfinity);
            var visited = new bool[columns + 1];

            do
            {
                visited[column] = true;
                var currentRow = columnRow[column];
                var delta = double.PositiveInfinity;
                var nextColumn = 0;

                for (var candidate = 1; candidate <= columns; candidate++)
                {
                    if (visited[candidate])
                    {
                        continue;
                    }

                    var reducedCost = cost[currentRow - 1, candidate - 1]
                        - rowPotential[currentRow]
                        - columnPotential[candidate];
                    if (reducedCost < minimalCost[candidate])
                    {
                        minimalCost[candidate] = reducedCost;
                        previousColumn[candidate] = column;
                    }

                    if (minimalCost[candidate] < delta)
                    {
                        delta = minimalCost[candidate];
                        nextColumn = candidate;
                    }
                }

                for (var candidate = 0; candidate <= columns; candidate++)
                {
                    if (visited[candidate])
                    {
                        rowPotential[columnRow[candidate]] += delta;
                        columnPotential[candidate] -= delta;
                    }
                    else
                    {
                        minimalCost[candidate] -= delta;
                    }
                }

                column = nextColumn;
            }
            while (columnRow[column] != 0);

            do
            {
                var source = previousColumn[column];
                columnRow[column] = columnRow[source];
                column = source;
            }
            while (column != 0);
        }

        for (var candidate = 1; candidate <= columns; candidate++)
        {
            if (columnRow[candidate] == 0)
            {
                continue;
            }

            if (transposed)
            {
                assignment[candidate - 1] = columnRow[candidate] - 1;
            }
            else
            {
                assignment[columnRow[candidate] - 1] = candidate - 1;
            }
        }

        return assignment;
    }

    private static double[,] Transpose(double[,] costs)
    {
        var transposed = new double[costs.GetLength(1), costs.GetLength(0)];
        for (var row = 0; row < costs.GetLength(0); row++)
        {
            for (var column = 0; column < costs.GetLength(1); column++)
            {
                transposed[column, row] = costs[row, column];
            }
        }

        return transposed;
    }
}
