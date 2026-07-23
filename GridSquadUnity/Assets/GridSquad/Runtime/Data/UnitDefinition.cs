using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [Serializable]
    public struct StartingInventoryItem
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField, Min(1)] private int quantity;

        public StartingInventoryItem(ItemDefinition definition, int quantity)
        {
            this.definition = definition;
            this.quantity = Mathf.Max(1, quantity);
        }

        public ItemDefinition Definition => definition;
        public int Quantity => Mathf.Max(1, quantity);
    }

    [CreateAssetMenu(menuName = "GridSquad/Unit Definition", fileName = "UnitDefinition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        private const int WeaponSlotCount = 2;

        [Header("저장 식별자")]
        [SerializeField] private string unitId = "unit";

        [Header("표시 정보")]
        [SerializeField] private string displayName = "대원";
        [SerializeField] private string roleName = "미지정";
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color accentColor = new(0.2f, 0.75f, 0.9f, 1f);

        [Header("기본 능력치")]
        [SerializeField] private UnitStatValue[] baseStatValues = Array.Empty<UnitStatValue>();

        [Header("기본 전투 구성")]
        [SerializeField] private WeaponDefinition[] defaultWeapons = new WeaponDefinition[WeaponSlotCount];
        [SerializeField] private CombatActionDefinition[] actionDefinitions = Array.Empty<CombatActionDefinition>();
        [SerializeField] private UnitTraitDefinition[] traits = Array.Empty<UnitTraitDefinition>();
        [SerializeField] private EquipmentSlotAssignment[] defaultEquipmentAssignments = Array.Empty<EquipmentSlotAssignment>();
        [SerializeField] private StartingInventoryItem[] startingInventoryItems = Array.Empty<StartingInventoryItem>();

        public string UnitId => unitId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string RoleName => roleName;
        public string Description => description;
        public Sprite Portrait => portrait;
        public Color AccentColor => accentColor;
        public IReadOnlyList<UnitStatValue> BaseStatValues => baseStatValues;
        public IReadOnlyList<WeaponDefinition> DefaultWeapons => defaultWeapons;
        public IReadOnlyList<CombatActionDefinition> ActionDefinitions => actionDefinitions;
        public IReadOnlyList<UnitTraitDefinition> Traits => traits;
        public IReadOnlyList<EquipmentSlotAssignment> DefaultEquipmentAssignments => defaultEquipmentAssignments;
        public IReadOnlyList<StartingInventoryItem> StartingInventoryItems => startingInventoryItems;

        public WeaponDefinition GetDefaultWeapon(int slotIndex)
        {
            return defaultWeapons != null
                && slotIndex >= 0
                && slotIndex < defaultWeapons.Length
                    ? defaultWeapons[slotIndex]
                    : null;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newDisplayName,
            string newRoleName,
            string newDescription,
            Sprite newPortrait,
            Color newAccentColor,
            UnitStatValue[] newBaseStatValues,
            WeaponDefinition firstWeapon,
            WeaponDefinition secondWeapon,
            CombatActionDefinition[] newActionDefinitions,
            UnitTraitDefinition[] newTraits)
        {
            if (string.IsNullOrWhiteSpace(unitId) || unitId == "unit")
                unitId = name;
            displayName = newDisplayName;
            roleName = newRoleName;
            description = newDescription;
            portrait = newPortrait;
            accentColor = newAccentColor;
            baseStatValues = newBaseStatValues ?? Array.Empty<UnitStatValue>();
            defaultWeapons = new[] { firstWeapon, secondWeapon };
            actionDefinitions = newActionDefinitions ?? Array.Empty<CombatActionDefinition>();
            traits = newTraits ?? Array.Empty<UnitTraitDefinition>();
        }

        public void SetEditorDefaultEquipment(EquipmentSlotAssignment[] assignments)
            => defaultEquipmentAssignments = assignments ?? Array.Empty<EquipmentSlotAssignment>();

        public void SetEditorStartingInventory(StartingInventoryItem[] newStartingItems)
            => startingInventoryItems = newStartingItems ?? Array.Empty<StartingInventoryItem>();

        public void SetEditorUnitId(string newUnitId)
            => unitId = newUnitId;
#endif
    }
}
