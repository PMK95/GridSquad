using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Equipment/Armor Definition", fileName = "ArmorDefinition")]
    public sealed class ArmorDefinition : EquippableDefinition
    {
        [SerializeField, Min(1)] private int maximumBlockCount = 3;
        [SerializeField, Min(1)] private int defense = 1;
        [SerializeField] private EquipmentSlotKind armorSlotKind = EquipmentSlotKind.Torso;
        public override EquipmentCategory Category => EquipmentCategory.Armor;
        public int MaximumBlockCount => Mathf.Max(1, maximumBlockCount);
        public int Defense => Mathf.Max(1, defense);
        public EquipmentSlotKind ArmorSlotKind => armorSlotKind;

#if UNITY_EDITOR
        public void SetEditorMaximumBlockCount(int value)
            => maximumBlockCount = Mathf.Max(1, value);

        public void SetEditorDefense(int value)
            => defense = Mathf.Max(1, value);

        public void SetEditorArmorSlotKind(EquipmentSlotKind newSlotKind)
            => armorSlotKind = newSlotKind;
#endif
    }
}
