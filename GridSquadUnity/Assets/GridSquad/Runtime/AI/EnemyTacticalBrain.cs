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
            public readonly float Score;
            public readonly int PathCost;

            public CellChoice(GridCoordinate cell, Combatant target, ShotEvaluation evaluation, float score, int pathCost)
            {
                Cell = cell;
                Target = target;
                Evaluation = evaluation;
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
            List<CellChoice> choices = EvaluateCandidateCells();
            if (choices.Count == 0)
                return;

            choices.Sort((left, right) => right.Score.CompareTo(left.Score));
            debugChoices.Clear();
            for (int index = 0; index < Mathf.Min(3, choices.Count); index++)
                debugChoices.Add(choices[index]);

            CellChoice currentChoice = FindCurrentCellChoice(choices);
            bool canMove = !combatant.IsMoving && Time.time >= nextMovementTime;
            if (canMove)
            {
                foreach (CellChoice choice in choices)
                {
                    if (choice.Cell == combatant.CurrentCell
                        || choice.Score < currentChoice.Score + tuning.AiMinimumImprovement)
                        continue;
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
                combatant.SetPriorityTarget(currentChoice.Target);
                combatant.SetPeekEnabled(currentChoice.Evaluation.UsesPeekPosition);
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

                    CellChoice? bestAtCell = EvaluateBestTargetAtCell(cell, path.Count);
                    if (bestAtCell.HasValue)
                        choices.Add(bestAtCell.Value);
                }
            }
            return choices;
        }

        private CellChoice? EvaluateBestTargetAtCell(GridCoordinate cell, int pathCost)
        {
            Combatant bestTarget = null;
            ShotEvaluation bestShot = default;
            int bestDistance = int.MaxValue;
            foreach (Combatant target in director.GetLivingEnemies(combatant.Team))
            {
                ShotEvaluation shot = shotEvaluator.EvaluateShotFromCell(combatant, target, cell, true);
                int distance = cell.ManhattanDistance(target.CurrentCell);
                if (bestTarget == null
                    || shot.HitChancePercent > bestShot.HitChancePercent
                    || (Mathf.Approximately(shot.HitChancePercent, bestShot.HitChancePercent) && distance < bestDistance))
                {
                    bestTarget = target;
                    bestShot = shot;
                    bestDistance = distance;
                }
            }
            if (bestTarget == null)
                return null;

            float incomingCover = shotEvaluator.EvaluateIncomingCoverAtCell(bestTarget, cell);
            float score = (bestShot.CanShoot ? tuning.AiShootableScore : 0f)
                + bestShot.HitChancePercent
                + incomingCover
                - pathCost * tuning.AiPathCostWeight
                - Mathf.Abs(bestDistance - tuning.AiIdealRangeCells) * tuning.AiRangeDifferenceWeight;
            return new CellChoice(cell, bestTarget, bestShot, score, pathCost);
        }

        private CellChoice FindCurrentCellChoice(List<CellChoice> choices)
        {
            foreach (CellChoice choice in choices)
            {
                if (choice.Cell == combatant.CurrentCell)
                    return choice;
            }
            return new CellChoice(combatant.CurrentCell, null, default, float.NegativeInfinity, 0);
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
                UnityEditor.Handles.Label(position + Vector3.up * 0.25f, $"#{index + 1} {choice.Score:0}");
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
