using System.Collections.Generic;

namespace GridSquad
{
    public static class GridLineTraversal
    {
        public static List<GridCoordinate> GetSupercoverCells(GridCoordinate start, GridCoordinate end)
        {
            List<GridCoordinate> cells = new();
            AddUnique(cells, start);

            int deltaX = end.X - start.X;
            int deltaZ = end.Z - start.Z;
            int stepX = deltaX == 0 ? 0 : deltaX > 0 ? 1 : -1;
            int stepZ = deltaZ == 0 ? 0 : deltaZ > 0 ? 1 : -1;
            int horizontalCount = System.Math.Abs(deltaX);
            int verticalCount = System.Math.Abs(deltaZ);
            int horizontalProgress = 0;
            int verticalProgress = 0;
            int x = start.X;
            int z = start.Z;

            while (horizontalProgress < horizontalCount || verticalProgress < verticalCount)
            {
                long horizontalDecision = (1L + 2L * horizontalProgress) * verticalCount;
                long verticalDecision = (1L + 2L * verticalProgress) * horizontalCount;
                if (horizontalDecision == verticalDecision)
                {
                    GridCoordinate horizontalSide = new(x + stepX, z);
                    GridCoordinate verticalSide = new(x, z + stepZ);
                    AddUnique(cells, horizontalSide);
                    AddUnique(cells, verticalSide);
                    x += stepX;
                    z += stepZ;
                    horizontalProgress++;
                    verticalProgress++;
                }
                else if (horizontalDecision < verticalDecision)
                {
                    x += stepX;
                    horizontalProgress++;
                }
                else
                {
                    z += stepZ;
                    verticalProgress++;
                }

                AddUnique(cells, new GridCoordinate(x, z));
            }

            return cells;
        }

        private static void AddUnique(List<GridCoordinate> cells, GridCoordinate cell)
        {
            if (cells.Count == 0 || cells[^1] != cell)
            {
                if (!cells.Contains(cell))
                    cells.Add(cell);
            }
        }
    }
}
