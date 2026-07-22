using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GridSquad
{
    public enum EquipmentCategory
    {
        Weapon,
        Armor,
        Support,
        Additional = Support,
        OffHand
    }

    public enum EquipmentSlotKind
    {
        LeftHand,
        RightHand,
        Head,
        Torso,
        Legs,
        Hands,
        Feet,
        SupportOne,
        SupportTwo
    }

    public enum WeaponHandedness
    {
        OneHanded,
        TwoHanded
    }

    public enum ItemActionAvailability
    {
        Equipped,
        Carried
    }

    [Serializable]
    public struct ItemActionGrant
    {
        [SerializeField] private CombatActionDefinition action;
        [SerializeField] private ItemActionAvailability availability;

        public ItemActionGrant(
            CombatActionDefinition action,
            ItemActionAvailability availability)
        {
            this.action = action;
            this.availability = availability;
        }

        public CombatActionDefinition Action => action;
        public ItemActionAvailability Availability => availability;
    }

    public abstract class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string equipmentId = "equipment";
        [FormerlySerializedAs("DisplayName")]
        [SerializeField] private string displayName = "장비";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(1)] private int maximumStack = 1;
        [SerializeField] private ItemActionGrant[] actionGrants = Array.Empty<ItemActionGrant>();

        public string EquipmentId => equipmentId;
        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
            set => displayName = value;
        }
        public string Description => description;
        public Sprite Icon => icon;
        public float Weight => Mathf.Max(0f, weight);
        public int MaximumStack => Mathf.Max(1, maximumStack);
        public bool IsStackable => MaximumStack > 1;
        public System.Collections.Generic.IReadOnlyList<ItemActionGrant> ActionGrants => actionGrants;

#if UNITY_EDITOR
        public void SetEditorEquipmentPresentation(
            string newEquipmentId,
            string newDisplayName,
            string newDescription,
            Sprite newIcon,
            float newWeight = 1f,
            int newMaximumStack = 1)
        {
            equipmentId = newEquipmentId;
            displayName = newDisplayName;
            description = newDescription;
            icon = newIcon;
            weight = Mathf.Max(0f, newWeight);
            maximumStack = Mathf.Max(1, newMaximumStack);
        }

        public void SetEditorActionGrants(ItemActionGrant[] newActionGrants)
            => actionGrants = newActionGrants ?? Array.Empty<ItemActionGrant>();
#endif
    }

    public abstract class EquippableDefinition : ItemDefinition
    {
        public abstract EquipmentCategory Category { get; }
    }

    [Serializable]
    public struct EquipmentSlotAssignment
    {
        [SerializeField] private EquipmentSlotDefinition slot;
        [SerializeField] private EquippableDefinition equipment;

        public EquipmentSlotAssignment(EquipmentSlotDefinition slot, EquippableDefinition equipment)
        {
            this.slot = slot;
            this.equipment = equipment;
        }

        public EquipmentSlotDefinition Slot => slot;
        public EquippableDefinition Equipment => equipment;
    }
}
