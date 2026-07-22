using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatActionButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text hotkeyText;
        [SerializeField] private Text statusText;

        public Button Button => button;
        public Image Background => background;
        public Image Icon => icon;
        public Text NameText => nameText;
        public Text HotkeyText => hotkeyText;
        public Text StatusText => statusText;

        public void Bind(UnityAction onClicked)
        {
            EnsureReferences();
            button.onClick.RemoveAllListeners();
            if (onClicked != null)
                button.onClick.AddListener(onClicked);
        }

        public void SetContent(string actionName, string hotkey, string status, Sprite actionIcon)
        {
            EnsureReferences();
            nameText.text = actionName;
            hotkeyText.text = hotkey;
            statusText.text = status;
            icon.sprite = actionIcon;
            icon.enabled = actionIcon != null;
        }

        public void SetState(bool interactable, Color backgroundColor, Color iconColor)
        {
            EnsureReferences();
            button.interactable = interactable;
            background.color = backgroundColor;
            icon.color = iconColor;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (background == null)
                background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            if (button == null)
                button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            if (nameText == null)
                nameText = CreateText("Name", TextAnchor.MiddleLeft, new Vector2(0.08f, 0.52f), new Vector2(0.68f, 0.94f));
            if (hotkeyText == null)
                hotkeyText = CreateText("Hotkey", TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(0.2f, 0.48f));
            if (statusText == null)
                statusText = CreateText("Status", TextAnchor.MiddleRight, new Vector2(0.35f, 0f), new Vector2(0.96f, 0.48f));
            if (icon == null)
            {
                GameObject iconObject = new("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                icon = iconObject.GetComponent<Image>();
                RectTransform rect = icon.rectTransform;
                rect.anchorMin = new Vector2(0.72f, 0.5f);
                rect.anchorMax = new Vector2(0.94f, 0.92f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            button.targetGraphic = background;
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
            text.fontSize = 13;
            text.alignment = alignment;
            text.color = Color.white;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
