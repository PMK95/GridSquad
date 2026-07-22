using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class DragVisualController : MonoBehaviour
    {
        private static DragVisualController activeController;

        private RectTransform host;
        private RectTransform ghost;
        private Canvas hostCanvas;

        public static DragVisualController Begin(
            Component source,
            Sprite icon,
            string displayName,
            PointerEventData eventData)
        {
            if (source == null || eventData == null)
                return null;

            RectTransform overlay = FindOverlay(source);
            if (overlay == null)
                return null;

            DragVisualController controller = overlay.GetComponent<DragVisualController>();
            if (controller == null)
                controller = overlay.gameObject.AddComponent<DragVisualController>();

            if (activeController != null && activeController != controller)
                activeController.End();
            controller.Show(source, icon, displayName, eventData);
            activeController = controller;
            return controller;
        }

        public void Move(PointerEventData eventData)
        {
            if (ghost == null || eventData == null)
                return;
            SetScreenPosition(eventData.position, eventData.pressEventCamera);
        }

        public void End()
        {
            if (ghost != null)
                Destroy(ghost.gameObject);
            ghost = null;
            if (activeController == this)
                activeController = null;
        }

        private void OnDisable() => End();

        private void Show(
            Component source,
            Sprite itemIcon,
            string displayName,
            PointerEventData eventData)
        {
            End();
            host = transform as RectTransform;
            hostCanvas = GetComponentInParent<Canvas>();
            if (host == null || hostCanvas == null)
                return;

            GameObject ghostObject = new(
                "DragGhost",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            ghostObject.transform.SetParent(host, false);
            ghostObject.transform.SetAsLastSibling();
            ghost = ghostObject.GetComponent<RectTransform>();
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);

            Vector2 sourceSize = source.transform is RectTransform sourceRect
                ? sourceRect.rect.size
                : new Vector2(160f, 72f);
            ghost.sizeDelta = new Vector2(
                Mathf.Clamp(sourceSize.x, 120f, 320f),
                Mathf.Clamp(sourceSize.y, 64f, 96f));

            CanvasGroup group = ghostObject.GetComponent<CanvasGroup>();
            group.alpha = 0.9f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = true;

            Image background = ghostObject.GetComponent<Image>();
            background.color = new Color(0.05f, 0.09f, 0.14f, 0.94f);
            background.raycastTarget = false;

            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(ghost, false);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = itemIcon;
            iconImage.enabled = itemIcon != null;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(56f, -8f);

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(ghost, false);
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = string.IsNullOrEmpty(displayName) ? "아이템" : displayName;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(itemIcon != null ? 72f : 12f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);

            SetScreenPosition(eventData.position, eventData.pressEventCamera);
        }

        private void SetScreenPosition(Vector2 screenPosition, Camera eventCamera)
        {
            Camera camera = hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : eventCamera != null ? eventCamera : hostCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    host,
                    screenPosition,
                    camera,
                    out Vector3 worldPosition))
            {
                ghost.position = worldPosition;
            }
        }

        private static RectTransform FindOverlay(Component source)
        {
            Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
            Canvas rootCanvas = sourceCanvas != null ? sourceCanvas.rootCanvas : null;
            if (rootCanvas == null)
                return null;

            Transform[] descendants = rootCanvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant.name == "FloatingUiOverlay" && descendant is RectTransform overlay)
                    return overlay;
            }
            return rootCanvas.transform as RectTransform;
        }
    }
}
