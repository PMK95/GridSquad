using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum UnitDetailTab
    {
        Stats,
        EquipmentInventory,
        Traits
    }

    public readonly struct ContextCommand
    {
        public readonly string CommandId;
        public readonly string Label;
        public readonly Sprite Icon;
        public readonly int Order;
        public readonly bool IsEnabled;
        public readonly string DisabledReason;
        public readonly Action Execute;
        public readonly string DetailText;

        public ContextCommand(
            string commandId,
            string label,
            Sprite icon,
            int order,
            bool isEnabled,
            string disabledReason,
            Action execute,
            string detailText = "")
        {
            CommandId = commandId;
            Label = label;
            Icon = icon;
            Order = order;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason;
            Execute = execute;
            DetailText = detailText;
        }
    }

    public readonly struct ContextCommandQuery
    {
        public readonly Combatant SelectedCombatant;
        public readonly TacticalEntity TargetEntity;
        public readonly SelectionDetailWindowController DetailWindow;
        public readonly CombatHudController Hud;
        public readonly ItemInstance Item;
        public readonly EquipmentSlotDefinition EquipmentSlot;
        public readonly EquipmentLoadout EquipmentLoadout;
        public readonly UnitInventory Inventory;

        public ContextCommandQuery(
            Combatant selectedCombatant,
            TacticalEntity targetEntity,
            SelectionDetailWindowController detailWindow,
            CombatHudController hud,
            ItemInstance item = null,
            EquipmentSlotDefinition equipmentSlot = null,
            EquipmentLoadout equipmentLoadout = null,
            UnitInventory inventory = null)
        {
            SelectedCombatant = selectedCombatant;
            TargetEntity = targetEntity;
            DetailWindow = detailWindow;
            Hud = hud;
            Item = item;
            EquipmentSlot = equipmentSlot;
            EquipmentLoadout = equipmentLoadout;
            Inventory = inventory;
        }
    }

    public interface IContextCommandProvider
    {
        void CollectAvailableContextCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands);
    }
}
