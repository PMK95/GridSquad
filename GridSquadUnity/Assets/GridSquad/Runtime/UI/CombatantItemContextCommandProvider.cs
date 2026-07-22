using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class CombatantItemContextCommandProvider : MonoBehaviour, IContextCommandProvider
    {
        private Combatant combatant;

        private void Awake() => combatant = GetComponent<Combatant>();

        public void CollectAvailableContextCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands)
        {
            combatant = combatant != null ? combatant : GetComponent<Combatant>();
            ItemInstance item = query.Item;
            if (combatant == null || item?.Definition == null || combatant.Team != Team.Ally)
                return;
            if (query.EquipmentSlot != null && query.EquipmentLoadout != null)
            {
                AddEquippedItemCommands(query, commands, item);
                return;
            }
            if (query.Inventory != null)
                AddInventoryItemCommands(query, commands, item);
        }

        private void AddInventoryItemCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands,
            ItemInstance item)
        {
            if (item.Definition is EquippableDefinition)
            {
                EquipmentSlotDefinition slot = FindPreferredSlot(item);
                bool canEquip = slot != null;
                commands.Add(new ContextCommand(
                    $"inventory.equip.{item.InstanceId}",
                    "장착",
                    item.Definition.Icon,
                    10,
                    canEquip,
                    canEquip ? string.Empty : "호환되는 장비 슬롯이 없습니다.",
                    () =>
                    {
                        if (!combatant.EquipmentLoadout.TryEquipFromInventory(slot, item, out string reason))
                            query.Hud?.SetActionMessage(reason);
                    }));
            }
            if (item.Definition is ConsumableItemDefinition)
            {
                string runtimeKey = FindCarriedItemActionRuntimeKey(item);
                bool canUse = !string.IsNullOrWhiteSpace(runtimeKey);
                commands.Add(new ContextCommand(
                    $"inventory.use.{item.InstanceId}",
                    "빠른 사용",
                    item.Definition.Icon,
                    20,
                    canUse,
                    canUse ? string.Empty : "사용 가능한 행동이 없습니다.",
                    () => UseCarriedItemAction(runtimeKey, query.Hud)));
            }
            commands.Add(new ContextCommand(
                $"inventory.drop.{item.InstanceId}",
                "버리기",
                item.Definition.Icon,
                100,
                true,
                string.Empty,
                () => query.Inventory.TryDrop(item, item.Quantity, out _, out _)));
        }

        private static void AddEquippedItemCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands,
            ItemInstance item)
        {
            commands.Add(new ContextCommand(
                $"equipment.unequip.{item.InstanceId}",
                "해제",
                item.Definition.Icon,
                10,
                true,
                string.Empty,
                () => query.EquipmentLoadout.TryUnequip(query.EquipmentSlot, out _)));
            commands.Add(new ContextCommand(
                $"equipment.drop.{item.InstanceId}",
                "버리기",
                item.Definition.Icon,
                100,
                true,
                string.Empty,
                () => query.EquipmentLoadout.TryDropEquipped(query.EquipmentSlot, out _, out _)));
        }

        private EquipmentSlotDefinition FindPreferredSlot(ItemInstance item)
        {
            EquipmentLoadout loadout = combatant.EquipmentLoadout;
            if (loadout == null || item?.Definition == null)
                return null;
            if (item.Definition is WeaponDefinition)
                return loadout.GetSlot(EquipmentSlotKind.LeftHand);
            if (item.Definition is OffHandDefinition)
                return loadout.GetSlot(EquipmentSlotKind.RightHand);
            if (item.Definition is ArmorDefinition armor)
                return loadout.GetSlot(armor.ArmorSlotKind);
            if (item.Definition is AdditionalEquipmentDefinition)
            {
                EquipmentSlotDefinition first = loadout.GetSlot(EquipmentSlotKind.SupportOne);
                return loadout.GetItemInstance(first) == null
                    ? first
                    : loadout.GetSlot(EquipmentSlotKind.SupportTwo);
            }
            return null;
        }

        private string FindCarriedItemActionRuntimeKey(ItemInstance item)
        {
            CombatActionController controller = combatant.GetComponent<CombatActionController>();
            if (controller == null)
                return null;
            string prefix = $"item:{item.InstanceId}|";
            for (int index = 0; index < controller.PlayerActionCount; index++)
            {
                CombatActionRuntimeState state = controller.GetPlayerActionRuntimeState(index);
                if (state.RuntimeKey != null && state.RuntimeKey.StartsWith(prefix))
                    return state.RuntimeKey;
            }
            return null;
        }

        private void UseCarriedItemAction(string runtimeKey, CombatHudController hud)
        {
            CombatActionController controller = combatant.GetComponent<CombatActionController>();
            if (controller == null)
                return;
            CombatActionTargetSelection selection = CombatActionTargetSelection.Self(combatant.CurrentCell);
            if (!controller.TryQueuePlayerAction(runtimeKey, selection, out string reason))
                hud?.SetActionMessage(reason);
        }
    }
}
