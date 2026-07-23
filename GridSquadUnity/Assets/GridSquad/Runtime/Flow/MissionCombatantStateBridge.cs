using System;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class MissionCombatantStateBridge : MonoBehaviour
    {
        private Combatant combatant;
        private MissionUnitState missionUnit;
        private bool bound;

        public MissionUnitState MissionUnit => missionUnit;

        private void Awake()
        {
            combatant = GetComponent<Combatant>();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void Bind(MissionUnitState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            Unbind();
            missionUnit = state;
            combatant.InitializeMissionHealth(
                state.MaximumHealthAtLaunch,
                state.CurrentHealth);
            combatant.DamageResolved += HandleDamageResolved;
            combatant.Died += HandleCombatantDied;
            bound = true;
        }

        public void CommitCurrentHealth()
        {
            if (!bound)
                return;
            missionUnit.ApplyCombatResult(combatant.CurrentHealth, 0f);
            combatant.Inventory?.WriteMissionState(missionUnit);
        }

        private void HandleDamageResolved(
            Combatant damagedCombatant,
            CombatDamageRequest request,
            CombatDamageResult result)
        {
            missionUnit.ApplyCombatResult(
                damagedCombatant.CurrentHealth,
                result.GeneratedTrauma);
        }

        private void HandleCombatantDied(Combatant deadCombatant)
        {
            missionUnit.SetIncapacitated();
        }

        private void Unbind()
        {
            if (!bound || combatant == null)
                return;
            combatant.DamageResolved -= HandleDamageResolved;
            combatant.Died -= HandleCombatantDied;
            bound = false;
        }
    }
}
