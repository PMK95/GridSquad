using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class ContextFloatingMenuController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform menuRoot;
        [SerializeField] private RectTransform commandContainer;
        [SerializeField] private Button commandButtonPrefab;
        [SerializeField] private Text messageText;
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private Canvas overlayCanvas;

        private readonly List<Button> activeButtons = new();

        public bool IsOpen => menuRoot != null && menuRoot.gameObject.activeSelf;

        private void Awake()
        {
            EnsureRuntimeReferences();
            HideMenu();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool clickedOutside = mouse != null
                && mouse.leftButton.wasPressedThisFrame
                && !RectTransformUtility.RectangleContainsScreenPoint(
                    menuRoot,
                    mouse.position.ReadValue(),
                    GetEventCamera());
            if (IsOpen && (escapePressed || clickedOutside))
            {
                HideMenu();
            }
        }

        public void ShowContextCommandsAtPointer(
            Vector2 screenPosition,
            IReadOnlyList<ContextCommand> commands)
        {
            EnsureRuntimeReferences();
            ClearButtons();
            List<ContextCommand> sorted = new(commands);
            sorted.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Label, right.Label);
            });
            foreach (ContextCommand command in sorted)
                CreateCommandButton(command);
            if (activeButtons.Count == 0)
            {
                HideMenu();
                return;
            }

            menuRoot.gameObject.SetActive(true);
            menuRoot.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            PositionMenuInsideCanvas(screenPosition);
        }

        public void ShowMessage(string message)
        {
            if (messageText != null)
                messageText.text = message;
        }

        public void HideMenu()
        {
            if (menuRoot != null)
                menuRoot.gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            eventData.Use();
        }

        private void CreateCommandButton(ContextCommand command)
        {
            Button button = commandButtonPrefab != null
                ? Instantiate(commandButtonPrefab, commandContainer)
                : CreateFallbackButton(commandContainer);
            button.gameObject.SetActive(true);
            button.interactable = command.IsEnabled;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = command.IsEnabled || string.IsNullOrWhiteSpace(command.DisabledReason)
                    ? command.Label
                    : $"{command.Label}  ({command.DisabledReason})";
            }
            Image[] images = button.GetComponentsInChildren<Image>(true);
            if (command.Icon != null && images.Length > 1)
            {
                images[1].sprite = command.Icon;
                images[1].enabled = true;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                HideMenu();
                command.Execute?.Invoke();
            });
            activeButtons.Add(button);
        }

        private void PositionMenuInsideCanvas(Vector2 screenPosition)
        {
            RectTransform boundsRect = overlayRoot;
            if (boundsRect == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                boundsRect = canvas != null ? canvas.transform as RectTransform : null;
            }
            if (boundsRect == null)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boundsRect,
                screenPosition,
                GetEventCamera(),
                out Vector2 localPoint);
            Vector2 size = menuRoot.rect.size;
            Rect bounds = boundsRect.rect;
            Vector2 pivot = menuRoot.pivot;
            float minimumX = bounds.xMin + size.x * pivot.x;
            float maximumX = bounds.xMax - size.x * (1f - pivot.x);
            float minimumY = bounds.yMin + size.y * pivot.y;
            float maximumY = bounds.yMax - size.y * (1f - pivot.y);
            localPoint.x = minimumX <= maximumX
                ? Mathf.Clamp(localPoint.x, minimumX, maximumX)
                : bounds.center.x;
            localPoint.y = minimumY <= maximumY
                ? Mathf.Clamp(localPoint.y, minimumY, maximumY)
                : bounds.center.y;
            menuRoot.anchoredPosition = localPoint;
        }

        private void ClearButtons()
        {
            foreach (Button button in activeButtons)
                if (button != null)
                    Destroy(button.gameObject);
            activeButtons.Clear();
        }

        private void EnsureRuntimeReferences()
        {
            if (menuRoot == null)
                menuRoot = transform as RectTransform;
            if (overlayRoot == null)
                overlayRoot = FindFloatingOverlay();
            if (overlayCanvas == null && overlayRoot != null)
                overlayCanvas = overlayRoot.GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = GetComponentInParent<Canvas>();
            if (overlayRoot != null
                && menuRoot != null
                && menuRoot != overlayRoot
                && menuRoot.parent != overlayRoot)
            {
                menuRoot.SetParent(overlayRoot, false);
            }
            if (overlayCanvas != null && overlayRoot != null && overlayCanvas.transform == overlayRoot)
            {
                overlayCanvas.overrideSorting = true;
                overlayCanvas.sortingOrder = Mathf.Max(100, overlayCanvas.sortingOrder);
                if (overlayCanvas.GetComponent<GraphicRaycaster>() == null)
                    overlayCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            if (commandContainer == null)
                commandContainer = menuRoot;
        }

        private Camera GetEventCamera()
            => overlayCanvas != null && overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? overlayCanvas.worldCamera
                : null;

        private RectTransform FindFloatingOverlay()
        {
            Transform searchRoot = transform.root;
            foreach (RectTransform candidate in searchRoot.GetComponentsInChildren<RectTransform>(true))
                if (candidate.name == "FloatingUiOverlay")
                    return candidate;
            return null;
        }

        private static Button CreateFallbackButton(Transform parent)
        {
            GameObject root = new("ContextCommand", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 38f);
            root.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.98f);
            GameObject textObject = new("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return root.GetComponent<Button>();
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            RectTransform newMenuRoot,
            RectTransform newCommandContainer,
            Button newCommandButtonPrefab,
            Text newMessageText,
            RectTransform newOverlayRoot = null,
            Canvas newOverlayCanvas = null)
        {
            menuRoot = newMenuRoot;
            commandContainer = newCommandContainer;
            commandButtonPrefab = newCommandButtonPrefab;
            messageText = newMessageText;
            overlayRoot = newOverlayRoot;
            overlayCanvas = newOverlayCanvas;
        }
#endif
    }
}
