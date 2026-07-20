using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public readonly struct TacticalCellChoice
    {
        public readonly GridCoordinate Cell;
        public readonly Combatant Target;
        public readonly ShotEvaluation Evaluation;
        public readonly CoverEvaluation IncomingCover;
        public readonly bool IsCoveredFiringPosition;
        public readonly float Score;
        public readonly int PathCost;

        public TacticalCellChoice(
            GridCoordinate cell,
            Combatant target,
            ShotEvaluation evaluation,
            CoverEvaluation incomingCover,
            bool isCoveredFiringPosition,
            float score,
            int pathCost)
        {
            Cell = cell;
            Target = target;
            Evaluation = evaluation;
            IncomingCover = incomingCover;
            IsCoveredFiringPosition = isCoveredFiringPosition;
            Score = score;
            PathCost = pathCost;
        }
    }

    public sealed class TacticalPositionEvaluator : MonoBehaviour
    {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatTuning tuning;

        public List<TacticalCellChoice> EvaluateReachableFiringCells(
            Combatant requester,
            bool allowPeek,
            Combatant requiredTarget = null)
        {
            List<TacticalCellChoice> choices = new();
            GridCoordinate origin = requester.CurrentCell;
            for (int x = 0; x < gridMap.Width; x++)
            {
                for (int z = 0; z < gridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (origin.ManhattanDistance(cell) > tuning.AiCandidatePathDistance)
                        continue;

                    List<GridCoordinate> path = GridPathfinder.FindPath(gridMap, origin, cell, requester);
                    if (path == null || path.Count > tuning.AiCandidatePathDistance)
                        continue;

                    AddTargetChoicesAtCell(
                        choices,
                        requester,
                        cell,
                        path.Count,
                        allowPeek,
                        requiredTarget);
                }
            }
            choices.Sort(CompareChoicesByScore);
            return choices;
        }

        public List<TacticalCellChoice> EvaluateCurrentCellFiringChoices(
            Combatant requester,
            bool allowPeek,
            Combatant requiredTarget = null)
        {
            List<TacticalCellChoice> choices = new();
            AddTargetChoicesAtCell(
                choices,
                requester,
                requester.CurrentCell,
                0,
                allowPeek,
                requiredTarget);
            choices.Sort(CompareChoicesByScore);
            return choices;
        }

        private void AddTargetChoicesAtCell(
            List<TacticalCellChoice> choices,
            Combatant requester,
            GridCoordinate cell,
            int pathCost,
            bool allowPeek,
            Combatant requiredTarget)
        {
            bool isAdjacentToCover = gridMap.IsCoverPosition(cell);
            foreach (Combatant target in director.GetLivingEnemies(requester.Team))
            {
                if (requiredTarget != null && target != requiredTarget)
                    continue;

                ShotEvaluation shot = shotEvaluator.EvaluateShotFromCell(
                    requester,
                    target,
                    cell,
                    allowPeek);
                if (!shot.CanShoot)
                    continue;

                CoverEvaluation incomingCover = shotEvaluator.EvaluateIncomingCover(target, cell);
                bool isCoveredFiringPosition = isAdjacentToCover && incomingCover.HasCover;
                int distance = cell.ManhattanDistance(target.CurrentCell);
                float score = tuning.AiShootableScore
                    + shot.HitChancePercent
                    + incomingCover.EvasionPercent
                    - pathCost * tuning.AiPathCostWeight
                    - Mathf.Abs(distance - tuning.AiIdealRangeCells) * tuning.AiRangeDifferenceWeight;
                choices.Add(new TacticalCellChoice(
                    cell,
                    target,
                    shot,
                    incomingCover,
                    isCoveredFiringPosition,
                    score,
                    pathCost));
            }
        }

        public static int CompareChoicesByScore(TacticalCellChoice left, TacticalCellChoice right)
        {
            int scoreComparison = right.Score.CompareTo(left.Score);
            if (scoreComparison != 0)
                return scoreComparison;
            int pathComparison = left.PathCost.CompareTo(right.PathCost);
            if (pathComparison != 0)
                return pathComparison;
            return left.Cell.ManhattanDistance(left.Target.CurrentCell)
                .CompareTo(right.Cell.ManhattanDistance(right.Target.CurrentCell));
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatDirector newDirector,
            CombatTuning newTuning)
        {
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            director = newDirector;
            tuning = newTuning;
        }
#endif
    }
}
