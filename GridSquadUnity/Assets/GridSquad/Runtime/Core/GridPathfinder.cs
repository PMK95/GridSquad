using System.Collections.Generic;

namespace GridSquad
{
    public static class GridPathfinder
    {
        public static List<GridCoordinate> FindPath(GridMap grid, GridCoordinate start, GridCoordinate goal, Combatant requester)
        {
            if (start == goal)
                return new List<GridCoordinate>();
            if (!grid.IsWalkable(goal, requester))
                return null;

            List<GridCoordinate> open = new() { start };
            HashSet<GridCoordinate> closed = new();
            Dictionary<GridCoordinate, GridCoordinate> previous = new();
            Dictionary<GridCoordinate, int> cost = new() { [start] = 0 };

            while (open.Count > 0)
            {
                GridCoordinate current = FindLowestEstimatedCost(open, cost, goal);
                if (current == goal)
                    return BuildPath(previous, start, goal);

                open.Remove(current);
                closed.Add(current);

                foreach (GridCoordinate neighbor in grid.GetCardinalNeighbors(current))
                {
                    if (closed.Contains(neighbor) || !grid.IsWalkable(neighbor, requester))
                        continue;

                    int newCost = cost[current] + 1;
                    if (!cost.TryGetValue(neighbor, out int oldCost) || newCost < oldCost)
                    {
                        cost[neighbor] = newCost;
                        previous[neighbor] = current;
                        if (!open.Contains(neighbor))
                            open.Add(neighbor);
                    }
                }
            }

            return null;
        }

        private static GridCoordinate FindLowestEstimatedCost(
            List<GridCoordinate> open,
            Dictionary<GridCoordinate, int> cost,
            GridCoordinate goal)
        {
            GridCoordinate best = open[0];
            int bestScore = cost[best] + best.ManhattanDistance(goal);
            for (int index = 1; index < open.Count; index++)
            {
                GridCoordinate candidate = open[index];
                int score = cost[candidate] + candidate.ManhattanDistance(goal);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private static List<GridCoordinate> BuildPath(
            Dictionary<GridCoordinate, GridCoordinate> previous,
            GridCoordinate start,
            GridCoordinate goal)
        {
            List<GridCoordinate> path = new();
            GridCoordinate current = goal;
            while (current != start)
            {
                path.Add(current);
                current = previous[current];
            }
            path.Reverse();
            return path;
        }
    }
}
