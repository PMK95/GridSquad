using System.Collections.Generic;

namespace GridSquad
{
    public sealed class CombatUtilitySelector
    {
        private readonly List<CombatActionCandidate> candidates = new();

        public IReadOnlyList<CombatActionCandidate> Candidates => candidates;

        internal bool TrySelectHighestUtilityAction(
            CombatActionContext context,
            IReadOnlyList<CombatActionRuntime> actions,
            out CombatActionCandidate selected)
        {
            candidates.Clear();
            foreach (CombatActionRuntime action in actions)
            {
                int startIndex = candidates.Count;
                action.CandidateProvider.CollectCandidates(context, candidates);
                for (int index = startIndex; index < candidates.Count; index++)
                    candidates[index] = candidates[index].WithRuntimeKey(action.RuntimeKey);
            }

            candidates.Sort(CompareCandidates);
            if (candidates.Count == 0)
            {
                selected = default;
                return false;
            }

            selected = candidates[0];
            return true;
        }

        private static int CompareCandidates(
            CombatActionCandidate left,
            CombatActionCandidate right)
        {
            int scoreComparison = right.UtilityScore.CompareTo(left.UtilityScore);
            if (scoreComparison != 0)
                return scoreComparison;
            string leftId = left.Definition != null ? left.Definition.ActionId : string.Empty;
            string rightId = right.Definition != null ? right.Definition.ActionId : string.Empty;
            return string.CompareOrdinal(leftId, rightId);
        }
    }
}
