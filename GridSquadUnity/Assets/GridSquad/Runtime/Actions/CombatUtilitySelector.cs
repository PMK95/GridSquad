using System.Collections.Generic;

namespace GridSquad
{
    public sealed class CombatUtilitySelector
    {
        private readonly List<CombatActionCandidate> candidates = new();

        public IReadOnlyList<CombatActionCandidate> Candidates => candidates;

        public bool TrySelectHighestUtilityAction(
            CombatActionContext context,
            IReadOnlyList<ICombatAction> actions,
            out CombatActionCandidate selected)
        {
            candidates.Clear();
            foreach (ICombatAction action in actions)
                action.CollectAutomaticCandidates(context, candidates);

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
            return left.Kind.CompareTo(right.Kind);
        }
    }
}
