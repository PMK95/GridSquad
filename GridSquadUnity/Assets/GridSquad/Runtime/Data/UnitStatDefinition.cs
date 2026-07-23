using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum UnitStatCategory
    {
        Survivability,
        Offense,
        Mobility,
        Utility
    }

    public enum UnitStatDisplayFormat
    {
        Integer,
        Decimal,
        PercentMultiplier,
        PercentagePoints,
        Kilograms
    }

    public enum UnitStatModifierOperation
    {
        Add,
        Multiply
    }

    public enum UnitStatModifierSourceKind
    {
        Trait,
        Equipment,
        Action,
        Status
    }

    [CreateAssetMenu(menuName = "GridSquad/Stats/Unit Stat Definition", fileName = "UnitStatDefinition")]
    public sealed class UnitStatDefinition : ScriptableObject
    {
        [SerializeField] private string statId = "stat";
        [SerializeField] private string displayName = "스탯";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private float defaultValue;
        [SerializeField] private float minimumValue = float.MinValue;
        [SerializeField] private bool roundToInteger;
        [SerializeField] private int hudOrder;
        [SerializeField] private UnitStatCategory category;
        [SerializeField] private UnitStatDisplayFormat displayFormat;

        public string StatId => statId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public float DefaultValue => defaultValue;
        public float MinimumValue => minimumValue;
        public bool RoundToInteger => roundToInteger;
        public int HudOrder => hudOrder;
        public UnitStatCategory Category => category;
        public UnitStatDisplayFormat DisplayFormat => displayFormat;

        public float NormalizeValue(float value)
        {
            float normalized = Mathf.Max(minimumValue, value);
            return roundToInteger ? Mathf.Round(normalized) : normalized;
        }

        public string FormatValue(float value)
        {
            return displayFormat switch
            {
                UnitStatDisplayFormat.Integer => Mathf.RoundToInt(value).ToString(),
                UnitStatDisplayFormat.PercentMultiplier => $"{value * 100f:0.#}%",
                UnitStatDisplayFormat.PercentagePoints => $"{value:0.#}%p",
                UnitStatDisplayFormat.Kilograms => $"{value:0.##}kg",
                _ => value.ToString("0.##")
            };
        }

        public string FormatModifier(UnitStatModifierOperation operation, float value)
        {
            if (operation == UnitStatModifierOperation.Multiply)
                return $"×{value:0.##}";

            return displayFormat switch
            {
                UnitStatDisplayFormat.Integer => $"{Mathf.RoundToInt(value):+0;-0;0}",
                UnitStatDisplayFormat.PercentMultiplier => $"{value * 100f:+0.#;-0.#;0}%",
                UnitStatDisplayFormat.PercentagePoints => $"{value:+0.#;-0.#;0}%p",
                UnitStatDisplayFormat.Kilograms => $"{value:+0.##;-0.##;0}kg",
                _ => $"{value:+0.##;-0.##;0}"
            };
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newStatId,
            string newDisplayName,
            float newDefaultValue,
            float newMinimumValue,
            bool newRoundToInteger,
            int newHudOrder,
            string newDescription = null,
            UnitStatCategory newCategory = UnitStatCategory.Survivability,
            UnitStatDisplayFormat newDisplayFormat = UnitStatDisplayFormat.Decimal)
        {
            statId = newStatId;
            displayName = newDisplayName;
            defaultValue = newDefaultValue;
            minimumValue = newMinimumValue;
            roundToInteger = newRoundToInteger;
            hudOrder = newHudOrder;
            if (newDescription != null)
                description = newDescription;
            category = newCategory;
            displayFormat = newDisplayFormat;
        }
#endif
    }

    [Serializable]
    public struct UnitStatValue
    {
        [SerializeField] private UnitStatDefinition definition;
        [SerializeField] private float value;

        public UnitStatValue(UnitStatDefinition definition, float value)
        {
            this.definition = definition;
            this.value = value;
        }

        public UnitStatDefinition Definition => definition;
        public float Value => value;
    }

    [Serializable]
    public struct UnitStatModifier
    {
        [SerializeField] private UnitStatDefinition definition;
        [SerializeField] private UnitStatModifierOperation operation;
        [SerializeField] private float value;

        public UnitStatModifier(
            UnitStatDefinition definition,
            UnitStatModifierOperation operation,
            float value)
        {
            this.definition = definition;
            this.operation = operation;
            this.value = value;
        }

        public UnitStatDefinition Definition => definition;
        public UnitStatModifierOperation Operation => operation;
        public float Value => value;
    }

    public readonly struct UnitStatModifierHandle
    {
        public readonly int Value;

        internal UnitStatModifierHandle(int value)
        {
            Value = value;
        }

        public bool IsValid => Value > 0;
    }

    public readonly struct UnitStatContribution
    {
        public readonly string SourceKey;
        public readonly string SourceDisplayName;
        public readonly Sprite SourceIcon;
        public readonly UnitStatModifierSourceKind SourceKind;
        public readonly UnitStatModifierOperation Operation;
        public readonly float Value;
        public readonly UnitStatModifierHandle RuntimeHandle;

        internal UnitStatContribution(
            string sourceKey,
            string sourceDisplayName,
            Sprite sourceIcon,
            UnitStatModifierSourceKind sourceKind,
            UnitStatModifierOperation operation,
            float value,
            UnitStatModifierHandle runtimeHandle)
        {
            SourceKey = sourceKey;
            SourceDisplayName = sourceDisplayName;
            SourceIcon = sourceIcon;
            SourceKind = sourceKind;
            Operation = operation;
            Value = value;
            RuntimeHandle = runtimeHandle;
        }
    }

    public readonly struct UnitRuntimeStatEntry
    {
        public readonly UnitStatDefinition Definition;
        public readonly float BaseValue;
        public readonly float Value;
        public readonly IReadOnlyList<UnitStatContribution> Contributions;

        public UnitRuntimeStatEntry(
            UnitStatDefinition definition,
            float baseValue,
            float value,
            IReadOnlyList<UnitStatContribution> contributions)
        {
            Definition = definition;
            BaseValue = baseValue;
            Value = value;
            Contributions = contributions ?? Array.Empty<UnitStatContribution>();
        }
    }

    internal sealed class UnitStatModifierSourceRecord
    {
        public UnitStatModifierHandle Handle;
        public string SourceKey;
        public string SourceDisplayName;
        public Sprite SourceIcon;
        public UnitStatModifierSourceKind SourceKind;
        public UnitStatModifier[] Modifiers;
        public bool IsTimed;
        public float RemainingSeconds;
    }

    public sealed class UnitRuntimeStatCollection
    {
        private readonly Dictionary<UnitStatDefinition, float> baseValues = new();
        private readonly Dictionary<UnitStatDefinition, float> values = new();
        private readonly Dictionary<UnitStatDefinition, UnitRuntimeStatEntry> entryByDefinition = new();
        private readonly List<UnitRuntimeStatEntry> entries = new();
        private readonly List<UnitStatModifierSourceRecord> staticSources = new();
        private readonly List<UnitStatModifierSourceRecord> runtimeSources = new();
        private UnitStatCatalog catalog;
        private int nextRuntimeHandle = 1;

        public IReadOnlyList<UnitRuntimeStatEntry> Entries => entries;

        public void Rebuild(
            UnitStatCatalog catalog,
            IReadOnlyList<UnitStatValue> baseValues,
            IReadOnlyList<UnitTraitDefinition> traits,
            EquipmentLoadout equipmentLoadout)
        {
            this.catalog = catalog;
            this.baseValues.Clear();
            staticSources.Clear();

            if (catalog != null)
            {
                foreach (UnitStatDefinition definition in catalog.CoreStats)
                {
                    if (definition != null)
                        this.baseValues[definition] = definition.DefaultValue;
                }
            }

            if (baseValues != null)
            {
                foreach (UnitStatValue statValue in baseValues)
                {
                    if (statValue.Definition != null)
                        this.baseValues[statValue.Definition] = statValue.Value;
                }
            }

            if (traits != null)
            {
                foreach (UnitTraitDefinition trait in traits)
                {
                    if (trait == null)
                        continue;
                    staticSources.Add(CreateSourceRecord(
                        default,
                        $"trait:{trait.name}",
                        trait.DisplayName,
                        trait.Icon,
                        UnitStatModifierSourceKind.Trait,
                        trait.Modifiers,
                        false,
                        -1f));
                }
            }

            if (equipmentLoadout != null)
            {
                foreach (KeyValuePair<EquipmentSlotDefinition, ItemInstance> pair
                    in equipmentLoadout.EnumerateEquippedItems())
                {
                    ItemInstance item = pair.Value;
                    if (item?.Definition is not EquippableDefinition equipment
                        || item.Durability <= 0)
                        continue;

                    List<UnitStatModifier> modifiers = new();
                    if (equipment is ArmorDefinition armor
                        && catalog?.Defense != null
                        && armor.Defense > 0)
                    {
                        modifiers.Add(new UnitStatModifier(
                            catalog.Defense,
                            UnitStatModifierOperation.Add,
                            armor.Defense));
                    }
                    foreach (UnitStatModifier modifier in equipment.StatModifiers)
                        modifiers.Add(modifier);
                    if (modifiers.Count == 0)
                        continue;

                    staticSources.Add(CreateSourceRecord(
                        default,
                        $"equipment:{item.InstanceId}",
                        equipment.DisplayName,
                        equipment.Icon,
                        UnitStatModifierSourceKind.Equipment,
                        modifiers,
                        false,
                        -1f));
                }
            }

            RecalculateEffectiveValues();
        }

        public float GetValue(UnitStatDefinition definition)
        {
            if (definition == null)
                return 0f;
            return values.TryGetValue(definition, out float value)
                ? value
                : definition.NormalizeValue(definition.DefaultValue);
        }

        public bool TryGetEntry(
            UnitStatDefinition definition,
            out UnitRuntimeStatEntry entry)
        {
            if (definition != null && entryByDefinition.TryGetValue(definition, out entry))
                return true;
            entry = default;
            return false;
        }

        public UnitStatModifierHandle AddTimedModifiers(
            string sourceKey,
            string sourceDisplayName,
            Sprite sourceIcon,
            UnitStatModifierSourceKind sourceKind,
            IReadOnlyList<UnitStatModifier> modifiers,
            float durationSeconds)
        {
            if (modifiers == null || modifiers.Count == 0 || durationSeconds <= 0f)
                return default;

            foreach (UnitStatModifierSourceRecord source in runtimeSources)
            {
                if (!source.IsTimed || source.SourceKey != sourceKey)
                    continue;
                source.SourceDisplayName = sourceDisplayName;
                source.SourceIcon = sourceIcon;
                source.SourceKind = sourceKind;
                source.Modifiers = CopyModifiers(modifiers);
                source.RemainingSeconds = durationSeconds;
                RecalculateEffectiveValues();
                return source.Handle;
            }

            UnitStatModifierHandle handle = CreateRuntimeHandle();
            runtimeSources.Add(CreateSourceRecord(
                handle,
                sourceKey,
                sourceDisplayName,
                sourceIcon,
                sourceKind,
                modifiers,
                true,
                durationSeconds));
            RecalculateEffectiveValues();
            return handle;
        }

        public UnitStatModifierHandle AddPersistentModifiers(
            string sourceKey,
            string sourceDisplayName,
            Sprite sourceIcon,
            UnitStatModifierSourceKind sourceKind,
            IReadOnlyList<UnitStatModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return default;
            UnitStatModifierHandle handle = CreateRuntimeHandle();
            runtimeSources.Add(CreateSourceRecord(
                handle,
                $"{sourceKey}:{handle.Value}",
                sourceDisplayName,
                sourceIcon,
                sourceKind,
                modifiers,
                false,
                -1f));
            RecalculateEffectiveValues();
            return handle;
        }

        public bool RemoveModifiers(UnitStatModifierHandle handle)
        {
            if (!handle.IsValid)
                return false;
            for (int index = runtimeSources.Count - 1; index >= 0; index--)
            {
                if (runtimeSources[index].Handle.Value != handle.Value)
                    continue;
                runtimeSources.RemoveAt(index);
                RecalculateEffectiveValues();
                return true;
            }
            return false;
        }

        public float GetRemainingSeconds(UnitStatModifierHandle handle)
        {
            if (!handle.IsValid)
                return -1f;
            foreach (UnitStatModifierSourceRecord source in runtimeSources)
            {
                if (source.Handle.Value == handle.Value)
                    return source.IsTimed ? Mathf.Max(0f, source.RemainingSeconds) : -1f;
            }
            return -1f;
        }

        public bool TickTimedModifiers(float deltaTime)
        {
            bool removed = false;
            for (int index = runtimeSources.Count - 1; index >= 0; index--)
            {
                UnitStatModifierSourceRecord source = runtimeSources[index];
                if (!source.IsTimed)
                    continue;
                source.RemainingSeconds = Mathf.Max(
                    0f,
                    source.RemainingSeconds - Mathf.Max(0f, deltaTime));
                if (source.RemainingSeconds > 0f)
                    continue;
                runtimeSources.RemoveAt(index);
                removed = true;
            }
            if (removed)
                RecalculateEffectiveValues();
            return removed;
        }

        public bool ClearTimedModifiers()
        {
            bool removed = false;
            for (int index = runtimeSources.Count - 1; index >= 0; index--)
            {
                if (!runtimeSources[index].IsTimed)
                    continue;
                runtimeSources.RemoveAt(index);
                removed = true;
            }
            if (removed)
                RecalculateEffectiveValues();
            return removed;
        }

        private void RecalculateEffectiveValues()
        {
            values.Clear();
            entries.Clear();
            entryByDefinition.Clear();

            Dictionary<UnitStatDefinition, float> additiveValues = new();
            Dictionary<UnitStatDefinition, float> multiplicativeValues = new();
            Dictionary<UnitStatDefinition, List<UnitStatContribution>> contributions = new();
            foreach (KeyValuePair<UnitStatDefinition, float> pair in baseValues)
                values[pair.Key] = pair.Value;

            foreach (UnitStatModifierSourceRecord source in staticSources)
                AccumulateSource(source, additiveValues, multiplicativeValues, contributions);
            foreach (UnitStatModifierSourceRecord source in runtimeSources)
                AccumulateSource(source, additiveValues, multiplicativeValues, contributions);

            foreach (UnitStatDefinition definition in new List<UnitStatDefinition>(values.Keys))
            {
                float baseValue = values[definition];
                float add = additiveValues.TryGetValue(definition, out float additive)
                    ? additive
                    : 0f;
                float multiply = multiplicativeValues.TryGetValue(definition, out float multiplier)
                    ? multiplier
                    : 1f;
                float effectiveValue = definition.NormalizeValue((baseValue + add) * multiply);
                values[definition] = effectiveValue;
                IReadOnlyList<UnitStatContribution> statContributions =
                    contributions.TryGetValue(definition, out List<UnitStatContribution> found)
                        ? found
                        : Array.Empty<UnitStatContribution>();
                found?.Sort((left, right) =>
                {
                    int kindOrder = left.SourceKind.CompareTo(right.SourceKind);
                    return kindOrder != 0
                        ? kindOrder
                        : string.CompareOrdinal(
                            left.SourceDisplayName,
                            right.SourceDisplayName);
                });
                UnitRuntimeStatEntry entry = new(
                    definition,
                    baseValue,
                    effectiveValue,
                    statContributions);
                entries.Add(entry);
                entryByDefinition[definition] = entry;
            }
            entries.Sort((left, right) =>
            {
                int categoryOrder = left.Definition.Category.CompareTo(right.Definition.Category);
                return categoryOrder != 0
                    ? categoryOrder
                    : left.Definition.HudOrder.CompareTo(right.Definition.HudOrder);
            });
        }

        private void AccumulateSource(
            UnitStatModifierSourceRecord source,
            Dictionary<UnitStatDefinition, float> additiveValues,
            Dictionary<UnitStatDefinition, float> multiplicativeValues,
            Dictionary<UnitStatDefinition, List<UnitStatContribution>> contributions)
        {
            if (source?.Modifiers == null)
                return;
            foreach (UnitStatModifier modifier in source.Modifiers)
            {
                UnitStatDefinition definition = modifier.Definition;
                if (definition == null)
                    continue;
                if (!values.ContainsKey(definition))
                    values[definition] = definition.DefaultValue;
                if (modifier.Operation == UnitStatModifierOperation.Multiply)
                {
                    float current = multiplicativeValues.TryGetValue(definition, out float value)
                        ? value
                        : 1f;
                    multiplicativeValues[definition] = current * modifier.Value;
                }
                else
                {
                    float current = additiveValues.TryGetValue(definition, out float value)
                        ? value
                        : 0f;
                    additiveValues[definition] = current + modifier.Value;
                }

                if (!contributions.TryGetValue(
                        definition,
                        out List<UnitStatContribution> statContributions))
                {
                    statContributions = new List<UnitStatContribution>();
                    contributions.Add(definition, statContributions);
                }
                statContributions.Add(new UnitStatContribution(
                    source.SourceKey,
                    source.SourceDisplayName,
                    source.SourceIcon,
                    source.SourceKind,
                    modifier.Operation,
                    modifier.Value,
                    source.Handle));
            }
        }

        private UnitStatModifierHandle CreateRuntimeHandle()
        {
            if (nextRuntimeHandle <= 0)
                nextRuntimeHandle = 1;
            return new UnitStatModifierHandle(nextRuntimeHandle++);
        }

        private static UnitStatModifierSourceRecord CreateSourceRecord(
            UnitStatModifierHandle handle,
            string sourceKey,
            string sourceDisplayName,
            Sprite sourceIcon,
            UnitStatModifierSourceKind sourceKind,
            IReadOnlyList<UnitStatModifier> modifiers,
            bool isTimed,
            float remainingSeconds)
        {
            return new UnitStatModifierSourceRecord
            {
                Handle = handle,
                SourceKey = sourceKey ?? string.Empty,
                SourceDisplayName = sourceDisplayName ?? string.Empty,
                SourceIcon = sourceIcon,
                SourceKind = sourceKind,
                Modifiers = CopyModifiers(modifiers),
                IsTimed = isTimed,
                RemainingSeconds = remainingSeconds
            };
        }

        private static UnitStatModifier[] CopyModifiers(
            IReadOnlyList<UnitStatModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return Array.Empty<UnitStatModifier>();
            UnitStatModifier[] copy = new UnitStatModifier[modifiers.Count];
            for (int index = 0; index < modifiers.Count; index++)
                copy[index] = modifiers[index];
            return copy;
        }
    }
}
