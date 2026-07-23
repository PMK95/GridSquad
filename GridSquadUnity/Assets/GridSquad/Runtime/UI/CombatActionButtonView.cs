using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatActionButtonView : MonoBehaviour,
        IUiTooltipContentProvider
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text hotkeyText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Text cooldownText;

        private UiTooltipContent tooltipContent;
        private UiTooltipTrigger tooltipTrigger;
        private bool hasTooltipContent;

        public Button Button => button;
        public Image Background => background;
        public Image Icon => icon;
        public Text NameText => nameText;
        public Text HotkeyText => hotkeyText;
        public Text StatusText => statusText;
        public Image CooldownFill => cooldownFill;
        public Text CooldownText => cooldownText;

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
            nameText.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
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

        public void SetCooldownProgress(float remainingSeconds, float durationSeconds)
        {
            EnsureReferences();
            bool coolingDown = remainingSeconds > 0f && durationSeconds > 0f;
            cooldownFill.gameObject.SetActive(coolingDown);
            cooldownText.gameObject.SetActive(coolingDown);
            if (!coolingDown)
            {
                cooldownFill.fillAmount = 0f;
                cooldownText.text = string.Empty;
                return;
            }

            cooldownFill.fillAmount = Mathf.Clamp01(remainingSeconds / durationSeconds);
            cooldownText.text = remainingSeconds >= 10f
                ? Mathf.CeilToInt(remainingSeconds).ToString()
                : remainingSeconds.ToString("0.0");
        }

        public void SetTooltipContent(
            string actionName,
            string description,
            string hotkey,
            CombatActionTargetingMode targetingMode,
            float cooldownDuration,
            float cooldownRemaining,
            string currentStatus,
            string sourceDisplayName)
        {
            string source = string.IsNullOrWhiteSpace(sourceDisplayName)
                ? "기본"
                : sourceDisplayName;
            tooltipContent = new UiTooltipContent(
                icon != null ? icon.sprite : null,
                actionName,
                string.IsNullOrWhiteSpace(hotkey) ? string.Empty : $"[{hotkey}]",
                description,
                $"대상  {GetTargetingLabel(targetingMode)}\n" +
                $"쿨다운  {cooldownDuration:0.##}초\n" +
                $"출처  {source}",
                cooldownRemaining > 0f
                    ? $"재사용 대기 {cooldownRemaining:0.0}초"
                    : currentStatus);
            hasTooltipContent = true;
        }

        public void ClearTooltipContent()
        {
            hasTooltipContent = false;
            if (tooltipTrigger != null)
            {
                tooltipTrigger.enabled = false;
                tooltipTrigger.enabled = true;
            }
        }

        public bool TryGetTooltipContent(out UiTooltipContent content)
        {
            content = tooltipContent;
            return hasTooltipContent;
        }

        private void Awake() => EnsureReferences();

        private void EnsureReferences()
        {
            if (transform is RectTransform rootRect)
                rootRect.sizeDelta = new Vector2(72f, 72f);
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
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(4f, 4f);
            icon.rectTransform.offsetMax = new Vector2(-4f, -4f);
            if (cooldownFill == null)
            {
                GameObject fillObject = new(
                    "CooldownFill",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                fillObject.transform.SetParent(transform, false);
                cooldownFill = fillObject.GetComponent<Image>();
                cooldownFill.color = new Color(0.015f, 0.025f, 0.04f, 0.82f);
                cooldownFill.raycastTarget = false;
                cooldownFill.type = Image.Type.Filled;
                cooldownFill.fillMethod = Image.FillMethod.Radial360;
                cooldownFill.fillOrigin = (int)Image.Origin360.Top;
                cooldownFill.fillClockwise = true;
            }
            cooldownFill.sprite = cooldownFill.sprite != null
                ? cooldownFill.sprite
                : UiFillSpriteProvider.GetSprite(background);
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillOrigin = (int)Image.Origin360.Top;
            cooldownFill.fillClockwise = true;
            CopyRect(icon.rectTransform, cooldownFill.rectTransform);
            if (cooldownText == null)
            {
                cooldownText = CreateText(
                    "CooldownText",
                    TextAnchor.LowerRight,
                    new Vector2(0.48f, 0f),
                    new Vector2(0.96f, 0.38f));
                cooldownText.fontSize = 15;
                cooldownText.fontStyle = FontStyle.Bold;
                cooldownText.raycastTarget = false;
            }
            RectTransform hotkeyRect = hotkeyText.rectTransform;
            hotkeyRect.anchorMin = new Vector2(0.04f, 0f);
            hotkeyRect.anchorMax = new Vector2(0.48f, 0.38f);
            hotkeyRect.offsetMin = hotkeyRect.offsetMax = Vector2.zero;
            hotkeyText.alignment = TextAnchor.LowerLeft;
            hotkeyText.fontStyle = FontStyle.Bold;
            cooldownText.rectTransform.anchorMin = new Vector2(0.48f, 0f);
            cooldownText.rectTransform.anchorMax = new Vector2(0.96f, 0.38f);
            cooldownText.rectTransform.offsetMin = cooldownText.rectTransform.offsetMax = Vector2.zero;
            cooldownText.alignment = TextAnchor.LowerRight;
            nameText.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
            if (cooldownText.transform.GetSiblingIndex() != transform.childCount - 1)
                cooldownText.transform.SetAsLastSibling();
            if (cooldownFill.transform.GetSiblingIndex() != transform.childCount - 2)
                cooldownFill.transform.SetSiblingIndex(transform.childCount - 2);
            button.targetGraphic = background;
            tooltipTrigger = GetComponent<UiTooltipTrigger>()
                ?? gameObject.AddComponent<UiTooltipTrigger>();
            tooltipTrigger.Bind(this);
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

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.offsetMin = source.offsetMin;
            destination.offsetMax = source.offsetMax;
        }

        private static string GetTargetingLabel(CombatActionTargetingMode targetingMode)
            => targetingMode switch
            {
                CombatActionTargetingMode.Self => "자신",
                CombatActionTargetingMode.GridCell => "지정 셀",
                CombatActionTargetingMode.ShootableTarget => "지정 대상",
                _ => "자동"
            };
    }
}
