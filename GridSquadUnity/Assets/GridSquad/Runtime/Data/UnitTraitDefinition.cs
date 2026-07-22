using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Unit Trait Definition", fileName = "UnitTraitDefinition")]
    public sealed class UnitTraitDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "특성";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private UnitStatModifier[] modifiers = Array.Empty<UnitStatModifier>();
        [SerializeField] private CombatActionDefinition[] grantedActions = Array.Empty<CombatActionDefinition>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public IReadOnlyList<UnitStatModifier> Modifiers => modifiers;
        public IReadOnlyList<CombatActionDefinition> GrantedActions => grantedActions;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newDisplayName,
            string newDescription,
            Sprite newIcon,
            UnitStatModifier[] newModifiers)
        {
            displayName = newDisplayName;
            description = newDescription;
            icon = newIcon;
            modifiers = newModifiers ?? Array.Empty<UnitStatModifier>();
        }

        public void SetEditorGrantedActions(CombatActionDefinition[] newGrantedActions)
            => grantedActions = newGrantedActions ?? Array.Empty<CombatActionDefinition>();
#endif
    }
}
