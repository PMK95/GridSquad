using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class CombatantContextCommandProvider : MonoBehaviour, IContextCommandProvider
    {
        private Combatant combatant;

        private void Awake() => combatant = GetComponent<Combatant>();

        public void CollectAvailableContextCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands)
        {
            combatant = combatant != null ? combatant : GetComponent<Combatant>();
            if (combatant == null || query.DetailWindow == null || query.Item != null)
                return;
            AddDetailCommand(commands, query, UnitDetailTab.Stats, "stats", "스탯", 100);
            AddDetailCommand(
                commands,
                query,
                UnitDetailTab.EquipmentInventory,
                "equipment_inventory",
                "장비 / 인벤토리",
                110);
            AddDetailCommand(commands, query, UnitDetailTab.Traits, "traits", "특성", 120);
        }

        private void AddDetailCommand(
            List<ContextCommand> commands,
            ContextCommandQuery query,
            UnitDetailTab tab,
            string id,
            string label,
            int order)
        {
            commands.Add(new ContextCommand(
                $"unit.{id}",
                label,
                null,
                order,
                true,
                string.Empty,
                () => query.DetailWindow.Show(combatant, tab)));
        }
    }
}
