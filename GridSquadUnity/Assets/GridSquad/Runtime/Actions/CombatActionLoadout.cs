using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class CombatActionLoadout : MonoBehaviour
    {
        [SerializeField] private CombatActionDefinition[] definitions = new CombatActionDefinition[0];

        public IReadOnlyList<CombatActionDefinition> Definitions => definitions;

        public CombatActionDefinition FindDefinition(CombatActionKind kind)
        {
            foreach (CombatActionDefinition definition in definitions)
            {
                if (definition != null && definition.Kind == kind)
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
#endif
    }
}
