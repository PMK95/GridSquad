using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalEntity))]
    public sealed class GridMovementController : MonoBehaviour
    {
        private readonly List<GridCoordinate> movementPath = new();

        private Combatant owner;
        private TacticalEntity entity;
        private GridMap gridMap;
        private CombatTuning tuning;
        private int movementIndex;
        private GridCoordinate movementDestination;
        private bool hasMovementDestination;
        private bool hasTransitReservation;
        private bool stopMovementAfterCellArrival;
        private float blockedMovementSeconds;
        private bool registered;

        public bool IsMoving => movementIndex < movementPath.Count;
        public GridCoordinate CurrentCell => entity != null ? entity.CurrentCell : default;

        public void Initialize(
            Combatant newOwner,
            TacticalEntity newEntity,
            GridMap newGridMap,
            CombatTuning newTuning)
        {
            owner = newOwner;
            entity = newEntity;
            gridMap = newGridMap;
            tuning = newTuning;
            if (registered || entity == null || gridMap == null)
                return;

            GridCoordinate initialCell = gridMap.WorldToGrid(transform.position);
            entity.SetCurrentCell(initialCell);
            transform.position = gridMap.GridToWorld(initialCell);
            gridMap.RegisterOccupant(entity, initialCell);
            registered = true;
        }

        private void OnDestroy()
        {
            ReleaseGridRegistration();
        }

        public void RemoveFromStageGrid()
        {
            ClearPathAndReservations();
            ReleaseGridRegistration();
        }

        public bool SetMoveDestination(GridCoordinate destination)
        {
            if (owner == null || !owner.IsAlive || owner.IsReloading || gridMap == null)
                return false;

            if (destination == CurrentCell)
            {
                StopMovementAtCurrentCell();
                return true;
            }

            List<GridCoordinate> path = GridPathfinder.FindPath(
                gridMap,
                CurrentCell,
                destination,
                entity);
            if (path == null || !gridMap.TryReserveCell(entity, destination))
                return false;

            gridMap.ReleaseTransitReservation(entity);
            movementPath.Clear();
            movementPath.AddRange(path);
            movementIndex = 0;
            movementDestination = destination;
            hasMovementDestination = true;
            hasTransitReservation = false;
            stopMovementAfterCellArrival = false;
            blockedMovementSeconds = 0f;
            return true;
        }

        public bool TickMovement(float movementSpeedMultiplier)
        {
            if (owner == null
                || movementIndex >= movementPath.Count
                || owner.IsReloading
                || owner.IsHitReacting)
            {
                return false;
            }

            GridCoordinate nextCell = movementPath[movementIndex];
            if (!hasTransitReservation)
            {
                if (!gridMap.TryReserveTransitCell(entity, nextCell))
                {
                    TryRebuildPathAroundBlockingEntity();
                    return false;
                }
                hasTransitReservation = true;
            }

            Vector3 destination = gridMap.GridToWorld(nextCell);
            float baseMovementSpeed = tuning != null ? tuning.MovementSpeed : 2f;
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                baseMovementSpeed * Mathf.Max(0.01f, movementSpeedMultiplier) * Time.deltaTime);
            if ((transform.position - destination).sqrMagnitude > 0.0001f)
                return true;

            GridCoordinate previous = CurrentCell;
            entity.SetCurrentCell(nextCell);
            gridMap.MoveOccupant(entity, previous, nextCell);
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
            movementIndex++;

            if (stopMovementAfterCellArrival)
            {
                ClearPathAndReservations();
                stopMovementAfterCellArrival = false;
                return true;
            }

            if (movementIndex >= movementPath.Count)
            {
                movementPath.Clear();
                gridMap.ReleaseReservation(entity);
                hasMovementDestination = false;
            }
            return true;
        }

        public Vector3 GetHorizontalLookDirection(ShootableTarget target)
        {
            Vector3 lookDirection = Vector3.zero;
            if (movementIndex < movementPath.Count)
                lookDirection = gridMap.GridToWorld(movementPath[movementIndex]) - transform.position;
            else if (target != null && target.IsAlive)
                lookDirection = target.CurrentExposureCenter - transform.position;
            lookDirection.y = 0f;
            return lookDirection;
        }

        public void AppendRemainingPathWorldPoints(List<Vector3> points)
        {
            points.Add(transform.position + Vector3.up * 0.08f);
            if (gridMap == null)
                return;
            for (int index = movementIndex; index < movementPath.Count; index++)
                points.Add(gridMap.GridToWorld(movementPath[index]) + Vector3.up * 0.08f);
        }

        public void StopMovementAtCurrentCell()
        {
            ClearPathAndReservations();
            stopMovementAfterCellArrival = false;
        }

        public void RequestStopMovementAfterCurrentCell()
        {
            if (!IsMoving)
            {
                StopMovementAtCurrentCell();
                return;
            }
            // 현재 통과 셀 예약은 유지하고 이전 최종 목적지만 즉시 양보합니다.
            gridMap?.ReleaseReservation(entity);
            hasMovementDestination = false;
            stopMovementAfterCellArrival = true;
        }

        public void PrepareForEntityRemoval()
        {
            ClearPathAndReservations();
            ReleaseGridRegistration();
        }

        public void PrepareForBattleResult()
        {
            ClearPathAndReservations();
            stopMovementAfterCellArrival = false;
        }

        public bool TryApplyForcedDisplacement(GridCoordinate sourceCell, int maximumCells)
        {
            if (gridMap == null || entity == null || maximumCells <= 0)
                return false;

            StopMovementAtCurrentCell();
            transform.position = gridMap.GridToWorld(CurrentCell);
            if (!TryCalculateForcedDisplacementDestination(
                    sourceCell,
                    maximumCells,
                    out GridCoordinate destination))
            {
                return false;
            }

            GridCoordinate previous = CurrentCell;
            entity.SetCurrentCell(destination);
            gridMap.MoveOccupant(entity, previous, destination);
            transform.position = gridMap.GridToWorld(destination);
            return true;
        }

        public bool TryCalculateForcedDisplacementDestination(
            GridCoordinate sourceCell,
            int maximumCells,
            out GridCoordinate destination)
        {
            destination = CurrentCell;
            if (gridMap == null || entity == null || maximumCells <= 0)
                return false;

            int xDelta = CurrentCell.X - sourceCell.X;
            int zDelta = CurrentCell.Z - sourceCell.Z;
            int xStep = Mathf.Abs(xDelta) >= Mathf.Abs(zDelta) ? System.Math.Sign(xDelta) : 0;
            int zStep = xStep == 0 ? System.Math.Sign(zDelta) : 0;
            if (xStep == 0 && zStep == 0)
                return false;

            for (int step = 0; step < maximumCells; step++)
            {
                GridCoordinate candidate = new(destination.X + xStep, destination.Z + zStep);
                if (!gridMap.IsWalkable(candidate, entity))
                    break;
                destination = candidate;
            }
            return destination != CurrentCell;
        }

        private void TryRebuildPathAroundBlockingEntity()
        {
            if (!hasMovementDestination)
                return;

            blockedMovementSeconds += Time.deltaTime;
            float retryDelay = 0.12f + Mathf.Abs(GetInstanceID() % 4) * 0.03f;
            if (blockedMovementSeconds < retryDelay)
                return;

            blockedMovementSeconds = 0f;
            List<GridCoordinate> alternatePath = GridPathfinder.FindPath(
                gridMap,
                CurrentCell,
                movementDestination,
                entity,
                true);
            if (alternatePath == null || alternatePath.Count == 0)
                return;

            gridMap.ReleaseTransitReservation(entity);
            hasTransitReservation = false;
            movementPath.Clear();
            movementPath.AddRange(alternatePath);
            movementIndex = 0;
        }

        private void ClearPathAndReservations()
        {
            movementPath.Clear();
            movementIndex = 0;
            if (gridMap != null && entity != null)
            {
                gridMap.ReleaseReservation(entity);
                gridMap.ReleaseTransitReservation(entity);
            }
            hasMovementDestination = false;
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
        }

        private void ReleaseGridRegistration()
        {
            if (!registered || gridMap == null || entity == null)
                return;
            gridMap.UnregisterOccupant(entity, CurrentCell);
            registered = false;
        }
    }
}
