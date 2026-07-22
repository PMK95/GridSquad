using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public readonly struct TacticalCellChoice
    {
        public readonly GridCoordinate Cell;
        public readonly ShootableTarget Target;
        public readonly ShotEvaluation Evaluation;
        public readonly CoverEvaluation IncomingCover;
        public readonly bool IsCoveredFiringPosition;
        public readonly float Score;
        public readonly int PathCost;
        public readonly float MovementExposurePenalty;

        public TacticalCellChoice(
            GridCoordinate cell,
            ShootableTarget target,
            ShotEvaluation evaluation,
            CoverEvaluation incomingCover,
            bool isCoveredFiringPosition,
            float score,
            int pathCost,
            float movementExposurePenalty)
        {
            Cell = cell;
            Target = target;
            Evaluation = evaluation;
            IncomingCover = incomingCover;
            IsCoveredFiringPosition = isCoveredFiringPosition;
            Score = score;
            PathCost = pathCost;
            MovementExposurePenalty = movementExposurePenalty;
        }
    }

    public sealed class TacticalPositionEvaluator : MonoBehaviour
    {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatTuning tuning;

        private TacticalPositionCandidateCollector candidateCollector;

        private void Awake()
        {
            BuildEvaluationServices();
        }

        public List<TacticalCellChoice> EvaluateReachableFiringCells(
            Combatant requester,
            bool allowPeek,
            ShootableTarget requiredTarget = null)
        {
            List<TacticalCellChoice> choices = new();
            EnsureEvaluationServices();
            candidateCollector.CollectReachableChoices(
                choices,
                requester,
                allowPeek,
                requiredTarget);
            KeepFriendlyFireSafeChoicesWhenAvailable(choices);
            choices.Sort(CompareChoicesByScore);
            return choices;
        }

        public List<TacticalCellChoice> EvaluateCurrentCellFiringChoices(
            Combatant requester,
            bool allowPeek,
            ShootableTarget requiredTarget = null)
        {
            List<TacticalCellChoice> choices = new();
            EnsureEvaluationServices();
            candidateCollector.CollectCurrentCellChoices(
                choices,
                requester,
                allowPeek,
                requiredTarget);
            KeepFriendlyFireSafeChoicesWhenAvailable(choices);
            choices.Sort(CompareChoicesByScore);
            return choices;
        }

        private static void KeepFriendlyFireSafeChoicesWhenAvailable(List<TacticalCellChoice> choices)
        {
            bool hasSafeChoice = choices.Exists(
                choice => choice.Evaluation.FriendlyFireRiskPercent <= 0.01f);
            if (!hasSafeChoice)
                return;

            choices.RemoveAll(choice => choice.Evaluation.FriendlyFireRiskPercent > 0.01f);
        }

        private void EnsureEvaluationServices()
        {
            if (candidateCollector == null)
                BuildEvaluationServices();
        }

        private void BuildEvaluationServices()
        {
            TacticalPositionScorer scorer = new(shotEvaluator, director, tuning);
            candidateCollector = new TacticalPositionCandidateCollector(
                gridMap,
                director,
                tuning,
                scorer);
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
            BuildEvaluationServices();
        }
#endif
    }
}
