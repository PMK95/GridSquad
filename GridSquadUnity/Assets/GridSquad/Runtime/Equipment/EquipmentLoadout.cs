using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class EquipmentLoadout : MonoBehaviour
    {
        [SerializeField] private EquipmentLayoutDefinition layout;
        [SerializeField] private EquipmentSlotAssignment[] assignments = Array.Empty<EquipmentSlotAssignment>();

        private readonly Dictionary<EquipmentSlotDefinition, ItemInstance> equippedItems = new();
        private readonly Dictionary<string, int> passiveCharges = new();
        private readonly Dictionary<string, float> passiveRechargeProgress = new();
        private UnitInventory inventory;
        private bool battleInitialized;
        private bool legacyAssignmentsBuilt;

        public event Action EquipmentChanged;
        public event Action<EquipmentSlotDefinition, int, int> DurabilityChanged;
        public EquipmentLayoutDefinition Layout => layout;
        public bool IsBattleInitialized => battleInitialized;
        public float EquippedWeight
        {
            get
            {
                float weight = 0f;
                foreach (ItemInstance item in equippedItems.Values)
                    weight += item != null ? item.TotalWeight : 0f;
                return weight;
            }
        }

        private void Awake()
        {
            InitializeRuntimeEquipmentStateIfNeeded();
        }

        private void Update()
        {
            if (!battleInitialized)
                return;
            TickRegeneratingBallisticPlates(Time.deltaTime);
        }

        public IEnumerable<KeyValuePair<EquipmentSlotDefinition, ItemInstance>> EnumerateEquippedItems()
        {
            InitializeRuntimeEquipmentStateIfNeeded();
            return equippedItems;
        }

        internal void InitializeRuntimeEquipmentStateIfNeeded()
        {
            inventory = inventory != null ? inventory : GetComponent<UnitInventory>();
            RebuildLegacyAssignments();
        }

        public ItemInstance GetItemInstance(EquipmentSlotDefinition slot)
            => slot != null && equippedItems.TryGetValue(slot, out ItemInstance item) ? item : null;

        public EquippableDefinition GetEquipment(EquipmentSlotDefinition slot)
            => GetItemInstance(slot)?.Definition as EquippableDefinition;

        public EquippableDefinition GetEquipment(string slotId)
        {
            if (layout == null || string.IsNullOrWhiteSpace(slotId))
                return null;
            foreach (EquipmentSlotDefinition slot in layout.Slots)
                if (slot != null && slot.SlotId == slotId)
                    return GetEquipment(slot);
            return null;
        }

        public EquipmentSlotDefinition GetSlot(EquipmentSlotKind slotKind)
        {
            if (layout == null)
                return null;
            foreach (EquipmentSlotDefinition slot in layout.Slots)
                if (slot != null && slot.SlotKind == slotKind)
                    return slot;
            return null;
        }

        public EquipmentSlotDefinition GetSlot(EquipmentCategory category, int categoryIndex)
        {
            if (layout == null || categoryIndex < 0)
                return null;
            int foundIndex = 0;
            foreach (EquipmentSlotDefinition slot in layout.Slots)
            {
                if (slot == null || slot.Category != category)
                    continue;
                if (foundIndex++ == categoryIndex)
                    return slot;
            }
            return null;
        }

        public void ApplyUnitDefinitionDefaults(UnitDefinition definition)
        {
            inventory = inventory != null ? inventory : GetComponent<UnitInventory>();
            inventory?.ApplyUnitDefinitionDefaults(definition);
        }

        public bool TryEquipFromInventory(
            EquipmentSlotDefinition slot,
            ItemInstance item,
            out string failureReason)
        {
            inventory = inventory != null ? inventory : GetComponent<UnitInventory>();
            if (inventory == null || item == null || !inventory.Items.Contains(item))
                return Fail("장착할 아이템이 인벤토리에 없습니다.", out failureReason);
            if (!CanEquip(slot, item, out failureReason))
                return false;

            inventory.Remove(item);
            if (!TryEquipOwnedItemImmediately(slot, item, inventory, out failureReason))
            {
                inventory.AddReturnedEquipment(item);
                return false;
            }
            return true;
        }

        public bool TryEquipOwnedItemImmediately(
            EquipmentSlotDefinition slot,
            ItemInstance item,
            UnitInventory ownerInventory,
            out string failureReason)
        {
            if (!CanEquip(slot, item, out failureReason))
                return false;
            inventory = ownerInventory != null ? ownerInventory : inventory;

            if (item.Definition is WeaponDefinition weapon
                && weapon.Handedness == WeaponHandedness.TwoHanded)
            {
                ReturnSlotItemToInventory(GetSlot(EquipmentSlotKind.RightHand));
            }
            else if (slot.SlotKind == EquipmentSlotKind.RightHand)
            {
                EquipmentSlotDefinition leftSlot = GetSlot(EquipmentSlotKind.LeftHand);
                if (GetItemInstance(leftSlot)?.Definition is WeaponDefinition leftWeapon
                    && leftWeapon.Handedness == WeaponHandedness.TwoHanded)
                {
                    ReturnSlotItemToInventory(leftSlot);
                }
            }

            ReturnSlotItemToInventory(slot);
            equippedItems[slot] = item;
            InitializePassiveState(item);
            NotifyEquipmentChanged();
            failureReason = string.Empty;
            return true;
        }

        public bool TryUnequip(EquipmentSlotDefinition slot, out string failureReason)
        {
            ItemInstance item = GetItemInstance(slot);
            if (item == null)
                return Fail("해제할 장비가 없습니다.", out failureReason);
            equippedItems.Remove(slot);
            inventory?.AddReturnedEquipment(item);
            NotifyEquipmentChanged();
            failureReason = string.Empty;
            return true;
        }

        public bool TryDropEquipped(
            EquipmentSlotDefinition slot,
            out WorldItemPickup pickup,
            out string failureReason)
        {
            pickup = null;
            ItemInstance item = GetItemInstance(slot);
            Combatant owner = GetComponent<Combatant>();
            if (item == null || owner == null)
                return Fail("버릴 장비가 없습니다.", out failureReason);
            equippedItems.Remove(slot);
            pickup = WorldItemPickup.CreateDroppedItem(item, owner.GridMap, owner.CurrentCell);
            NotifyEquipmentChanged();
            failureReason = string.Empty;
            return pickup != null;
        }

        public int GetRemainingDurability(EquipmentSlotDefinition slot)
        {
            ItemInstance item = GetItemInstance(slot);
            return item != null ? Mathf.Max(0, Mathf.RoundToInt(item.Durability)) : 0;
        }

        public void InitializeForBattle()
        {
            battleInitialized = true;
            foreach (ItemInstance item in equippedItems.Values)
                InitializePassiveState(item);
        }

        public bool TryBlockDamage(
            out AdditionalEquipmentDefinition plate,
            out int remaining,
            out int maximum)
        {
            foreach (ItemInstance item in equippedItems.Values)
            {
                if (item?.Definition is not AdditionalEquipmentDefinition support
                    || support.PassiveKind != SupportEquipmentPassiveKind.RegeneratingBallisticPlate)
                {
                    continue;
                }
                maximum = support.MaximumPassiveCharges;
                remaining = passiveCharges.TryGetValue(item.InstanceId, out int value) ? value : maximum;
                if (remaining <= 0)
                    continue;
                remaining--;
                passiveCharges[item.InstanceId] = remaining;
                item.SetDurability(remaining);
                EquipmentSlotDefinition slot = FindSlotForItem(item);
                DurabilityChanged?.Invoke(slot, remaining, maximum);
                plate = support;
                return true;
            }
            plate = null;
            remaining = 0;
            maximum = 0;
            return false;
        }

        public bool TryGetArmorState(out ArmorDefinition armor, out int remaining, out int maximum)
        {
            foreach (ItemInstance item in equippedItems.Values)
            {
                if (item?.Definition is not ArmorDefinition equippedArmor)
                    continue;
                armor = equippedArmor;
                maximum = Mathf.Max(1, equippedArmor.MaximumBlockCount);
                remaining = maximum;
                return true;
            }
            armor = null;
            remaining = 0;
            maximum = 0;
            return false;
        }

        public bool TryGetBallisticPlateState(out int remaining, out int maximum, out float rechargeProgress)
        {
            foreach (ItemInstance item in equippedItems.Values)
            {
                if (item?.Definition is not AdditionalEquipmentDefinition support
                    || support.PassiveKind != SupportEquipmentPassiveKind.RegeneratingBallisticPlate)
                {
                    continue;
                }
                maximum = support.MaximumPassiveCharges;
                remaining = passiveCharges.TryGetValue(item.InstanceId, out int value) ? value : maximum;
                rechargeProgress = passiveRechargeProgress.TryGetValue(item.InstanceId, out float progress)
                    ? Mathf.Clamp01(progress / support.PassiveRechargeSeconds)
                    : 0f;
                return true;
            }
            remaining = 0;
            maximum = 0;
            rechargeProgress = 0f;
            return false;
        }

        public IEnumerable<CombatActionGrant> EnumerateCombatActionGrants()
        {
            foreach (KeyValuePair<EquipmentSlotDefinition, ItemInstance> pair in equippedItems)
            {
                ItemInstance item = pair.Value;
                if (item?.Definition == null)
                    continue;
                foreach (ItemActionGrant grant in item.Definition.ActionGrants)
                {
                    if (grant.Action != null && grant.Availability == ItemActionAvailability.Equipped)
                    {
                        yield return new CombatActionGrant(
                            $"item:{item.InstanceId}",
                            item.Definition.DisplayName,
                            grant.Action,
                            null);
                    }
                }
                if (item.Definition.ActionGrants.Count == 0
                    && item.Definition is AdditionalEquipmentDefinition additional)
                {
                    foreach (CombatActionDefinition action in additional.GrantedActions)
                    {
                        if (action != null)
                        {
                            yield return new CombatActionGrant(
                                $"item:{item.InstanceId}",
                                item.Definition.DisplayName,
                                action,
                                null);
                        }
                    }
                }
            }
        }

        public IEnumerable<CombatActionDefinition> EnumerateGrantedActions()
        {
            foreach (CombatActionGrant grant in EnumerateCombatActionGrants())
                yield return grant.Definition;
        }

        public string BuildAdditionalEquipmentSummary()
        {
            List<string> names = new();
            foreach (ItemInstance item in equippedItems.Values)
                if (item?.Definition is AdditionalEquipmentDefinition)
                    names.Add(item.Definition.DisplayName);
            return names.Count > 0 ? string.Join(", ", names) : "미장착";
        }

        public void ClearEquippedItems()
        {
            equippedItems.Clear();
            passiveCharges.Clear();
            passiveRechargeProgress.Clear();
            NotifyEquipmentChanged();
        }

        private bool CanEquip(
            EquipmentSlotDefinition slot,
            ItemInstance item,
            out string failureReason)
        {
            if (slot == null)
                return Fail("장비 슬롯이 없습니다.", out failureReason);
            if (item?.Definition is not EquippableDefinition equipment)
                return Fail("장착할 수 없는 아이템입니다.", out failureReason);

            if (!EquipmentSlotCompatibility.CanAssign(slot, equipment))
                return Fail($"{equipment.DisplayName}은(는) {slot.DisplayName} 슬롯에 장착할 수 없습니다.", out failureReason);
            failureReason = string.Empty;
            return true;
        }

        private void TickRegeneratingBallisticPlates(float deltaTime)
        {
            foreach (KeyValuePair<EquipmentSlotDefinition, ItemInstance> pair in equippedItems)
            {
                ItemInstance item = pair.Value;
                if (item?.Definition is not AdditionalEquipmentDefinition support
                    || support.PassiveKind != SupportEquipmentPassiveKind.RegeneratingBallisticPlate)
                {
                    continue;
                }
                int maximum = support.MaximumPassiveCharges;
                int remaining = passiveCharges.TryGetValue(item.InstanceId, out int value) ? value : maximum;
                if (remaining >= maximum)
                {
                    passiveRechargeProgress[item.InstanceId] = 0f;
                    continue;
                }
                float progress = passiveRechargeProgress.TryGetValue(item.InstanceId, out float current)
                    ? current + Mathf.Max(0f, deltaTime)
                    : Mathf.Max(0f, deltaTime);
                if (progress >= support.PassiveRechargeSeconds)
                {
                    progress -= support.PassiveRechargeSeconds;
                    remaining++;
                    passiveCharges[item.InstanceId] = remaining;
                    item.SetDurability(remaining);
                    DurabilityChanged?.Invoke(pair.Key, remaining, maximum);
                }
                passiveRechargeProgress[item.InstanceId] = progress;
            }
        }

        private void InitializePassiveState(ItemInstance item)
        {
            if (item?.Definition is not AdditionalEquipmentDefinition support
                || support.PassiveKind != SupportEquipmentPassiveKind.RegeneratingBallisticPlate)
            {
                return;
            }
            int charges = item.Durability > 0
                ? Mathf.Min(item.Durability, support.MaximumPassiveCharges)
                : support.MaximumPassiveCharges;
            passiveCharges[item.InstanceId] = charges;
            passiveRechargeProgress[item.InstanceId] = 0f;
            item.SetDurability(charges);
        }

        private void ReturnSlotItemToInventory(EquipmentSlotDefinition slot)
        {
            if (slot == null || !equippedItems.TryGetValue(slot, out ItemInstance item))
                return;
            equippedItems.Remove(slot);
            inventory?.AddReturnedEquipment(item);
        }

        private EquipmentSlotDefinition FindSlotForItem(ItemInstance item)
        {
            foreach (KeyValuePair<EquipmentSlotDefinition, ItemInstance> pair in equippedItems)
                if (ReferenceEquals(pair.Value, item))
                    return pair.Key;
            return null;
        }

        private void RebuildLegacyAssignments()
        {
            if (legacyAssignmentsBuilt)
                return;
            legacyAssignmentsBuilt = true;
            if (assignments == null || assignments.Length == 0)
                return;
            foreach (EquipmentSlotAssignment assignment in assignments)
            {
                if (assignment.Slot != null && assignment.Equipment != null)
                    equippedItems[assignment.Slot] = new ItemInstance(assignment.Equipment);
            }
        }

        private void NotifyEquipmentChanged()
        {
            WriteAssignmentsFromRuntime();
            EquipmentChanged?.Invoke();
            inventory?.NotifyEquipmentChanged();
        }

        private void WriteAssignmentsFromRuntime()
        {
            List<EquipmentSlotAssignment> values = new();
            if (layout != null)
            {
                foreach (EquipmentSlotDefinition slot in layout.Slots)
                {
                    values.Add(new EquipmentSlotAssignment(
                        slot,
                        GetItemInstance(slot)?.Definition as EquippableDefinition));
                }
            }
            assignments = values.ToArray();
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            EquipmentLayoutDefinition newLayout,
            EquipmentSlotAssignment[] newAssignments)
        {
            layout = newLayout;
            assignments = newAssignments ?? Array.Empty<EquipmentSlotAssignment>();
            equippedItems.Clear();
            legacyAssignmentsBuilt = false;
            RebuildLegacyAssignments();
        }
#endif
    }
}
