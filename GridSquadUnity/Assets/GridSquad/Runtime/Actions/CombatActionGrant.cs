using System;
using System.Collections.Generic;

namespace GridSquad
{
    public readonly struct CombatActionGrant
    {
        public readonly string SourceKey;
        public readonly string SourceDisplayName;
        public readonly CombatActionDefinition Definition;
        public readonly Func<bool> ConsumeOnSuccessfulUse;

        public CombatActionGrant(
            string sourceKey,
            string sourceDisplayName,
            CombatActionDefinition definition,
            Func<bool> consumeOnSuccessfulUse)
        {
            SourceKey = sourceKey;
            SourceDisplayName = sourceDisplayName;
            Definition = definition;
            ConsumeOnSuccessfulUse = consumeOnSuccessfulUse;
        }

        public string RuntimeKey => $"{SourceKey}|{Definition?.ActionId}";
    }

    public interface ICombatActionGrantProvider
    {
        void CollectCombatActionGrants(List<CombatActionGrant> grants);
    }
}
