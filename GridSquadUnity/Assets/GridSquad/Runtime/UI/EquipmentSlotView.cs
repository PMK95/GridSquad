using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridSquad
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EquipmentSlotView : MonoBehaviour,
        IDropHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IUiTooltipContentProvider
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Image durabilityFill;
        [SerializeField] private Text label;

        private EquipmentSlotDefinition slot;
        private ItemInstance item;
        private EquipmentLoadout loadout;
        private Action<EquipmentSlotView> clicked;
        private Action<EquipmentSlotView, Vector2> contextRequested;
        private CanvasGroup canvasGroup;
        private DragVisualController dragVisual;
        private bool isDragging;
        private UiTooltipTrigger tooltipTrigger;

        public EquipmentSlotDefinition Slot => slot;
        public ItemInstance Item => item;
        public EquippableDefinition Equipment => item?.Definition as EquippableDefinition;
        public EquipmentLoadout Loadout => loadout;

        private void Awake() => EnsureVisuals();

        public void Bind(
            EquipmentSlotDefinition newSlot,
            ItemInstance newItem,
            EquipmentLoadout newLoadout,
            Action<EquipmentSlotView> onClicked,
            Action<EquipmentSlotView, Vector2> onContextRequested)
        {
            EnsureVisuals();
            slot = newSlot;
            item = newItem;
            loadout = newLoadout;
            clicked = onClicked;
            contextRequested = onContextRequested;
            label.text = $"{slot.DisplayName}\n{(item?.Definition != null ? item.Definition.DisplayName : "-")}";
            icon.sprite = item?.Definition?.Icon;
            icon.enabled = icon.sprite != null;
            SetDurability(item?.Durability ?? 0, Mathf.Max(1, item?.Durability ?? 1));
        }

        public void Bind(
            EquipmentSlotDefinition newSlot,
            EquippableDefinition newEquipment,
            Action<EquipmentSlotView> onClicked,
            Func<EquipmentSlotDefinition, EquippableDefinition, bool> onEquipRequested)
        {
            Bind(newSlot, newEquipment != null ? new ItemInstance(newEquipment) : null, null, onClicked, null);
        }

        public void SetDurability(int remaining, int maximum)
        {
            EnsureVisuals();
            durabilityFill.gameObject.SetActive(maximum > 1);
            durabilityFill.fillAmount = maximum > 0 ? Mathf.Clamp01(remaining / (float)maximum) : 0f;
            durabilityFill.color = remaining <= 0
                ? new Color(0.9f, 0.12f, 0.12f, 1f)
                : new Color(0.2f, 0.75f, 0.95f, 1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                contextRequested?.Invoke(this, eventData.position);
            else if (eventData.button == PointerEventData.InputButton.Left)
                clicked?.Invoke(this);
        }

        public bool TryGetTooltipContent(out UiTooltipContent content)
        {
            content = UiTooltipContentFactory.CreateItem(
                item,
                slot != null ? slot.DisplayName : "장비 슬롯");
            return slot != null;
        }

        public void OnDrop(PointerEventData eventData)
        {
            EquipmentItemCardView card = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<EquipmentItemCardView>()
                : null;
            if (card?.Item != null && loadout != null)
                loadout.TryEquipFromInventory(slot, card.Item, out _);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (item == null || item.Definition == null || loadout == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            CanvasGroup group = EnsureCanvasGroup();
            if (group == null)
                return;

            isDragging = true;
            group.blocksRaycasts = false;
            dragVisual = DragVisualController.Begin(
                this,
                icon != null ? icon.sprite : null,
                item.Definition.DisplayName,
                eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging)
                dragVisual?.Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            FinishDrag();
        }

        private void OnDisable() => FinishDrag();

        private void EnsureVisuals()
        {
            EnsureCanvasGroup();
            tooltipTrigger = GetComponent<UiTooltipTrigger>()
                ?? gameObject.AddComponent<UiTooltipTrigger>();
            tooltipTrigger.Bind(this);
            background = background != null ? background : GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            if (durabilityFill != null && durabilityFill.sprite == null)
                durabilityFill.sprite = UiFillSpriteProvider.GetSprite(background);
            if (label != null)
                return;
            GameObject labelObject = new("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(transform, false);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.LowerCenter;
            label.color = Color.white;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(transform, false);
            icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = new Vector2(0.12f, 0.28f);
            icon.rectTransform.anchorMax = new Vector2(0.88f, 0.92f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;

            GameObject fillObject = new("Durability", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(transform, false);
            durabilityFill = fillObject.GetComponent<Image>();
            durabilityFill.sprite = UiFillSpriteProvider.GetSprite(background);
            durabilityFill.type = Image.Type.Filled;
            durabilityFill.fillMethod = Image.FillMethod.Horizontal;
            durabilityFill.rectTransform.anchorMin = new Vector2(0.08f, 0.05f);
            durabilityFill.rectTransform.anchorMax = new Vector2(0.92f, 0.12f);
            durabilityFill.rectTransform.offsetMin = durabilityFill.rectTransform.offsetMax = Vector2.zero;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            return canvasGroup;
        }

        private void FinishDrag()
        {
            if (!isDragging)
                return;
            isDragging = false;
            CanvasGroup group = EnsureCanvasGroup();
            if (group != null)
                group.blocksRaycasts = true;
            dragVisual?.End();
            dragVisual = null;
        }
    }
}
