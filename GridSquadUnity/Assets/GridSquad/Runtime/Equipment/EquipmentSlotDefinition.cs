using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Equipment/Slot Definition", fileName = "EquipmentSlot")]
    public sealed class EquipmentSlotDefinition : ScriptableObject
    {
        [SerializeField] private string slotId = "slot";
        [SerializeField] private string displayName = "슬롯";
        [SerializeField] private EquipmentCategory category;
        [SerializeField] private EquipmentSlotKind slotKind;
        [SerializeField] private Vector2 paperDollPosition;

        public string SlotId => slotId;
        public string DisplayName => displayName;
        public EquipmentCategory Category => category;
        public EquipmentSlotKind SlotKind => slotKind;
        public Vector2 PaperDollPosition => paperDollPosition;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newSlotId,
            string newDisplayName,
            EquipmentCategory newCategory,
            EquipmentSlotKind newSlotKind,
            Vector2 newPaperDollPosition)
        {
            slotId = newSlotId;
            displayName = newDisplayName;
            category = newCategory;
            slotKind = newSlotKind;
            paperDollPosition = newPaperDollPosition;
        }
#endif
    }
}
