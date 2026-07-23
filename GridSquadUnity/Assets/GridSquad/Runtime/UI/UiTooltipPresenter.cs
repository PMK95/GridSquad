using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridSquad
{
    public readonly struct UiTooltipContent
    {
        public readonly Sprite Icon;
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string Description;
        public readonly string Details;
        public readonly string Status;

        public UiTooltipContent(
            Sprite icon,
            string title,
            string subtitle,
            string description,
            string details,
            string status)
        {
            Icon = icon;
            Title = title;
            Subtitle = subtitle;
            Description = description;
            Details = details;
            Status = status;
        }
    }

    public interface IUiTooltipContentProvider
    {
        bool TryGetTooltipContent(out UiTooltipContent content);
    }

    [DisallowMultipleComponent]
    public sealed class UiTooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private const float TooltipDelaySeconds = 0.15f;

        private IUiTooltipContentProvider contentProvider;
        private UiTooltipPresenter presenter;
        private bool pointerInside;
        private float pointerEnteredAt;

        public void Bind(IUiTooltipContentProvider provider)
        {
            contentProvider = provider;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            pointerEnteredAt = Time.unscaledTime;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            HideTooltip();
        }

        private void Update()
        {
            if (!pointerInside
                || contentProvider == null
                || Time.unscaledTime - pointerEnteredAt < TooltipDelaySeconds
                || !contentProvider.TryGetTooltipContent(out UiTooltipContent content))
            {
                return;
            }

            presenter ??= UiTooltipPresenter.GetOrCreateFor(this);
            presenter?.Show(this, transform as RectTransform, content);
        }

        private void OnDisable()
        {
            pointerInside = false;
            HideTooltip();
        }

        private void OnDestroy()
        {
            HideTooltip();
        }

        private void HideTooltip()
        {
            presenter?.Hide(this);
        }
    }

    [DisallowMultipleComponent]
    public sealed class UiTooltipPresenter : MonoBehaviour
    {
        private const string TooltipResourcePath = "UI/UiTooltip";
        private const float EdgePadding = 10f;
        private const float AnchorSpacing = 10f;

        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private Image icon;
        [SerializeField] private Text actionNameText;
        [SerializeField] private Text hotkeyText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text detailsText;
        [SerializeField] private Text statusText;

        private UiTooltipTrigger owner;
        private Canvas rootCanvas;
        private RectTransform canvasRect;

        public static UiTooltipPresenter GetOrCreateFor(Component source)
        {
            if (source == null)
                return null;

            Canvas canvas = source.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            UiTooltipPresenter existing =
                root.GetComponentInChildren<UiTooltipPresenter>(true);
            if (existing != null)
            {
                existing.SetCanvas(root);
                return existing;
            }

            UiTooltipPresenter prefab =
                Resources.Load<UiTooltipPresenter>(TooltipResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[공용 툴팁] Resources/UI/UiTooltip 프리팹을 찾을 수 없습니다.");
                return null;
            }

            UiTooltipPresenter created = Instantiate(prefab, root.transform, false);
            created.name = prefab.name;
            created.SetCanvas(root);
            return created;
        }

        public void Show(
            UiTooltipTrigger requestingOwner,
            RectTransform anchor,
            UiTooltipContent content)
        {
            if (requestingOwner == null || anchor == null)
                return;

            owner = requestingOwner;
            EnsureReferences();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            actionNameText.text = content.Title;
            hotkeyText.text = content.Subtitle;
            hotkeyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Subtitle));
            descriptionText.text = string.IsNullOrWhiteSpace(content.Description)
                ? "설명이 없습니다."
                : content.Description;
            detailsText.text = content.Details;
            detailsText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Details));
            statusText.text = content.Status;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Status));
            icon.sprite = content.Icon;
            icon.gameObject.SetActive(content.Icon != null);

            Canvas.ForceUpdateCanvases();
            PositionBesideAnchor(anchor);
            transform.SetAsLastSibling();
        }

        public void Hide(UiTooltipTrigger requestingOwner)
        {
            if (owner != requestingOwner)
                return;
            owner = null;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureReferences();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            owner = null;
        }

        private void SetCanvas(Canvas canvas)
        {
            rootCanvas = canvas;
            canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        }

        private void EnsureReferences()
        {
            if (tooltipRect == null)
                tooltipRect = transform as RectTransform;
            if (icon != null)
                return;

            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(transform, false);
            icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = new Vector2(0.8f, 0.76f);
            icon.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            icon.gameObject.SetActive(false);
        }

        private void PositionBesideAnchor(RectTransform anchor)
        {
            if (rootCanvas == null)
                SetCanvas(anchor.GetComponentInParent<Canvas>()?.rootCanvas);
            if (tooltipRect == null || canvasRect == null)
                return;

            Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 topScreen = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                (corners[1] + corners[2]) * 0.5f);
            Vector2 bottomScreen = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                (corners[0] + corners[3]) * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, topScreen, eventCamera, out Vector2 topLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, bottomScreen, eventCamera, out Vector2 bottomLocal);

            float width = tooltipRect.rect.width;
            float height = tooltipRect.rect.height;
            Rect canvasBounds = canvasRect.rect;
            bool showAbove = topLocal.y + AnchorSpacing + height
                <= canvasBounds.yMax - EdgePadding;
            tooltipRect.pivot = showAbove ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            Vector2 position = showAbove
                ? topLocal + Vector2.up * AnchorSpacing
                : bottomLocal + Vector2.down * AnchorSpacing;
            float minimumX = canvasBounds.xMin + EdgePadding + width * tooltipRect.pivot.x;
            float maximumX = canvasBounds.xMax - EdgePadding - width * (1f - tooltipRect.pivot.x);
            float minimumY = canvasBounds.yMin + EdgePadding + height * tooltipRect.pivot.y;
            float maximumY = canvasBounds.yMax - EdgePadding - height * (1f - tooltipRect.pivot.y);
            position.x = minimumX <= maximumX
                ? Mathf.Clamp(position.x, minimumX, maximumX)
                : canvasBounds.center.x;
            position.y = minimumY <= maximumY
                ? Mathf.Clamp(position.y, minimumY, maximumY)
                : canvasBounds.center.y;
            tooltipRect.anchoredPosition = position;
        }
    }
}
