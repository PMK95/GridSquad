using UnityEngine;

namespace GridSquad
{
    internal static class CombatActionPresentation
    {
        public static Sprite GetDisplayedIcon(
            Combatant combatant,
            CombatActionRuntimeState state)
        {
            if (state.Definition != null
                && state.Definition.HasCapability(CombatActionCapabilityFlags.DefaultAttack)
                && combatant != null
                && combatant.Weapon != null
                && combatant.Weapon.Icon != null)
            {
                return combatant.Weapon.Icon;
            }

            return state.Definition != null ? state.Definition.Icon : null;
        }
    }
}
