using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [Serializable]
    public struct UnitStatBlock
    {
        [Min(1)] public int MaximumHealth;
        [Min(0.1f)] public float MovementSpeedMultiplier;
        public float HitChanceBonusPercent;
        [Min(0.1f)] public float DamageMultiplier;

        public UnitStatBlock(
            int maximumHealth,
            float movementSpeedMultiplier,
            float hitChanceBonusPercent,
            float damageMultiplier)
        {
            MaximumHealth = Mathf.Max(1, maximumHealth);
            MovementSpeedMultiplier = Mathf.Max(0.1f, movementSpeedMultiplier);
            HitChanceBonusPercent = hitChanceBonusPercent;
            DamageMultiplier = Mathf.Max(0.1f, damageMultiplier);
        }
    }

    [CreateAssetMenu(menuName = "GridSquad/Unit Definition", fileName = "UnitDefinition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        private const int WeaponSlotCount = 2;

        [Header("표시 정보")]
        [SerializeField] private string displayName = "유닛";
        [SerializeField] private string roleName = "전투원";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color accentColor = new(0.2f, 0.75f, 0.9f, 1f);

        [Header("기본 스탯")]
        [SerializeField] private UnitStatBlock baseStats = new(500, 1f, 0f, 1f);

        [Header("기본 로드아웃")]
        [SerializeField] private WeaponDefinition[] defaultWeapons = new WeaponDefinition[WeaponSlotCount];
        [SerializeField] private CombatActionDefinition[] actionDefinitions = Array.Empty<CombatActionDefinition>();
        [SerializeField] private UnitTraitDefinition[] traits = Array.Empty<UnitTraitDefinition>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string RoleName => roleName;
        public string Description => description;
        public Sprite Portrait => portrait;
        public Color AccentColor => accentColor;
        public UnitStatBlock BaseStats => baseStats;
        public IReadOnlyList<WeaponDefinition> DefaultWeapons => defaultWeapons;
        public IReadOnlyList<CombatActionDefinition> ActionDefinitions => actionDefinitions;
        public IReadOnlyList<UnitTraitDefinition> Traits => traits;

        public WeaponDefinition GetDefaultWeapon(int slotIndex)
        {
            return defaultWeapons != null
                && slotIndex >= 0
                && slotIndex < defaultWeapons.Length
                    ? defaultWeapons[slotIndex]
                    : null;
        }

        public UnitStatBlock CalculateEffectiveStats()
        {
            int maximumHealth = baseStats.MaximumHealth;
            float movementSpeedMultiplier = baseStats.MovementSpeedMultiplier;
            float hitChanceBonusPercent = baseStats.HitChanceBonusPercent;
            float damageMultiplier = baseStats.DamageMultiplier;

            if (traits != null)
            {
                foreach (UnitTraitDefinition trait in traits)
                {
                    if (trait == null)
                        continue;

                    maximumHealth += trait.MaximumHealthDelta;
                    movementSpeedMultiplier += trait.MovementSpeedMultiplierDelta;
                    hitChanceBonusPercent += trait.HitChanceBonusPercent;
                    damageMultiplier += trait.DamageMultiplierDelta;
                }
            }

            return new UnitStatBlock(
                maximumHealth,
                movementSpeedMultiplier,
                hitChanceBonusPercent,
                damageMultiplier);
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newDisplayName,
            string newRoleName,
            string newDescription,
            Sprite newPortrait,
            Color newAccentColor,
            UnitStatBlock newBaseStats,
            WeaponDefinition firstWeapon,
            WeaponDefinition secondWeapon,
            CombatActionDefinition[] newActionDefinitions,
            UnitTraitDefinition[] newTraits)
        {
            displayName = newDisplayName;
            roleName = newRoleName;
            description = newDescription;
            portrait = newPortrait;
            accentColor = newAccentColor;
            baseStats = newBaseStats;
            defaultWeapons = new[] { firstWeapon, secondWeapon };
            actionDefinitions = newActionDefinitions ?? Array.Empty<CombatActionDefinition>();
            traits = newTraits ?? Array.Empty<UnitTraitDefinition>();
        }
#endif
    }
}
