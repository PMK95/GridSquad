using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad.Editor
{
    public static class DeveloperInventoryPanelPrefabFactory
    {
        private const string DeveloperPanelPrefabPath =
            "Assets/GridSquad/Prefabs/UI/DeveloperInventoryPanel.prefab";
        private const string SelectionUiRootPrefabPath =
            "Assets/GridSquad/Resources/UI/SelectionUiRoot.prefab";
        private const string ItemCatalogPath =
            "Assets/GridSquad/Data/Equipment/Catalogs/ItemCatalog.asset";

        [MenuItem("GridSquad/UI/개발자 인벤토리 패널 갱신")]
        public static void CreateAndAttachDeveloperInventoryPanel()
        {
            GameObject developerPanelPrefab = CreateDeveloperInventoryPanelPrefab();
            AttachDeveloperPanelToSelectionUiRoot(developerPanelPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[개발자 UI] 개발자 인벤토리 패널 프리팹을 갱신했습니다.");
        }

        private static GameObject CreateDeveloperInventoryPanelPrefab()
        {
            DefaultControls.Resources resources = new();
            GameObject root = new(
                "DeveloperInventoryPanel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(DeveloperInventoryPanelController));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;

            Button modeButton = CreateButton(
                resources,
                "DeveloperModeButton",
                root.transform,
                "개발자 모드: OFF",
                new Color(0.12f, 0.18f, 0.24f, 0.96f));
            SetTopRightRect(
                modeButton.GetComponent<RectTransform>(),
                new Vector2(-16f, -16f),
                new Vector2(190f, 38f));
            Text modeButtonText = modeButton.GetComponentInChildren<Text>(true);

            GameObject panel = new(
                "InventoryPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.transform.SetParent(root.transform, false);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.06f, 0.98f);
            SetTopRightRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(-16f, -62f),
                new Vector2(340f, 250f));

            Text title = CreateText(
                "Title",
                panel.transform,
                "인벤토리 아이템 추가",
                19,
                TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(14f, -12f), new Vector2(312f, 30f));

            Text targetText = CreateText(
                "Target",
                panel.transform,
                "대상: 아군 유닛을 선택하세요",
                14,
                TextAnchor.MiddleLeft);
            targetText.color = new Color(0.65f, 0.82f, 0.92f);
            SetRect(targetText.rectTransform, new Vector2(14f, -48f), new Vector2(312f, 26f));

            Dropdown itemDropdown = CreateDropdown(
                resources,
                "ItemDropdown",
                panel.transform);
            SetRect(
                itemDropdown.GetComponent<RectTransform>(),
                new Vector2(14f, -82f),
                new Vector2(312f, 34f));

            InputField quantityInput = CreateInputField(
                resources,
                "QuantityInput",
                panel.transform,
                "수량");
            quantityInput.contentType = InputField.ContentType.IntegerNumber;
            quantityInput.text = "1";
            SetRect(
                quantityInput.GetComponent<RectTransform>(),
                new Vector2(14f, -124f),
                new Vector2(104f, 34f));

            Button addItemButton = CreateButton(
                resources,
                "AddItemButton",
                panel.transform,
                "선택 아이템 추가",
                new Color(0.12f, 0.38f, 0.28f, 1f));
            SetRect(
                addItemButton.GetComponent<RectTransform>(),
                new Vector2(126f, -124f),
                new Vector2(200f, 34f));

            Text statusText = CreateText(
                "Status",
                panel.transform,
                string.Empty,
                13,
                TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(statusText.rectTransform, new Vector2(14f, -168f), new Vector2(312f, 64f));

            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            DeveloperInventoryPanelController controller =
                root.GetComponent<DeveloperInventoryPanelController>();
            controller.SetEditorReferences(
                itemCatalog,
                modeButton,
                modeButtonText,
                panel,
                targetText,
                itemDropdown,
                quantityInput,
                addItemButton,
                statusText);
            panel.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DeveloperPanelPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AttachDeveloperPanelToSelectionUiRoot(
            GameObject developerPanelPrefab)
        {
            GameObject selectionUiRoot =
                PrefabUtility.LoadPrefabContents(SelectionUiRootPrefabPath);
            try
            {
                Transform existingPanel =
                    selectionUiRoot.transform.Find("DeveloperInventoryPanel");
                if (existingPanel != null)
                    Object.DestroyImmediate(existingPanel.gameObject);

                GameObject panelInstance = PrefabUtility.InstantiatePrefab(
                    developerPanelPrefab,
                    selectionUiRoot.transform) as GameObject;
                if (panelInstance == null)
                    throw new UnityException("개발자 패널 프리팹 인스턴스를 만들 수 없습니다.");
                panelInstance.name = "DeveloperInventoryPanel";
                panelInstance.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(selectionUiRoot, SelectionUiRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(selectionUiRoot);
            }
        }

        private static Button CreateButton(
            DefaultControls.Resources resources,
            string objectName,
            Transform parent,
            string label,
            Color backgroundColor)
        {
            GameObject buttonObject = DefaultControls.CreateButton(resources);
            buttonObject.name = objectName;
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = backgroundColor;
            Text text = buttonObject.GetComponentInChildren<Text>(true);
            text.text = label;
            text.color = Color.white;
            text.fontSize = 14;
            return buttonObject.GetComponent<Button>();
        }

        private static Dropdown CreateDropdown(
            DefaultControls.Resources resources,
            string objectName,
            Transform parent)
        {
            GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
            dropdownObject.name = objectName;
            dropdownObject.transform.SetParent(parent, false);
            dropdownObject.GetComponent<Image>().color =
                new Color(0.08f, 0.12f, 0.16f, 1f);
            Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.captionText.color = Color.white;
            dropdown.captionText.fontSize = 14;
            dropdown.itemText.color = Color.white;
            dropdown.itemText.fontSize = 14;
            return dropdown;
        }

        private static InputField CreateInputField(
            DefaultControls.Resources resources,
            string objectName,
            Transform parent,
            string placeholder)
        {
            GameObject inputObject = DefaultControls.CreateInputField(resources);
            inputObject.name = objectName;
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color =
                new Color(0.08f, 0.12f, 0.16f, 1f);
            InputField input = inputObject.GetComponent<InputField>();
            input.textComponent.color = Color.white;
            input.textComponent.fontSize = 14;
            Text placeholderText = input.placeholder as Text;
            if (placeholderText != null)
            {
                placeholderText.text = placeholder;
                placeholderText.color = new Color(0.65f, 0.7f, 0.75f, 0.7f);
            }
            return input;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            string textValue,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetTopRightRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 topLeft,
            Vector2 size)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
        }
    }
}
