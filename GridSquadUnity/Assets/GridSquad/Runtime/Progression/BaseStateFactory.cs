using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class BaseStateFactory
    {
        public BaseState Create(GameContentCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            BaseState state = new();
            foreach (UnitDefinition definition in catalog.Units)
            {
                if (definition == null)
                    continue;
                UnitState unit = new(
                    Guid.NewGuid().ToString("N"),
                    definition.UnitId);
                AddDefaultEquipment(definition, unit);
                AddStartingInventory(definition, unit);
                state.AddUnit(unit);
            }
            return state;
        }

        private static void AddDefaultEquipment(UnitDefinition definition, UnitState unit)
        {
            HashSet<ItemDefinition> assignedDefinitions = new();
            foreach (EquipmentSlotAssignment assignment in definition.DefaultEquipmentAssignments)
            {
                if (assignment.Slot == null || assignment.Equipment == null)
                    continue;
                ItemState item = CreateItemState(assignment.Equipment, 1);
                unit.AddItem(item);
                unit.AddEquipment(new EquipmentSlotState(
                    assignment.Slot.SlotId,
                    item.ItemInstanceId));
                assignedDefinitions.Add(assignment.Equipment);
            }

            for (int index = 0; index < definition.DefaultWeapons.Count; index++)
            {
                WeaponDefinition weapon = definition.GetDefaultWeapon(index);
                if (weapon != null && !assignedDefinitions.Contains(weapon))
                    unit.AddItem(CreateItemState(weapon, 1));
            }
        }

        private static void AddStartingInventory(UnitDefinition definition, UnitState unit)
        {
            foreach (StartingInventoryItem startingItem in definition.StartingInventoryItems)
            {
                if (startingItem.Definition != null)
                    unit.AddItem(CreateItemState(startingItem.Definition, startingItem.Quantity));
            }
        }

        private static ItemState CreateItemState(ItemDefinition definition, int quantity)
        {
            int durability = definition is EquippableDefinition equipment
                ? equipment.MaximumDurability
                : 0;
            int magazineAmmo = definition is WeaponDefinition weapon
                ? weapon.MagazineCapacity
                : 0;
            int reserveAmmo = definition is WeaponDefinition ammunitionWeapon
                ? ammunitionWeapon.StartingReserveAmmo
                : 0;
            return new ItemState(
                Guid.NewGuid().ToString("N"),
                definition.ItemId,
                quantity,
                durability,
                magazineAmmo,
                reserveAmmo);
        }
    }

    public sealed class BaseUnitStatCalculator
    {
        private readonly GameContentCatalog catalog;

        public BaseUnitStatCalculator(GameContentCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int GetMaximumHealth(UnitState state)
        {
            UnitStatDefinition definition = catalog.StatCatalog?.MaximumHealth;
            return Mathf.Max(1, Mathf.RoundToInt(CalculateStat(state, definition, 100f)));
        }

        public float GetTraumaResistance(UnitState state)
        {
            UnitStatDefinition definition = catalog.StatCatalog?.TraumaResistance;
            return CalculateStat(state, definition, 0f);
        }

        private float CalculateStat(
            UnitState state,
            UnitStatDefinition statDefinition,
            float fallback)
        {
            if (state == null || statDefinition == null)
                return fallback;
            UnitDefinition unit = catalog.GetRequiredUnit(state.UnitDefinitionId);
            float baseValue = statDefinition.DefaultValue;
            foreach (UnitStatValue value in unit.BaseStatValues)
                if (value.Definition == statDefinition)
                    baseValue = value.Value;

            float additive = 0f;
            float multiplier = 1f;
            foreach (UnitTraitDefinition trait in unit.Traits)
                ApplyModifiers(trait?.Modifiers, statDefinition, ref additive, ref multiplier);
            foreach (EquipmentSlotState slot in state.Equipment)
            {
                ItemState item = state.FindItem(slot.ItemInstanceId);
                if (item == null || item.IsBroken)
                    continue;
                if (catalog.GetRequiredItem(item.ItemDefinitionId) is EquippableDefinition equipment)
                {
                    ApplyModifiers(
                        equipment.StatModifiers,
                        statDefinition,
                        ref additive,
                        ref multiplier);
                }
            }
            foreach (AftereffectState aftereffect in state.Aftereffects)
            {
                AftereffectDefinition definition = FindAftereffect(aftereffect.AftereffectId);
                ApplyModifiers(
                    definition?.StatModifiers,
                    statDefinition,
                    ref additive,
                    ref multiplier);
            }
            return statDefinition.NormalizeValue((baseValue + additive) * multiplier);
        }

        private AftereffectDefinition FindAftereffect(string aftereffectId)
        {
            AftereffectRuleSet rules = catalog.AftereffectRules;
            if (rules == null)
                return null;
            foreach (AftereffectDefinition definition in rules.Definitions)
                if (definition != null && definition.AftereffectId == aftereffectId)
                    return definition;
            return null;
        }

        private static void ApplyModifiers(
            IReadOnlyList<UnitStatModifier> modifiers,
            UnitStatDefinition target,
            ref float additive,
            ref float multiplier)
        {
            if (modifiers == null)
                return;
            foreach (UnitStatModifier modifier in modifiers)
            {
                if (modifier.Definition != target)
                    continue;
                if (modifier.Operation == UnitStatModifierOperation.Multiply)
                    multiplier *= modifier.Value;
                else
                    additive += modifier.Value;
            }
        }
    }
}
