using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class ShotEvaluator : MonoBehaviour
    {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private CombatTuning tuning;

        public ShotEvaluation EvaluateShot(Combatant shooter, Combatant target)
        {
            GridCoordinate originCell = shooter.CurrentCell;
            if (target == null)
            {
                return CreateCannotShoot(
                    ShotFailureReason.NoTarget,
                    shooter,
                    originCell,
                    originCell,
                    shooter.transform.position,
                    true);
            }
            if (!target.IsAlive)
            {
                return CreateCannotShoot(
                    ShotFailureReason.TargetDead,
                    shooter,
                    originCell,
                    target.CurrentExposureCell,
                    target.CurrentExposureCenter,
                    true);
            }

            GridCoordinate targetExposureCell = target.CurrentExposureCell;
            ShotEvaluation direct = EvaluateFromCells(
                shooter,
                originCell,
                target.CurrentCell,
                targetExposureCell,
                target.CurrentExposureCenter,
                false,
                true);
            if (!shooter.PeekEnabled)
                return direct;

            if (!TryFindBestPeekShot(shooter, target, originCell, out ShotEvaluation peek))
                return direct;

            if (peek.CanShoot && (!direct.CanShoot || peek.HitChancePercent > direct.HitChancePercent))
                return peek;
            return direct;
        }

        public ShotEvaluation EvaluateShotFromCell(
            Combatant shooter,
            Combatant target,
            GridCoordinate shooterCell,
            bool allowPeek)
        {
            if (target == null || !target.IsAlive)
            {
                return CreateCannotShoot(
                    ShotFailureReason.NoTarget,
                    shooter,
                    shooterCell,
                    shooterCell,
                    gridMap.GridToWorld(shooterCell),
                    false);
            }

            GridCoordinate targetExposureCell = target.CurrentExposureCell;
            ShotEvaluation direct = EvaluateFromCells(
                shooter,
                shooterCell,
                target.CurrentCell,
                targetExposureCell,
                target.CurrentExposureCenter,
                false,
                false);
            if (!allowPeek)
                return direct;

            if (TryFindBestPeekShot(shooter, target, shooterCell, out ShotEvaluation peek)
                && peek.CanShoot
                && (!direct.CanShoot || peek.HitChancePercent > direct.HitChancePercent))
            {
                return peek;
            }
            return direct;
        }

        public CoverEvaluation EvaluateIncomingCover(Combatant attacker, GridCoordinate targetCell)
        {
            if (attacker == null || !attacker.IsAlive)
                return CoverEvaluation.None;
            return EvaluateCoverFromCells(attacker.CurrentIndicatorShotOriginCell, targetCell);
        }

        public float EvaluateIncomingCoverAtCell(Combatant attacker, GridCoordinate targetCell)
            => EvaluateIncomingCover(attacker, targetCell).EvasionPercent;

        public bool IsCellInsideShootingView(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate shotOriginCell)
            => GetShootingRangeFailure(shooter, targetCell, shotOriginCell) == ShotFailureReason.None;

        public ShotEvaluation EvaluateShotAtCell(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate shotOriginCell)
        {
            Vector3 targetCenter = gridMap.GridToWorld(targetCell) + Vector3.up * 1.1f;
            return EvaluateFromCells(
                shooter,
                shotOriginCell,
                targetCell,
                targetCell,
                targetCenter,
                false,
                false);
        }

        private bool TryFindBestPeekShot(
            Combatant shooter,
            Combatant target,
            GridCoordinate shooterCell,
            out ShotEvaluation best)
        {
            best = default;
            bool found = false;
            HashSet<GridCoordinate> evaluatedOrigins = new();
            GridCoordinate targetExposureCell = target.CurrentExposureCell;
            foreach (GridCoordinate coverCell in gridMap.GetAdjacentBlockedCells(shooterCell))
            {
                if (!gridMap.IsBlockedCellOnLineBetween(
                        shooterCell,
                        targetExposureCell,
                        coverCell))
                {
                    continue;
                }

                GridCoordinate coverDirection = new(
                    coverCell.X - shooterCell.X,
                    coverCell.Z - shooterCell.Z);
                GridCoordinate[] sideDirections =
                {
                    new(-coverDirection.Z, coverDirection.X),
                    new(coverDirection.Z, -coverDirection.X)
                };

                foreach (GridCoordinate sideDirection in sideDirections)
                {
                    GridCoordinate candidateCell = shooterCell + sideDirection;
                    if (!evaluatedOrigins.Add(candidateCell)
                        || !gridMap.IsAvailablePeekCell(candidateCell, shooter))
                    {
                        continue;
                    }

                    ShotEvaluation candidate = EvaluateFromCells(
                        shooter,
                        candidateCell,
                        target.CurrentCell,
                        targetExposureCell,
                        target.CurrentExposureCenter,
                        true,
                        false);
                    if (!found
                        || candidate.CanShoot && !best.CanShoot
                        || candidate.CanShoot == best.CanShoot
                            && candidate.HitChancePercent > best.HitChancePercent)
                    {
                        found = true;
                        best = candidate;
                    }
                }
            }
            return found;
        }

        private ShotEvaluation EvaluateFromCells(
            Combatant shooter,
            GridCoordinate originCell,
            GridCoordinate targetPhysicalCell,
            GridCoordinate targetExposureCell,
            Vector3 targetWorldCenter,
            bool usesPeekPosition,
            bool useLiveMuzzle)
        {
            Vector3 shotOrigin = useLiveMuzzle && !usesPeekPosition
                ? shooter.MuzzlePosition
                : gridMap.GridToWorld(originCell) + Vector3.up * shooter.MuzzleHeight;
            ShotFailureReason rangeFailure = GetShootingRangeFailure(shooter, targetExposureCell, originCell);
            if (rangeFailure != ShotFailureReason.None)
            {
                return ShotEvaluation.CannotShoot(
                    rangeFailure,
                    originCell,
                    targetExposureCell,
                    shotOrigin,
                    targetWorldCenter);
            }

            if (!gridMap.HasClearCellLine(originCell, targetExposureCell))
            {
                return ShotEvaluation.CannotShoot(
                    ShotFailureReason.FullyBlocked,
                    originCell,
                    targetExposureCell,
                    shotOrigin,
                    targetWorldCenter);
            }

            CoverEvaluation cover = EvaluateCoverFromCells(originCell, targetPhysicalCell);
            float hitChance = Mathf.Clamp(
                shooter.Weapon.BaseHitChancePercent - cover.EvasionPercent,
                tuning.MinimumHitChancePercent,
                tuning.MaximumHitChancePercent);
            return new ShotEvaluation
            {
                CanShoot = true,
                UsesPeekPosition = usesPeekPosition,
                ShotOriginCell = originCell,
                TargetExposureCell = targetExposureCell,
                CoverAngleDegrees = cover.AngleDegrees,
                CoverEvasionPercent = cover.EvasionPercent,
                HitChancePercent = hitChance,
                ShotOrigin = shotOrigin,
                TargetCenter = targetWorldCenter,
                FailureReason = ShotFailureReason.None
            };
        }

        private CoverEvaluation EvaluateCoverFromCells(
            GridCoordinate attackerOriginCell,
            GridCoordinate targetPhysicalCell)
        {
            Vector2 attackDirection = new(
                attackerOriginCell.X - targetPhysicalCell.X,
                attackerOriginCell.Z - targetPhysicalCell.Z);
            if (attackDirection.sqrMagnitude <= 0.0001f)
                return CoverEvaluation.None;
            attackDirection.Normalize();

            float minimumAngle = float.PositiveInfinity;
            foreach (GridCoordinate coverCell in gridMap.GetAdjacentBlockedCells(targetPhysicalCell))
            {
                Vector2 coverDirection = new(
                    coverCell.X - targetPhysicalCell.X,
                    coverCell.Z - targetPhysicalCell.Z);
                float angle = Vector2.Angle(attackDirection, coverDirection.normalized);
                minimumAngle = Mathf.Min(minimumAngle, angle);
            }
            if (float.IsPositiveInfinity(minimumAngle))
                return CoverEvaluation.None;

            float coverRatio = Mathf.Clamp01(1f - minimumAngle / 90f);
            return new CoverEvaluation(
                minimumAngle,
                coverRatio * tuning.MaximumCoverEvasionPercent);
        }

        private ShotFailureReason GetShootingRangeFailure(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate originCell)
        {
            Vector2 cellDifference = new(targetCell.X - originCell.X, targetCell.Z - originCell.Z);
            return cellDifference.magnitude > shooter.Weapon.RangeInCells
                ? ShotFailureReason.OutOfRange
                : ShotFailureReason.None;
        }

        private ShotEvaluation CreateCannotShoot(
            ShotFailureReason reason,
            Combatant shooter,
            GridCoordinate originCell,
            GridCoordinate targetExposureCell,
            Vector3 targetCenter,
            bool useLiveMuzzle)
        {
            Vector3 origin = useLiveMuzzle
                ? shooter.MuzzlePosition
                : gridMap.GridToWorld(originCell) + Vector3.up * shooter.MuzzleHeight;
            return ShotEvaluation.CannotShoot(
                reason,
                originCell,
                targetExposureCell,
                origin,
                targetCenter);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(GridMap newGridMap, CombatTuning newTuning)
        {
            gridMap = newGridMap;
            tuning = newTuning;
        }
#endif
    }
}
