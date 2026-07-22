using System.Collections.Generic;

namespace GridSquad
{
    internal sealed class TacticalPositionCandidateCollector
    {
        private readonly GridMap gridMap;
        private readonly CombatDirector director;
        private readonly CombatTuning tuning;
        private readonly TacticalPositionScorer scorer;

        public TacticalPositionCandidateCollector(
            GridMap gridMap,
            CombatDirector director,
            CombatTuning tuning,
            TacticalPositionScorer scorer)
        {
            this.gridMap = gridMap;
            this.director = director;
            this.tuning = tuning;
            this.scorer = scorer;
        }

        public void CollectReachableChoices(
            List<TacticalCellChoice> results,
            Combatant requester,
            bool allowPeek,
            ShootableTarget requiredTarget)
        {
            GridCoordinate origin = requester.CurrentCell;
            for (int x = 0; x < gridMap.Width; x++)
            {
                for (int z = 0; z < gridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (origin.ManhattanDistance(cell) > tuning.AiCandidatePathDistance)
                        continue;
                    List<GridCoordinate> path = GridPathfinder.FindPath(
                        gridMap,
                        origin,
                        cell,
                        requester.Entity);
                    if (path == null || path.Count > tuning.AiCandidatePathDistance)
                        continue;
                    CollectTargetChoicesAtCell(
                        results,
                        requester,
                        cell,
                        path,
                        allowPeek,
                        requiredTarget);
                }
            }
        }

        public void CollectCurrentCellChoices(
            List<TacticalCellChoice> results,
            Combatant requester,
            bool allowPeek,
            ShootableTarget requiredTarget)
        {
            CollectTargetChoicesAtCell(
                results,
                requester,
                requester.CurrentCell,
                null,
                allowPeek,
                requiredTarget);
        }

        private void CollectTargetChoicesAtCell(
            List<TacticalCellChoice> results,
            Combatant requester,
            GridCoordinate cell,
            IReadOnlyList<GridCoordinate> path,
            bool allowPeek,
            ShootableTarget requiredTarget)
        {
            int pathCost = path?.Count ?? 0;
            float exposurePenalty = path != null
                ? scorer.CalculateMovementExposurePenalty(requester, path)
                : 0f;
            bool adjacentToCover = gridMap.IsCoverPosition(cell);
            if (requiredTarget != null)
            {
                scorer.TryAddChoice(
                    results,
                    requester,
                    requiredTarget,
                    cell,
                    pathCost,
                    exposurePenalty,
                    allowPeek,
                    adjacentToCover);
                return;
            }
            foreach (Combatant target in director.GetLivingEnemies(requester.Team))
            {
                scorer.TryAddChoice(
                    results,
                    requester,
                    target.ShootableTarget,
                    cell,
                    pathCost,
                    exposurePenalty,
                    allowPeek,
                    adjacentToCover);
            }
        }
    }
}
