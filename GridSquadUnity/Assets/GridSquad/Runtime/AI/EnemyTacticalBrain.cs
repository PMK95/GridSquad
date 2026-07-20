using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class EnemyTacticalBrain : MonoBehaviour
    {
        private readonly struct CellChoice
        {
            public readonly GridCoordinate Cell;
            public readonly Combatant Target;
            public readonly ShotEvaluation Evaluation;
            public readonly CoverEvaluation IncomingCover;
            public readonly bool IsCoveredFiringPosition;
            public readonly float Score;
            public readonly int PathCost;

            public CellChoice(
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

        [SerializeField] private Combatant combatant;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatTuning tuning;

        private readonly List<CellChoice> debugChoices = new();
        private float nextEvaluationTime;
        private float nextMovementTime;

        private void Update()
        {
            if (!combatant.IsAlive || director.BattleFinished || Time.time < nextEvaluationTime)
                return;

            nextEvaluationTime = Time.time + tuning.AiEvaluationInterval;
            ChooseBestCombatCell();
        }

        public void ChooseBestCombatCell()
        {
            List<CellChoice> allChoices = EvaluateCandidateCells();
            if (allChoices.Count == 0)
            {
                debugChoices.Clear();
                if (!combatant.IsMoving)
                {
                    combatant.SetPriorityTarget(null);
                    combatant.SetPeekEnabled(false);
                }
                return;
            }

            allChoices.Sort(CompareChoicesByScore);
            bool hasCoveredFiringPosition = allChoices.Exists(choice => choice.IsCoveredFiringPosition);
            List<CellChoice> movementChoices = hasCoveredFiringPosition
                ? allChoices.FindAll(choice => choice.IsCoveredFiringPosition)
                : allChoices;

            debugChoices.Clear();
            for (int index = 0; index < Mathf.Min(3, movementChoices.Count); index++)
                debugChoices.Add(movementChoices[index]);

            CellChoice? currentMovementChoice = FindBestCurrentCellChoice(movementChoices);
            CellChoice? currentFiringChoice = FindBestCurrentCellChoice(allChoices);
            bool canMove = !combatant.IsMoving && Time.time >= nextMovementTime;
            if (canMove)
            {
                bool enteringCoverTier = hasCoveredFiringPosition
                    && (!currentMovementChoice.HasValue || !currentMovementChoice.Value.IsCoveredFiringPosition);
                foreach (CellChoice choice in movementChoices)
                {
                    if (choice.Cell == combatant.CurrentCell)
                        continue;
                    if (!enteringCoverTier
                        && currentMovementChoice.HasValue
                        && choice.Score < currentMovementChoice.Value.Score + tuning.AiMinimumImprovement)
                    {
                        continue;
                    }
                    if (!combatant.SetMoveDestination(choice.Cell))
                        continue;

                    combatant.SetPriorityTarget(choice.Target);
                    combatant.SetPeekEnabled(false);
                    nextMovementTime = Time.time + tuning.AiMovementCooldown;
                    return;
                }
            }

            if (!combatant.IsMoving)
            {
                if (currentFiringChoice.HasValue)
                {
                    combatant.SetPriorityTarget(currentFiringChoice.Value.Target);
                    combatant.SetPeekEnabled(currentFiringChoice.Value.Evaluation.UsesPeekPosition);
                }
                else
                {
                    combatant.SetPriorityTarget(null);
                    combatant.SetPeekEnabled(false);
                }
            }
        }

        private List<CellChoice> EvaluateCandidateCells()
        {
            List<CellChoice> choices = new();
            GridCoordinate origin = combatant.CurrentCell;
            for (int x = 0; x < gridMap.Width; x++)
            {
                for (int z = 0; z < gridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (origin.ManhattanDistance(cell) > tuning.AiCandidatePathDistance)
                        continue;

                    List<GridCoordinate> path = GridPathfinder.FindPath(gridMap, origin, cell, combatant);
                    if (path == null || path.Count > tuning.AiCandidatePathDistance)
                        continue;

                    AddTargetChoicesAtCell(choices, cell, path.Count);
                }
            }
            return choices;
        }

        private void AddTargetChoicesAtCell(List<CellChoice> choices, GridCoordinate cell, int pathCost)
        {
            bool isAdjacentToCover = gridMap.IsCoverPosition(cell);
            foreach (Combatant target in director.GetLivingEnemies(combatant.Team))
            {
                ShotEvaluation shot = shotEvaluator.EvaluateShotFromCell(combatant, target, cell, true);
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
                choices.Add(new CellChoice(
                    cell,
                    target,
                    shot,
                    incomingCover,
                    isCoveredFiringPosition,
                    score,
                    pathCost));
            }
        }

        private CellChoice? FindBestCurrentCellChoice(List<CellChoice> choices)
        {
            foreach (CellChoice choice in choices)
            {
                if (choice.Cell == combatant.CurrentCell)
                    return choice;
            }
            return null;
        }

        private static int CompareChoicesByScore(CellChoice left, CellChoice right)
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

        private void OnDrawGizmos()
        {
            if (director == null || gridMap == null || !director.DebugVisible)
                return;

            for (int index = 0; index < debugChoices.Count; index++)
            {
                CellChoice choice = debugChoices[index];
                Gizmos.color = index == 0 ? Color.magenta : new Color(1f, 0.45f, 1f, 0.65f);
                Vector3 position = gridMap.GridToWorld(choice.Cell) + Vector3.up * (0.1f + index * 0.08f);
                Gizmos.DrawWireCube(position, new Vector3(gridMap.CellSize * 0.8f, 0.08f, gridMap.CellSize * 0.8f));
#if UNITY_EDITOR
                string positionType = choice.IsCoveredFiringPosition ? "COVER" : "EXPOSED";
                string shotType = choice.Evaluation.UsesPeekPosition ? "PEEK" : "DIRECT";
                string coverAngle = choice.IncomingCover.AngleDegrees >= 0f
                    ? $"{choice.IncomingCover.AngleDegrees:0}deg"
                    : "-";
                UnityEditor.Handles.Label(
                    position + Vector3.up * 0.25f,
                    $"#{index + 1} {positionType} {choice.Target.name} {shotType} ANG {coverAngle} HIT {choice.Evaluation.HitChancePercent:0}% SCORE {choice.Score:0}");
#endif
            }
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Combatant newCombatant,
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatDirector newDirector,
            CombatTuning newTuning)
        {
            combatant = newCombatant;
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            director = newDirector;
            tuning = newTuning;
        }
#endif
    }
}
