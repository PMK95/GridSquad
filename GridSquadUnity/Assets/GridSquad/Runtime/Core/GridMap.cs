using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class GridMap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 12;
        [SerializeField, Min(1)] private int height = 12;
        [SerializeField, Min(0.1f)] private float cellSize = 2f;
        [SerializeField] private Vector3 origin;
        [SerializeField] private Vector2Int[] blockedCells = Array.Empty<Vector2Int>();
        [SerializeField, HideInInspector, Min(0)] private int editorRandomBlockedCellCount = 14;
        [SerializeField, HideInInspector] private int editorRandomSeed = 12345;

        private readonly HashSet<GridCoordinate> blocked = new();
        private readonly Dictionary<GridCoordinate, Combatant> occupants = new();
        private readonly Dictionary<GridCoordinate, Combatant> reservations = new();

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;

        private static readonly GridCoordinate[] CardinalDirections =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        private void Awake()
        {
            RebuildBlockedCellLookup();
        }

        public void RebuildBlockedCellLookup()
        {
            blocked.Clear();
            foreach (Vector2Int cell in blockedCells)
                blocked.Add(new GridCoordinate(cell.x, cell.y));
        }

        public GridCoordinate WorldToGrid(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - origin;
            return new GridCoordinate(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize));
        }

        public Vector3 GridToWorld(GridCoordinate coordinate)
        {
            return origin + new Vector3(
                (coordinate.X + 0.5f) * cellSize,
                0f,
                (coordinate.Z + 0.5f) * cellSize);
        }

        public bool IsInside(GridCoordinate coordinate)
            => coordinate.X >= 0 && coordinate.X < width && coordinate.Z >= 0 && coordinate.Z < height;

        public bool IsBlocked(GridCoordinate coordinate) => blocked.Contains(coordinate);

        public bool IsWalkable(GridCoordinate coordinate, Combatant requester = null)
        {
            if (!IsInside(coordinate) || IsBlocked(coordinate))
                return false;
            if (occupants.TryGetValue(coordinate, out Combatant occupant) && occupant != requester)
                return false;
            return !reservations.TryGetValue(coordinate, out Combatant owner) || owner == requester;
        }

        public bool IsAvailablePeekCell(GridCoordinate coordinate, Combatant requester)
            => IsWalkable(coordinate, requester);

        public bool HasClearCellLine(GridCoordinate originCell, GridCoordinate targetCell)
        {
            List<GridCoordinate> crossedCells = GridLineTraversal.GetSupercoverCells(originCell, targetCell);
            foreach (GridCoordinate cell in crossedCells)
            {
                if (cell == originCell || cell == targetCell)
                    continue;
                if (!IsInside(cell) || IsBlocked(cell))
                    return false;
            }
            return true;
        }

        public bool IsBlockedCellOnLineBetween(
            GridCoordinate originCell,
            GridCoordinate targetCell,
            GridCoordinate blockedCell)
        {
            if (blockedCell == originCell
                || blockedCell == targetCell
                || !IsBlocked(blockedCell))
            {
                return false;
            }

            // Supercover 선이 스치는 셀도 포함해 실제 사격선을 막는 엄폐물인지 확인한다.
            List<GridCoordinate> crossedCells = GridLineTraversal.GetSupercoverCells(originCell, targetCell);
            return crossedCells.Contains(blockedCell);
        }

        public bool IsCoverPosition(GridCoordinate coordinate)
        {
            if (!IsInside(coordinate) || IsBlocked(coordinate))
                return false;
            foreach (GridCoordinate neighbor in GetCardinalNeighbors(coordinate))
            {
                if (IsBlocked(neighbor))
                    return true;
            }
            return false;
        }

        public IReadOnlyList<GridCoordinate> GetAdjacentBlockedCells(GridCoordinate coordinate)
        {
            List<GridCoordinate> blockedNeighbors = new(4);
            foreach (GridCoordinate neighbor in GetCardinalNeighbors(coordinate))
            {
                if (IsBlocked(neighbor))
                    blockedNeighbors.Add(neighbor);
            }
            return blockedNeighbors;
        }

        public IReadOnlyList<GridCoordinate> GetCardinalNeighbors(GridCoordinate coordinate)
        {
            List<GridCoordinate> neighbors = new(4);
            foreach (GridCoordinate direction in CardinalDirections)
            {
                GridCoordinate candidate = coordinate + direction;
                if (IsInside(candidate))
                    neighbors.Add(candidate);
            }
            return neighbors;
        }

        public void RegisterOccupant(Combatant combatant, GridCoordinate coordinate)
        {
            occupants[coordinate] = combatant;
        }

        public void MoveOccupant(Combatant combatant, GridCoordinate previous, GridCoordinate next)
        {
            if (occupants.TryGetValue(previous, out Combatant previousOccupant) && previousOccupant == combatant)
                occupants.Remove(previous);
            occupants[next] = combatant;
        }

        public void UnregisterOccupant(Combatant combatant, GridCoordinate coordinate)
        {
            if (occupants.TryGetValue(coordinate, out Combatant occupant) && occupant == combatant)
                occupants.Remove(coordinate);
            ReleaseReservation(combatant);
        }

        public bool TryReserveCell(Combatant combatant, GridCoordinate coordinate)
        {
            if (!IsWalkable(coordinate, combatant))
                return false;
            ReleaseReservation(combatant);
            reservations[coordinate] = combatant;
            return true;
        }

        public void ReleaseReservation(Combatant combatant)
        {
            GridCoordinate? target = null;
            foreach (KeyValuePair<GridCoordinate, Combatant> pair in reservations)
            {
                if (pair.Value == combatant)
                {
                    target = pair.Key;
                    break;
                }
            }
            if (target.HasValue)
                reservations.Remove(target.Value);
        }

        public bool TryGetOccupant(GridCoordinate coordinate, out Combatant combatant)
            => occupants.TryGetValue(coordinate, out combatant);

#if UNITY_EDITOR
        public void SetEditorConfiguration(int newWidth, int newHeight, float newCellSize, Vector3 newOrigin, Vector2Int[] newBlockedCells)
        {
            width = newWidth;
            height = newHeight;
            cellSize = newCellSize;
            origin = newOrigin;
            blockedCells = newBlockedCells ?? Array.Empty<Vector2Int>();
            RebuildBlockedCellLookup();
        }
#endif
    }
}
