using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    internal readonly struct FriendlyFireCandidate
    {
        public readonly ShootableTarget Target;
        public readonly float DistanceAlongShot;
        public readonly float HitChancePercent;

        public FriendlyFireCandidate(
            ShootableTarget target,
            float distanceAlongShot,
            float hitChancePercent)
        {
            Target = target;
            DistanceAlongShot = distanceAlongShot;
            HitChancePercent = hitChancePercent;
        }
    }

    internal sealed class FriendlyFireEvaluator
    {
        private readonly GridMap gridMap;
        private readonly CombatTuning tuning;

        public FriendlyFireEvaluator(GridMap gridMap, CombatTuning tuning)
        {
            this.gridMap = gridMap;
            this.tuning = tuning;
        }

        public float CalculateFriendlyRiskPercent(
            Combatant shooter,
            ShootableTarget intendedTarget,
            Vector3 shotOrigin,
            Vector3 targetCenter)
        {
            float shotStillTravelingProbability = 1f;
            float friendlyHitProbability = 0f;
            foreach (FriendlyFireCandidate candidate in CollectCandidates(
                         shooter,
                         intendedTarget,
                         shotOrigin,
                         targetCenter))
            {
                float candidateHitProbability = candidate.HitChancePercent / 100f;
                if (candidate.Target.TargetTeam == shooter.Team)
                {
                    friendlyHitProbability += shotStillTravelingProbability
                        * candidateHitProbability;
                }
                shotStillTravelingProbability *= 1f - candidateHitProbability;
            }
            return friendlyHitProbability * 100f;
        }

        public List<FriendlyFireCandidate> CollectCandidates(
            Combatant shooter,
            ShootableTarget intendedTarget,
            Vector3 shotOrigin,
            Vector3 targetCenter)
        {
            List<FriendlyFireCandidate> candidates = new();
            Vector2 origin = new(shotOrigin.x, shotOrigin.z);
            Vector2 destination = new(targetCenter.x, targetCenter.z);
            Vector2 shotVector = destination - origin;
            float shotLengthSquared = shotVector.sqrMagnitude;
            if (shotLengthSquared <= 0.0001f)
                return candidates;

            float safeRangeWorld = tuning.FriendlyFireSafeRangeCells * gridMap.CellSize;
            foreach (ShootableTarget candidate in gridMap.GetRegisteredShootableTargets())
            {
                if (candidate == null
                    || candidate == shooter.ShootableTarget
                    || candidate == intendedTarget
                    || !candidate.IsAlive)
                {
                    continue;
                }
                Vector3 candidateCenterWorld = candidate.CurrentExposureCenter;
                Vector2 candidateCenter = new(candidateCenterWorld.x, candidateCenterWorld.z);
                float shotProgress = Vector2.Dot(candidateCenter - origin, shotVector) / shotLengthSquared;
                if (shotProgress <= 0f || shotProgress >= 1f)
                    continue;
                if (candidate.TargetTeam == shooter.Team
                    && Vector2.Distance(origin, candidateCenter) <= safeRangeWorld)
                {
                    continue;
                }

                Vector2 closestPoint = origin + shotVector * shotProgress;
                float distanceFromShot = Vector2.Distance(candidateCenter, closestPoint);
                float hitRadius = candidate.AccidentalHitRadiusWorld;
                if (distanceFromShot >= hitRadius)
                    continue;
                float hitChance = Mathf.Clamp01(1f - distanceFromShot / hitRadius) * 100f;
                if (hitChance <= 0.01f)
                    continue;
                candidates.Add(new FriendlyFireCandidate(
                    candidate,
                    Mathf.Sqrt(shotLengthSquared) * shotProgress,
                    hitChance));
            }
            candidates.Sort((left, right) => left.DistanceAlongShot.CompareTo(right.DistanceAlongShot));
            return candidates;
        }
    }
}
