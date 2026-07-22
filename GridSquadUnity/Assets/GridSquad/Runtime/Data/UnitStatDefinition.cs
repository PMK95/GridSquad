using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum UnitStatModifierOperation
    {
        Add,
        Multiply
    }

    [CreateAssetMenu(menuName = "GridSquad/Stats/Unit Stat Definition", fileName = "UnitStatDefinition")]
    public sealed class UnitStatDefinition : ScriptableObject
    {
        [SerializeField] private string statId = "stat";
        [SerializeField] private string displayName = "스탯";
        [SerializeField] private float defaultValue;
        [SerializeField] private float minimumValue = float.MinValue;
        [SerializeField] private bool roundToInteger;
        [SerializeField] private int hudOrder;

        public string StatId => statId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float DefaultValue => defaultValue;
        public float MinimumValue => minimumValue;
        public bool RoundToInteger => roundToInteger;
        public int HudOrder => hudOrder;

        public float NormalizeValue(float value)
        {
            float normalized = Mathf.Max(minimumValue, value);
            return roundToInteger ? Mathf.Round(normalized) : normalized;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newStatId,
            string newDisplayName,
            float newDefaultValue,
            float newMinimumValue,
            bool newRoundToInteger,
            int newHudOrder)
        {
            statId = newStatId;
            displayName = newDisplayName;
            defaultValue = newDefaultValue;
            minimumValue = newMinimumValue;
            roundToInteger = newRoundToInteger;
            hudOrder = newHudOrder;
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

    public readonly struct UnitRuntimeStatEntry
    {
        public readonly UnitStatDefinition Definition;
        public readonly float Value;

        public UnitRuntimeStatEntry(UnitStatDefinition definition, float value)
        {
            Definition = definition;
            Value = value;
        }
    }

    public sealed class UnitRuntimeStatCollection
    {
        private readonly Dictionary<UnitStatDefinition, float> values = new();
        private readonly List<UnitRuntimeStatEntry> entries = new();

        public IReadOnlyList<UnitRuntimeStatEntry> Entries => entries;

        public void Rebuild(
            UnitStatCatalog catalog,
            IReadOnlyList<UnitStatValue> baseValues,
            IReadOnlyList<UnitTraitDefinition> traits)
        {
            values.Clear();
            entries.Clear();

            if (catalog != null)
            {
                foreach (UnitStatDefinition definition in catalog.CoreStats)
                {
                    if (definition != null)
                        values[definition] = definition.DefaultValue;
                }
            }

            if (baseValues != null)
            {
                foreach (UnitStatValue statValue in baseValues)
                {
                    if (statValue.Definition != null)
                        values[statValue.Definition] = statValue.Value;
                }
            }

            if (traits != null)
            {
                foreach (UnitTraitDefinition trait in traits)
                {
                    if (trait == null)
                        continue;
                    ApplyModifiers(trait.Modifiers);
                }
            }

            foreach (UnitStatDefinition definition in new List<UnitStatDefinition>(values.Keys))
            {
                values[definition] = definition.NormalizeValue(values[definition]);
                entries.Add(new UnitRuntimeStatEntry(definition, values[definition]));
            }
            entries.Sort((left, right) =>
                left.Definition.HudOrder.CompareTo(right.Definition.HudOrder));
        }

        public float GetValue(UnitStatDefinition definition)
        {
            if (definition == null)
                return 0f;
            return values.TryGetValue(definition, out float value)
                ? value
                : definition.NormalizeValue(definition.DefaultValue);
        }

        private void ApplyModifiers(IReadOnlyList<UnitStatModifier> modifiers)
        {
            if (modifiers == null)
                return;
            foreach (UnitStatModifier modifier in modifiers)
            {
                UnitStatDefinition definition = modifier.Definition;
                if (definition == null)
                    continue;
                float current = values.TryGetValue(definition, out float value)
                    ? value
                    : definition.DefaultValue;
                values[definition] = modifier.Operation == UnitStatModifierOperation.Multiply
                    ? current * modifier.Value
                    : current + modifier.Value;
            }
        }
    }
}
