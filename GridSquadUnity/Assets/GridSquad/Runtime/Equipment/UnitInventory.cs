using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class UnitInventory : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fallbackCarryCapacity = 30f;
        [SerializeField] private List<ItemInstance> items = new();

        private Combatant owner;
        private EquipmentLoadout equipmentLoadout;
        private bool defaultsApplied;

        public event Action InventoryChanged;
        public IReadOnlyList<ItemInstance> Items => items;
        public float CarryCapacity => owner != null ? owner.CarryCapacity : fallbackCarryCapacity;
        public float CarriedWeight
        {
            get
            {
                float total = equipmentLoadout != null ? equipmentLoadout.EquippedWeight : 0f;
                foreach (ItemInstance item in items)
                    total += item != null ? item.TotalWeight : 0f;
                return total;
            }
        }

        private void Awake()
        {
            InitializeRuntimeReferencesIfNeeded();
            RemoveInvalidEntries();
        }

        public void ApplyUnitDefinitionDefaults(UnitDefinition definition)
        {
            InitializeRuntimeReferencesIfNeeded();
            if (defaultsApplied || definition == null)
                return;
            defaultsApplied = true;
            items.Clear();
            equipmentLoadout?.ClearEquippedItems();

            HashSet<ItemDefinition> equippedDefinitions = new();
            foreach (EquipmentSlotAssignment assignment in definition.DefaultEquipmentAssignments)
            {
                if (assignment.Slot == null || assignment.Equipment == null)
                    continue;
                ItemInstance instance = new(assignment.Equipment);
                if (equipmentLoadout != null
                    && equipmentLoadout.TryEquipOwnedItemImmediately(
                        assignment.Slot,
                        instance,
                        this,
                        out _))
                {
                    equippedDefinitions.Add(assignment.Equipment);
                }
                else
                {
                    AddWithoutWeightCheck(instance);
                }
            }

            WeaponDefinition firstWeapon = definition.GetDefaultWeapon(0);
            if (firstWeapon != null && !equippedDefinitions.Contains(firstWeapon))
            {
                EquipmentSlotDefinition leftHand = equipmentLoadout != null
                    ? equipmentLoadout.GetSlot(EquipmentSlotKind.LeftHand)
                    : null;
                ItemInstance instance = new(firstWeapon);
                if (leftHand == null
                    || !equipmentLoadout.TryEquipOwnedItemImmediately(
                        leftHand,
                        instance,
                        this,
                        out _))
                {
                    AddWithoutWeightCheck(instance);
                }
            }

            WeaponDefinition secondWeapon = definition.GetDefaultWeapon(1);
            bool secondWeaponAlreadyMigrated = definition.StartingInventoryItems.Any(
                item => item.Definition == secondWeapon);
            if (secondWeapon != null
                && !equippedDefinitions.Contains(secondWeapon)
                && !secondWeaponAlreadyMigrated)
                AddWithoutWeightCheck(new ItemInstance(secondWeapon));

            foreach (StartingInventoryItem startingItem in definition.StartingInventoryItems)
            {
                if (startingItem.Definition != null)
                    AddWithoutWeightCheck(new ItemInstance(startingItem.Definition, startingItem.Quantity));
            }
            InventoryChanged?.Invoke();
        }

        private void InitializeRuntimeReferencesIfNeeded()
        {
            owner = owner != null ? owner : GetComponent<Combatant>();
            equipmentLoadout = equipmentLoadout != null
                ? equipmentLoadout
                : GetComponent<EquipmentLoadout>();
            equipmentLoadout?.InitializeRuntimeEquipmentStateIfNeeded();
        }

        public bool CanAccept(ItemInstance item, out string failureReason)
        {
            if (item == null || item.Definition == null)
                return Fail("유효한 아이템이 아닙니다.", out failureReason);
            float projected = CarriedWeight + item.TotalWeight;
            if (projected > CarryCapacity + 0.001f)
            {
                return Fail(
                    $"무게 제한을 초과합니다. {projected:0.##}/{CarryCapacity:0.##}kg",
                    out failureReason);
            }
            failureReason = string.Empty;
            return true;
        }

        public bool TryAdd(ItemInstance item, out string failureReason)
        {
            if (!CanAccept(item, out failureReason))
                return false;
            AddWithoutWeightCheck(item);
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryAdd(
            ItemDefinition definition,
            int quantity,
            out string failureReason)
        {
            if (definition == null)
                return Fail("추가할 아이템이 없습니다.", out failureReason);
            if (quantity <= 0)
                return Fail("추가 수량은 1개 이상이어야 합니다.", out failureReason);

            float projectedWeight = CarriedWeight + definition.Weight * quantity;
            if (projectedWeight > CarryCapacity + 0.001f)
            {
                return Fail(
                    $"무게 제한을 초과합니다. {projectedWeight:0.##}/{CarryCapacity:0.##}kg",
                    out failureReason);
            }

            int remainingQuantity = quantity;
            while (remainingQuantity > 0)
            {
                int stackQuantity = Mathf.Min(remainingQuantity, definition.MaximumStack);
                AddWithoutWeightCheck(new ItemInstance(definition, stackQuantity));
                remainingQuantity -= stackQuantity;
            }

            InventoryChanged?.Invoke();
            failureReason = string.Empty;
            return true;
        }

        public bool Remove(ItemInstance item)
        {
            bool removed = item != null && items.Remove(item);
            if (removed)
                InventoryChanged?.Invoke();
            return removed;
        }

        public bool Consume(string itemInstanceId, int amount, out string failureReason)
        {
            ItemInstance item = Find(itemInstanceId);
            if (item == null || !item.ConsumeQuantity(amount))
                return Fail("아이템 수량이 부족합니다.", out failureReason);
            if (item.Quantity <= 0)
                items.Remove(item);
            InventoryChanged?.Invoke();
            failureReason = string.Empty;
            return true;
        }

        public ItemInstance Find(string itemInstanceId)
        {
            foreach (ItemInstance item in items)
                if (item != null && item.InstanceId == itemInstanceId)
                    return item;
            return null;
        }

        public IEnumerable<CombatActionGrant> EnumerateCarriedCombatActionGrants()
        {
            foreach (ItemInstance item in items)
            {
                if (item?.Definition == null)
                    continue;
                foreach (ItemActionGrant grant in item.Definition.ActionGrants)
                {
                    if (grant.Action == null || grant.Availability != ItemActionAvailability.Carried)
                        continue;
                    ItemInstance capturedItem = item;
                    yield return new CombatActionGrant(
                        $"item:{item.InstanceId}",
                        item.Definition.DisplayName,
                        grant.Action,
                        item.Definition is ConsumableItemDefinition
                            ? () => Consume(capturedItem.InstanceId, 1, out _)
                            : null);
                }
            }
        }

        public bool TryDrop(ItemInstance item, int quantity, out WorldItemPickup pickup, out string failureReason)
        {
            pickup = null;
            if (owner == null || item == null || !items.Contains(item))
                return Fail("버릴 아이템이 인벤토리에 없습니다.", out failureReason);

            ItemInstance dropped = item.Quantity > quantity
                ? item.CreateSplitInstance(quantity)
                : item;
            if (ReferenceEquals(dropped, item))
                items.Remove(item);
            pickup = WorldItemPickup.CreateDroppedItem(
                dropped,
                owner.GridMap,
                owner.CurrentCell);
            InventoryChanged?.Invoke();
            failureReason = string.Empty;
            return pickup != null;
        }

        internal void NotifyEquipmentChanged()
        {
            InventoryChanged?.Invoke();
            GetComponent<CombatActionController>()?.RefreshRuntimeActionsFromLoadout();
        }

        internal void AddReturnedEquipment(ItemInstance item)
        {
            if (item == null)
                return;
            AddWithoutWeightCheck(item);
            InventoryChanged?.Invoke();
        }

        private void AddWithoutWeightCheck(ItemInstance item)
        {
            if (item == null || item.Definition == null)
                return;
            int remaining = item.Quantity;
            if (item.Definition.IsStackable)
            {
                foreach (ItemInstance existing in items)
                {
                    if (existing == null || existing.Definition != item.Definition)
                        continue;
                    remaining = existing.AddQuantity(remaining);
                    if (remaining <= 0)
                        return;
                }
            }
            while (remaining > 0)
            {
                int stack = Mathf.Min(remaining, item.Definition.MaximumStack);
                ItemInstance added = remaining == item.Quantity && stack == item.Quantity
                    ? item
                    : new ItemInstance(item.Definition, stack);
                items.Add(added);
                remaining -= stack;
            }
        }

        private void RemoveInvalidEntries()
        {
            items.RemoveAll(item => item == null || item.Definition == null || item.Quantity <= 0);
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }
    }
}
