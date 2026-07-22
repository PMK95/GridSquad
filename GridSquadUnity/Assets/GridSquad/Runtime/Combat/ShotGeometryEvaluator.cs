using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    internal sealed class ShotGeometryEvaluator
    {
        private readonly GridMap gridMap;
        private readonly CombatTuning tuning;
        private readonly FriendlyFireEvaluator friendlyFireEvaluator;

        public ShotGeometryEvaluator(
            GridMap gridMap,
            CombatTuning tuning,
            FriendlyFireEvaluator friendlyFireEvaluator)
        {
            this.gridMap = gridMap;
            this.tuning = tuning;
            this.friendlyFireEvaluator = friendlyFireEvaluator;
        }

        public ShotEvaluation EvaluateShot(
            Combatant shooter,
            ShootableTarget target,
            WeaponDefinition weapon)
        {
            if (shooter == null)
                return default;
            GridCoordinate originCell = shooter.CurrentCell;
            if (weapon == null || target == null)
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

            ShotEvaluation direct = EvaluateFromCells(
                shooter,
                target,
                originCell,
                target.CurrentCell,
                target.CurrentExposureCell,
                target.CurrentExposureCenter,
                false,
                true,
                weapon);
            if (!shooter.PeekEnabled)
                return direct;
            if (!TryFindBestPeekShot(shooter, target, originCell, weapon, out ShotEvaluation peek))
                return direct;
            return peek.CanShoot && (!direct.CanShoot || IsBetterShot(peek, direct))
                ? peek
                : direct;
        }

        public ShotEvaluation EvaluateShotFromCell(
            Combatant shooter,
            ShootableTarget target,
            GridCoordinate shooterCell,
            bool allowPeek,
            WeaponDefinition weapon)
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
            ShotEvaluation direct = EvaluateFromCells(
                shooter,
                target,
                shooterCell,
                target.CurrentCell,
                target.CurrentExposureCell,
                target.CurrentExposureCenter,
                false,
                false,
                weapon);
            if (!allowPeek || target.IsCover)
                return direct;
            if (TryFindBestPeekShot(shooter, target, shooterCell, weapon, out ShotEvaluation peek)
                && peek.CanShoot
                && (!direct.CanShoot || IsBetterShot(peek, direct)))
            {
                return peek;
            }
            return direct;
        }

        public ShotEvaluation EvaluateShotAtCell(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate shotOriginCell)
        {
            Vector3 targetCenter = gridMap.GridToWorld(targetCell) + Vector3.up * 1.1f;
            return EvaluateFromCells(
                shooter,
                null,
                shotOriginCell,
                targetCell,
                targetCell,
                targetCenter,
                false,
                false,
                shooter.Weapon);
        }

        public CoverEvaluation EvaluateIncomingCover(
            Combatant attacker,
            GridCoordinate targetCell)
        {
            if (attacker == null || !attacker.IsAlive)
                return CoverEvaluation.None;
            return EvaluateCoverFromCells(attacker.CurrentIndicatorShotOriginCell, targetCell);
        }

        public bool IsCellInsideShootingView(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate shotOriginCell)
        {
            return GetShootingRangeFailure(shooter.Weapon, targetCell, shotOriginCell)
                == ShotFailureReason.None;
        }

        private bool TryFindBestPeekShot(
            Combatant shooter,
            ShootableTarget target,
            GridCoordinate shooterCell,
            WeaponDefinition weapon,
            out ShotEvaluation best)
        {
            best = default;
            bool found = false;
            HashSet<GridCoordinate> evaluatedOrigins = new();
            GridCoordinate targetExposureCell = target.CurrentExposureCell;
            foreach (GridCoordinate coverCell in gridMap.GetAdjacentBlockedCells(shooterCell))
            {
                if (!gridMap.IsBlockedCellOnLineBetween(shooterCell, targetExposureCell, coverCell))
                    continue;
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
                        target,
                        candidateCell,
                        target.CurrentCell,
                        targetExposureCell,
                        target.CurrentExposureCenter,
                        true,
                        false,
                        weapon);
                    if (!found
                        || candidate.CanShoot && !best.CanShoot
                        || candidate.CanShoot == best.CanShoot && IsBetterShot(candidate, best))
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
            ShootableTarget target,
            GridCoordinate originCell,
            GridCoordinate targetPhysicalCell,
            GridCoordinate targetExposureCell,
            Vector3 targetWorldCenter,
            bool usesPeekPosition,
            bool useLiveMuzzle,
            WeaponDefinition weapon)
        {
            Vector3 shotOrigin = useLiveMuzzle && !usesPeekPosition
                ? shooter.MuzzlePosition
                : gridMap.GridToWorld(originCell) + Vector3.up * shooter.MuzzleHeight;
            ShotFailureReason rangeFailure = GetShootingRangeFailure(
                weapon,
                targetExposureCell,
                originCell);
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
            CoverEvaluation cover = target != null && target.IsCover
                ? CoverEvaluation.None
                : EvaluateCoverFromCells(originCell, targetPhysicalCell);
            float hitChance = Mathf.Clamp(
                weapon.BaseHitChancePercent
                    + (shooter != null ? shooter.HitChanceBonusPercent : 0f)
                    - cover.EvasionPercent,
                tuning.MinimumHitChancePercent,
                tuning.MaximumHitChancePercent);
            float friendlyFireRisk = target != null
                ? friendlyFireEvaluator.CalculateFriendlyRiskPercent(
                    shooter,
                    target,
                    shotOrigin,
                    targetWorldCenter)
                : 0f;
            return new ShotEvaluation
            {
                CanShoot = true,
                UsesPeekPosition = usesPeekPosition,
                ShotOriginCell = originCell,
                TargetExposureCell = targetExposureCell,
                CoverAngleDegrees = cover.AngleDegrees,
                CoverEvasionPercent = cover.EvasionPercent,
                HitChancePercent = hitChance,
                FriendlyFireRiskPercent = friendlyFireRisk,
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
                minimumAngle = Mathf.Min(
                    minimumAngle,
                    Vector2.Angle(attackDirection, coverDirection.normalized));
            }
            if (float.IsPositiveInfinity(minimumAngle))
                return CoverEvaluation.None;
            float coverRatio = Mathf.Clamp01(1f - minimumAngle / 90f);
            return new CoverEvaluation(
                minimumAngle,
                coverRatio * tuning.MaximumCoverEvasionPercent);
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

        private static ShotFailureReason GetShootingRangeFailure(
            WeaponDefinition weapon,
            GridCoordinate targetCell,
            GridCoordinate originCell)
        {
            if (weapon == null)
                return ShotFailureReason.OutOfRange;
            Vector2 difference = new(targetCell.X - originCell.X, targetCell.Z - originCell.Z);
            return difference.magnitude > weapon.RangeInCells
                ? ShotFailureReason.OutOfRange
                : ShotFailureReason.None;
        }

        private static bool IsBetterShot(ShotEvaluation candidate, ShotEvaluation current)
        {
            int riskComparison = candidate.FriendlyFireRiskPercent.CompareTo(
                current.FriendlyFireRiskPercent);
            return riskComparison != 0
                ? riskComparison < 0
                : candidate.HitChancePercent > current.HitChancePercent;
        }
    }
}
