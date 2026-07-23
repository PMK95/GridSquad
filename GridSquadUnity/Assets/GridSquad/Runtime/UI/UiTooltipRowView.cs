using System;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class UiTooltipRowView : MonoBehaviour, IUiTooltipContentProvider
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;

        private UiTooltipContent tooltipContent;
        private Func<UiTooltipContent> tooltipContentProvider;
        private bool hasTooltip;

        private void Awake() => EnsureVisuals();

        public void Bind(
            Sprite rowIcon,
            string displayName,
            string value,
            UiTooltipContent content,
            bool showTooltip = true)
        {
            EnsureVisuals();
            icon.sprite = rowIcon;
            icon.gameObject.SetActive(rowIcon != null);
            nameText.text = displayName;
            valueText.text = value;
            tooltipContent = content;
            tooltipContentProvider = null;
            hasTooltip = showTooltip;
        }

        public void Bind(
            Sprite rowIcon,
            string displayName,
            string value,
            Func<UiTooltipContent> contentProvider,
            bool showTooltip = true)
        {
            Bind(
                rowIcon,
                displayName,
                value,
                default(UiTooltipContent),
                showTooltip);
            tooltipContentProvider = contentProvider;
        }

        public bool TryGetTooltipContent(out UiTooltipContent content)
        {
            content = tooltipContentProvider != null
                ? tooltipContentProvider()
                : tooltipContent;
            return hasTooltip;
        }

        public static UiTooltipRowView Create(Transform parent)
        {
            GameObject root = new(
                "UiTooltipRow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(UiTooltipRowView));
            root.transform.SetParent(parent, false);
            return root.GetComponent<UiTooltipRowView>();
        }

        private void EnsureVisuals()
        {
            if (transform is RectTransform rootRect)
                rootRect.sizeDelta = new Vector2(0f, 52f);
            Image background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.055f, 0.08f, 0.12f, 0.94f);

            if (icon == null)
            {
                GameObject iconObject = new("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.rectTransform.anchorMin = new Vector2(0.02f, 0.12f);
                icon.rectTransform.anchorMax = new Vector2(0.14f, 0.88f);
                icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            }
            nameText ??= CreateText("Name", TextAnchor.MiddleLeft, new Vector2(0.16f, 0f), new Vector2(0.72f, 1f));
            valueText ??= CreateText("Value", TextAnchor.MiddleRight, new Vector2(0.72f, 0f), new Vector2(0.96f, 1f));
            UiTooltipTrigger trigger = GetComponent<UiTooltipTrigger>()
                ?? gameObject.AddComponent<UiTooltipTrigger>();
            trigger.Bind(this);
        }

        private Text CreateText(
            string objectName,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }
    }
}
