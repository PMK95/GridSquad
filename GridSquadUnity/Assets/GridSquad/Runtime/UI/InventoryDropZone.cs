using UnityEngine;
using UnityEngine.EventSystems;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class InventoryDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            EquipmentSlotView slotView = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<EquipmentSlotView>()
                : null;
            if (slotView?.Loadout != null && slotView.Slot != null)
                slotView.Loadout.TryUnequip(slotView.Slot, out _);
        }
    }
}
