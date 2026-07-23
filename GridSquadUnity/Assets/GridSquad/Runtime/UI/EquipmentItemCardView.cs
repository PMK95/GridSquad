using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridSquad
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EquipmentItemCardView : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler,
        IUiTooltipContentProvider
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Text quantityText;
        [SerializeField] private Text weightText;

        private Action<ItemInstance> clicked;
        private Action<ItemInstance, Vector2> contextRequested;
        private CanvasGroup canvasGroup;
        private DragVisualController dragVisual;
        private bool isDragging;
        private UiTooltipTrigger tooltipTrigger;

        public ItemInstance Item { get; private set; }
        public UnitInventory Inventory { get; private set; }
        public EquippableDefinition Equipment => Item?.Definition as EquippableDefinition;

        private void Awake() => EnsureVisuals();

        public void Bind(
            ItemInstance item,
            UnitInventory inventory,
            Action<ItemInstance> onClicked,
            Action<ItemInstance, Vector2> onContextRequested)
        {
            EnsureVisuals();
            Item = item;
            Inventory = inventory;
            clicked = onClicked;
            contextRequested = onContextRequested;
            label.text = item?.Definition != null
                ? item.Definition.DisplayName
                : "-";
            quantityText.text = item != null ? $"x{item.Quantity}" : "-";
            weightText.text = item != null ? $"{item.TotalWeight:0.##}kg" : "-";
            icon.sprite = item?.Definition?.Icon;
            icon.enabled = icon.sprite != null;
        }

        public void Bind(EquippableDefinition equipment, Action<EquippableDefinition> onClicked)
        {
            Bind(
                equipment != null ? new ItemInstance(equipment) : null,
                null,
                item => onClicked?.Invoke(item?.Definition as EquippableDefinition),
                null);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                contextRequested?.Invoke(Item, eventData.position);
            else if (eventData.button == PointerEventData.InputButton.Left)
                clicked?.Invoke(Item);
        }

        public bool TryGetTooltipContent(out UiTooltipContent content)
        {
            content = UiTooltipContentFactory.CreateItem(Item);
            return Item?.Definition != null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null || Item.Definition == null || Inventory == null)
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
                Item.Definition.DisplayName,
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
            if (GetComponent<Image>() == null)
                gameObject.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.98f);
            if (icon == null)
            {
                GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.03f, 0.12f);
                iconRect.anchorMax = new Vector2(0.18f, 0.88f);
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            }
            if (label == null)
                label = CreateColumnText("Name", new Vector2(0.22f, 0f), new Vector2(0.68f, 1f), TextAnchor.MiddleLeft);
            if (quantityText == null)
                quantityText = CreateColumnText("Quantity", new Vector2(0.68f, 0f), new Vector2(0.82f, 1f), TextAnchor.MiddleCenter);
            if (weightText == null)
                weightText = CreateColumnText("Weight", new Vector2(0.82f, 0f), new Vector2(0.97f, 1f), TextAnchor.MiddleRight);
        }

        private Text CreateColumnText(
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
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
