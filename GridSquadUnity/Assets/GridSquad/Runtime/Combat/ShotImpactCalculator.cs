using UnityEngine;

namespace GridSquad
{
    internal sealed class ShotImpactCalculator
    {
        private readonly GridMap gridMap;
        private readonly FriendlyFireEvaluator friendlyFireEvaluator;

        public ShotImpactCalculator(
            GridMap gridMap,
            FriendlyFireEvaluator friendlyFireEvaluator)
        {
            this.gridMap = gridMap;
            this.friendlyFireEvaluator = friendlyFireEvaluator;
        }

        public ShotImpactResult Calculate(
            Combatant shooter,
            ShootableTarget intendedTarget,
            ShotEvaluation evaluation)
        {
            if (shooter == null
                || intendedTarget == null
                || !intendedTarget.IsAlive
                || !evaluation.CanShoot)
            {
                return ShotImpactResult.CannotFire(evaluation.TargetCenter);
            }

            foreach (FriendlyFireCandidate candidate in friendlyFireEvaluator.CollectCandidates(
                         shooter,
                         intendedTarget,
                         evaluation.ShotOrigin,
                         intendedTarget.CurrentExposureCenter))
            {
                if (Random.value * 100f > candidate.HitChancePercent)
                    continue;
                return new ShotImpactResult(
                    ShotImpactKind.AccidentalTargetHit,
                    candidate.Target,
                    candidate.Target.CurrentExposureCenter);
            }

            if (Random.value * 100f <= evaluation.HitChancePercent)
            {
                return new ShotImpactResult(
                    ShotImpactKind.IntendedTargetHit,
                    intendedTarget,
                    intendedTarget.CurrentExposureCenter);
            }
            return new ShotImpactResult(
                ShotImpactKind.Missed,
                null,
                CreateMissImpactPoint(evaluation, intendedTarget));
        }

        private Vector3 CreateMissImpactPoint(
            ShotEvaluation evaluation,
            ShootableTarget intendedTarget)
        {
            Vector2 offsetDirection = Random.insideUnitCircle;
            if (offsetDirection.sqrMagnitude <= 0.01f)
                offsetDirection = Vector2.right;
            offsetDirection.Normalize();
            float missDistance = Mathf.Max(
                intendedTarget.AccidentalHitRadiusWorld * 1.25f,
                gridMap.CellSize * 0.35f);
            return evaluation.TargetCenter
                + new Vector3(offsetDirection.x, 0f, offsetDirection.y) * missDistance;
        }
    }
}
