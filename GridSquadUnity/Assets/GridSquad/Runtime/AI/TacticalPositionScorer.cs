using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    internal sealed class TacticalPositionScorer
    {
        private readonly ShotEvaluator shotEvaluator;
        private readonly CombatDirector director;
        private readonly CombatTuning tuning;

        public TacticalPositionScorer(
            ShotEvaluator shotEvaluator,
            CombatDirector director,
            CombatTuning tuning)
        {
            this.shotEvaluator = shotEvaluator;
            this.director = director;
            this.tuning = tuning;
        }

        public void TryAddChoice(
            List<TacticalCellChoice> results,
            Combatant requester,
            ShootableTarget target,
            GridCoordinate cell,
            int pathCost,
            float movementExposurePenalty,
            bool allowPeek,
            bool adjacentToCover)
        {
            if (target == null || !target.IsAlive || target.TargetTeam == requester.Team)
                return;
            ShotEvaluation shot = shotEvaluator.EvaluateShotFromCell(
                requester,
                target,
                cell,
                allowPeek);
            if (!shot.CanShoot)
                return;

            Combatant targetCombatant = target.Entity != null ? target.Entity.Combatant : null;
            CoverEvaluation incomingCover = targetCombatant != null
                ? shotEvaluator.EvaluateIncomingCover(targetCombatant, cell)
                : CoverEvaluation.None;
            bool coveredFiringPosition = adjacentToCover && incomingCover.HasCover;
            int distance = cell.ManhattanDistance(target.CurrentCell);
            float score = tuning.AiShootableScore
                + shot.HitChancePercent
                + incomingCover.EvasionPercent
                - shot.FriendlyFireRiskPercent * tuning.AiFriendlyFireRiskScoreWeight
                - pathCost * tuning.AiPathCostWeight
                - movementExposurePenalty
                - Mathf.Abs(distance - tuning.AiIdealRangeCells) * tuning.AiRangeDifferenceWeight;
            results.Add(new TacticalCellChoice(
                cell,
                target,
                shot,
                incomingCover,
                coveredFiringPosition,
                score,
                pathCost,
                movementExposurePenalty));
        }

        public float CalculateMovementExposurePenalty(
            Combatant requester,
            IReadOnlyList<GridCoordinate> path)
        {
            float penalty = 0f;
            foreach (GridCoordinate pathCell in path)
            {
                float highestIncomingHitChance = 0f;
                foreach (Combatant enemy in director.GetLivingEnemies(requester.Team))
                {
                    ShotEvaluation incomingShot = shotEvaluator.EvaluateShotAtCell(
                        enemy,
                        pathCell,
                        enemy.CurrentIndicatorShotOriginCell);
                    if (incomingShot.CanShoot)
                    {
                        highestIncomingHitChance = Mathf.Max(
                            highestIncomingHitChance,
                            incomingShot.HitChancePercent);
                    }
                }
                penalty += highestIncomingHitChance * 0.08f;
            }
            return penalty;
        }
    }
}
