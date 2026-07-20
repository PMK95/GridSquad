using UnityEngine;

namespace GridSquad
{
    public sealed class ShotEvaluator : MonoBehaviour
    {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private LayerMask coverLayerMask;

        private static readonly Vector3[] SampleOffsets =
        {
            new(0f, 0f, 0f),
            new(0f, 0.65f, 0f),
            new(0f, -0.55f, 0f),
            new(-0.35f, 0.1f, 0f),
            new(0.35f, 0.1f, 0f)
        };

        public ShotEvaluation EvaluateShot(Combatant shooter, Combatant target)
        {
            if (target == null)
                return ShotEvaluation.CannotShoot(ShotFailureReason.NoTarget, shooter.MuzzlePosition, shooter.transform.position);
            if (!target.IsAlive)
                return ShotEvaluation.CannotShoot(ShotFailureReason.TargetDead, shooter.MuzzlePosition, target.CurrentAimCenter);

            Vector3 targetCenter = target.CurrentAimCenter;
            ShotEvaluation direct = EvaluateFromWorldPosition(shooter, targetCenter, shooter.MuzzlePosition, false);
            if (!shooter.PeekEnabled)
                return direct;

            if (!TryFindBestPeekShot(shooter, targetCenter, shooter.CurrentCell, out ShotEvaluation peek))
            {
                if (direct.CanShoot)
                    return direct;
                direct.FailureReason = ShotFailureReason.NoPeekPosition;
                return direct;
            }

            if (!direct.CanShoot || peek.HitChancePercent > direct.HitChancePercent)
                return peek;
            return direct;
        }

        public ShotEvaluation EvaluateShotFromCell(Combatant shooter, Combatant target, GridCoordinate shooterCell, bool allowPeek)
        {
            if (target == null || !target.IsAlive)
                return ShotEvaluation.CannotShoot(ShotFailureReason.NoTarget, gridMap.GridToWorld(shooterCell), Vector3.zero);

            Vector3 origin = gridMap.GridToWorld(shooterCell) + Vector3.up * shooter.MuzzleHeight;
            Vector3 targetCenter = target.CurrentAimCenter;
            ShotEvaluation direct = EvaluateFromWorldPosition(shooter, targetCenter, origin, false);
            if (!allowPeek)
                return direct;

            if (TryFindBestPeekShot(shooter, targetCenter, shooterCell, out ShotEvaluation peek)
                && (!direct.CanShoot || peek.HitChancePercent > direct.HitChancePercent))
                return peek;
            return direct;
        }

        public float EvaluateIncomingCoverAtCell(Combatant attacker, GridCoordinate targetCell)
        {
            if (attacker == null || !attacker.IsAlive)
                return 0f;
            Vector3 center = gridMap.GridToWorld(targetCell) + Vector3.up * 1.1f;
            ShotEvaluation evaluation = EvaluateFromWorldPosition(attacker, center, attacker.MuzzlePosition, false);
            return evaluation.CanShoot ? evaluation.CoverEvasionPercent : tuning.MaximumCoverEvasionPercent;
        }

        private bool TryFindBestPeekShot(
            Combatant shooter,
            Vector3 targetCenter,
            GridCoordinate shooterCell,
            out ShotEvaluation best)
        {
            best = default;
            Vector3 shooterWorld = gridMap.GridToWorld(shooterCell);
            Vector3 targetDirection = targetCenter - shooterWorld;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude < 0.01f)
                return false;
            targetDirection.Normalize();

            GridCoordinate? coverCell = null;
            float bestCoverAlignment = float.NegativeInfinity;
            foreach (GridCoordinate neighbor in gridMap.GetCardinalNeighbors(shooterCell))
            {
                if (!gridMap.IsBlocked(neighbor))
                    continue;
                Vector3 direction = gridMap.GridToWorld(neighbor) - shooterWorld;
                direction.y = 0f;
                float alignment = Vector3.Dot(direction.normalized, targetDirection);
                if (alignment > bestCoverAlignment)
                {
                    bestCoverAlignment = alignment;
                    coverCell = neighbor;
                }
            }

            if (!coverCell.HasValue)
                return false;

            GridCoordinate coverDirection = new(
                coverCell.Value.X - shooterCell.X,
                coverCell.Value.Z - shooterCell.Z);
            GridCoordinate[] sideDirections =
            {
                new(-coverDirection.Z, coverDirection.X),
                new(coverDirection.Z, -coverDirection.X)
            };

            bool found = false;
            foreach (GridCoordinate side in sideDirections)
            {
                GridCoordinate candidateCell = shooterCell + side;
                if (!gridMap.IsAvailablePeekCell(candidateCell, shooter))
                    continue;

                Vector3 origin = gridMap.GridToWorld(candidateCell) + Vector3.up * shooter.MuzzleHeight;
                ShotEvaluation candidate = EvaluateFromWorldPosition(shooter, targetCenter, origin, true);
                if (!found || candidate.VisibleSampleCount > best.VisibleSampleCount
                    || (candidate.VisibleSampleCount == best.VisibleSampleCount && candidate.HitChancePercent > best.HitChancePercent))
                {
                    found = true;
                    best = candidate;
                }
            }
            return found;
        }

        private ShotEvaluation EvaluateFromWorldPosition(
            Combatant shooter,
            Vector3 targetCenter,
            Vector3 origin,
            bool usesPeekPosition)
        {
            float range = Vector3.Distance(
                new Vector3(origin.x, 0f, origin.z),
                new Vector3(targetCenter.x, 0f, targetCenter.z)) / gridMap.CellSize;
            if (range > shooter.Weapon.RangeInCells)
                return ShotEvaluation.CannotShoot(ShotFailureReason.OutOfRange, origin, targetCenter);

            int visible = 0;
            foreach (Vector3 offset in SampleOffsets)
            {
                Vector3 sample = targetCenter + offset;
                Vector3 direction = sample - origin;
                float distance = direction.magnitude;
                if (distance <= 0.01f || !Physics.Raycast(origin, direction.normalized, distance, coverLayerMask, QueryTriggerInteraction.Ignore))
                    visible++;
            }

            if (visible == 0)
                return ShotEvaluation.CannotShoot(ShotFailureReason.FullyBlocked, origin, targetCenter);

            float coverEvasion = (1f - visible / (float)SampleOffsets.Length) * tuning.MaximumCoverEvasionPercent;
            float hitChance = Mathf.Clamp(
                shooter.Weapon.BaseHitChancePercent - coverEvasion,
                tuning.MinimumHitChancePercent,
                tuning.MaximumHitChancePercent);

            return new ShotEvaluation
            {
                CanShoot = true,
                UsesPeekPosition = usesPeekPosition,
                VisibleSampleCount = visible,
                CoverEvasionPercent = coverEvasion,
                HitChancePercent = hitChance,
                ShotOrigin = origin,
                TargetCenter = targetCenter,
                FailureReason = ShotFailureReason.None
            };
        }

#if UNITY_EDITOR
        public void SetEditorReferences(GridMap newGridMap, CombatTuning newTuning, LayerMask newCoverLayerMask)
        {
            gridMap = newGridMap;
            tuning = newTuning;
            coverLayerMask = newCoverLayerMask;
        }
#endif
    }
}
