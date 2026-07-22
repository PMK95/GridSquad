using UnityEngine;
using UnityEngine.EventSystems;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class WorldItemDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            EquipmentItemCardView itemCard = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<EquipmentItemCardView>()
                : null;
            if (itemCard?.Inventory != null && itemCard.Item != null)
            {
                itemCard.Inventory.TryDrop(
                    itemCard.Item,
                    itemCard.Item.Quantity,
                    out _,
                    out _);
                return;
            }

            EquipmentSlotView slotView = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<EquipmentSlotView>()
                : null;
            if (slotView?.Loadout != null && slotView.Slot != null)
                slotView.Loadout.TryDropEquipped(slotView.Slot, out _, out _);
        }
    }
}
