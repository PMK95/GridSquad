using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class CombatActionLoadout : MonoBehaviour
    {
        public const int KeyboardShortcutActionCount = 4;

        [SerializeField] private CombatActionDefinition[] innateDefinitions = new CombatActionDefinition[0];
        [SerializeField] private CombatActionDefinition[] definitions = new CombatActionDefinition[0];

        public IReadOnlyList<CombatActionDefinition> InnateDefinitions => innateDefinitions;
        public IReadOnlyList<CombatActionDefinition> Definitions => definitions;
        public int PlayerActionCount => definitions?.Length ?? 0;

        public CombatActionDefinition GetPlayerDefinition(int slotIndex)
        {
            return definitions != null
                && slotIndex >= 0
                && slotIndex < definitions.Length
                    ? definitions[slotIndex]
                    : null;
        }

        public CombatActionDefinition FindDefinition(string actionId)
        {
            CombatActionDefinition found = FindDefinitionIn(innateDefinitions, actionId);
            return found != null ? found : FindDefinitionIn(definitions, actionId);
        }

        private static CombatActionDefinition FindDefinitionIn(
            CombatActionDefinition[] source,
            string actionId)
        {
            if (source == null)
                return null;
            foreach (CombatActionDefinition definition in source)
            {
                if (definition != null && definition.ActionId == actionId)
                    return definition;
            }
            return null;
        }

        public void ApplyUnitDefinitionDefaults(UnitDefinition unitDefinition)
        {
            if (unitDefinition == null)
                return;

            int definitionCount = unitDefinition.ActionDefinitions.Count;
            definitions = new CombatActionDefinition[definitionCount];
            for (int index = 0; index < definitionCount; index++)
                definitions[index] = unitDefinition.ActionDefinitions[index];
        }

#if UNITY_EDITOR
        public void SetEditorDefinitions(CombatActionDefinition[] newDefinitions)
        {
            definitions = newDefinitions ?? new CombatActionDefinition[0];
        }

        public void SetEditorInnateDefinitions(CombatActionDefinition[] newDefinitions)
        {
            innateDefinitions = newDefinitions ?? new CombatActionDefinition[0];
        }
#endif
    }
}
