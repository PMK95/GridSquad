using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class SelectionDetailWindowController : MonoBehaviour
    {
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text weightText;
        [SerializeField, FormerlySerializedAs("equipmentContainer")]
        private RectTransform paperDollPanel;
        [SerializeField, FormerlySerializedAs("inventoryContainer")]
        private Transform inventoryListContent;
        [SerializeField] private InventoryDropZone inventoryDropTarget;
        [SerializeField] private WorldItemDropZone worldDropTarget;
        [SerializeField] private EquipmentSlotView equipmentSlotPrefab;
        [SerializeField] private EquipmentItemCardView inventoryItemPrefab;
        [SerializeField] private Button statsTabButton;
        [SerializeField, FormerlySerializedAs("equipmentTabButton")]
        private Button equipmentInventoryTabButton;
        [SerializeField] private Button traitsTabButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ContextFloatingMenuController floatingMenu;
        [SerializeField] private CombatHudController hud;

        private readonly List<GameObject> generatedEquipmentViews = new();
        private readonly List<GameObject> generatedInventoryViews = new();
        private readonly List<GameObject> generatedDetailViews = new();
        private RectTransform detailListContent;
        private UiTooltipRowView detailRowPrefab;
        private Combatant selectedCombatant;
        private UnitDetailTab activeTab;
        private bool rebuildDirty;

        public Combatant SelectedCombatant => selectedCombatant;
        public UnitDetailTab ActiveTab => activeTab;
        public bool IsVisible => windowRoot != null
            ? windowRoot.activeSelf
            : gameObject.activeSelf;

        private void Awake()
        {
            EnsureDetailList();
            BindButtons();
            Hide();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSelectedCombatant();
        }

        private void LateUpdate()
        {
            if (!rebuildDirty || !IsVisible)
                return;
            rebuildDirty = false;
            RebuildActiveTab();
        }

        public void Show(Combatant combatant, UnitDetailTab tab)
        {
            if (combatant == null)
                return;
            activeTab = tab;
            if (windowRoot != null)
                windowRoot.SetActive(true);
            FollowSelection(combatant);
        }

        public void FollowSelection(Combatant combatant)
        {
            floatingMenu?.HideMenu();
            if (combatant == null)
            {
                Hide();
                return;
            }
            if (selectedCombatant != combatant)
            {
                UnsubscribeFromSelectedCombatant();
                selectedCombatant = combatant;
                SubscribeToSelectedCombatant();
            }
            rebuildDirty = false;
            RebuildActiveTab();
        }

        public void Hide()
        {
            floatingMenu?.HideMenu();
            if (windowRoot != null)
                windowRoot.SetActive(false);
            rebuildDirty = false;
            UnsubscribeFromSelectedCombatant();
            selectedCombatant = null;
        }

        public void RebuildActiveTab()
        {
            if (selectedCombatant == null)
                return;
            if (titleText != null)
                titleText.text = $"{selectedCombatant.DisplayName} · {GetTabLabel(activeTab)}";
            if (weightText != null)
            {
                UnitInventory inventory = selectedCombatant.Inventory;
                weightText.text = inventory != null
                    ? $"무게 {inventory.CarriedWeight:0.##} / {inventory.CarryCapacity:0.##}kg"
                    : "무게 -";
                weightText.color = inventory != null && inventory.CarriedWeight > inventory.CarryCapacity
                    ? new Color(0.95f, 0.2f, 0.15f)
                    : Color.white;
            }
            ClearGeneratedViews(generatedEquipmentViews);
            ClearGeneratedViews(generatedInventoryViews);
            ClearGeneratedViews(generatedDetailViews);
            bool showEquipmentInventory = activeTab == UnitDetailTab.EquipmentInventory;
            SetContainerVisible(paperDollPanel, showEquipmentInventory);
            SetInventoryPanelVisible(showEquipmentInventory);
            if (summaryText != null)
            {
                summaryText.gameObject.SetActive(!showEquipmentInventory);
                summaryText.enabled = false;
            }
            bool canEditEquipmentInventory = showEquipmentInventory
                && selectedCombatant.Team == Team.Ally;
            if (inventoryDropTarget != null)
                inventoryDropTarget.enabled = canEditEquipmentInventory;
            if (worldDropTarget != null)
                worldDropTarget.gameObject.SetActive(canEditEquipmentInventory);

            switch (activeTab)
            {
                case UnitDetailTab.Stats:
                    ShowStats();
                    break;
                case UnitDetailTab.EquipmentInventory:
                    ShowEquipmentInventory();
                    break;
                case UnitDetailTab.Traits:
                    ShowTraits();
                    break;
            }
        }

        private void ShowStats()
        {
            EnsureDetailList();
            bool hasCarryCapacity = false;
            UnitStatCategory? previousCategory = null;
            foreach (UnitRuntimeStatEntry stat in selectedCombatant.EffectiveStats)
            {
                if (stat.Definition == null)
                    continue;
                if (previousCategory != stat.Definition.Category)
                {
                    previousCategory = stat.Definition.Category;
                    CreateDetailRow(
                        null,
                        GetStatCategoryLabel(stat.Definition.Category),
                        string.Empty,
                        default,
                        false);
                }
                hasCarryCapacity |= stat.Definition.StatId == "carry_capacity";
                UnitStatDefinition capturedDefinition = stat.Definition;
                CreateDynamicDetailRow(
                    null,
                    capturedDefinition.DisplayName,
                    capturedDefinition.FormatValue(stat.Value),
                    () => UiTooltipContentFactory.CreateStat(
                        selectedCombatant,
                        capturedDefinition));
                if (capturedDefinition.StatId == "maximum_health")
                {
                    string currentHealthValue =
                        $"{selectedCombatant.CurrentHealth} / {selectedCombatant.MaximumHealth}";
                    CreateDetailRow(
                        null,
                        "현재 체력",
                        currentHealthValue,
                        new UiTooltipContent(
                            null,
                            "현재 체력",
                            currentHealthValue,
                            "현재 남아 있는 체력입니다. 0이 되면 전투 불능이 됩니다.",
                            $"최대 체력  {selectedCombatant.MaximumHealth}",
                            string.Empty));
                }
            }
            if (!hasCarryCapacity)
            {
                CreateDetailRow(
                    null,
                    "휴대 한도",
                    $"{selectedCombatant.CarryCapacity:0.##}kg",
                    new UiTooltipContent(
                        null,
                        "휴대 한도",
                        $"{selectedCombatant.CarryCapacity:0.##}kg",
                        "과적재 판정 전에 휴대할 수 있는 인벤토리 최대 무게입니다.",
                        string.Empty,
                        string.Empty));
            }
        }

        private void ShowTraits()
        {
            EnsureDetailList();
            if (selectedCombatant.Traits.Count == 0)
            {
                CreateDetailRow(null, "특성 없음", string.Empty, default, false);
                return;
            }
            foreach (UnitTraitDefinition trait in selectedCombatant.Traits)
            {
                if (trait == null)
                    continue;
                CreateDetailRow(
                    trait.Icon,
                    trait.DisplayName,
                    string.Empty,
                    UiTooltipContentFactory.CreateTrait(trait));
            }
        }

        private void ShowEquipmentInventory()
        {
            BuildEquipmentViews();
            BuildInventoryViews();
        }

        private void BuildEquipmentViews()
        {
            EquipmentLoadout loadout = selectedCombatant.EquipmentLoadout;
            if (loadout?.Layout == null || paperDollPanel == null || equipmentSlotPrefab == null)
                return;
            LayoutGroup layout = paperDollPanel.GetComponent<LayoutGroup>();
            if (layout != null)
                layout.enabled = false;
            foreach (EquipmentSlotDefinition slot in loadout.Layout.Slots)
            {
                if (slot == null)
                    continue;
                EquipmentSlotView view = Instantiate(equipmentSlotPrefab, paperDollPanel);
                generatedEquipmentViews.Add(view.gameObject);
                if (view.transform is RectTransform viewRect)
                {
                    Vector2 normalizedPosition = new(
                        0.5f + slot.PaperDollPosition.x,
                        0.5f + slot.PaperDollPosition.y);
                    viewRect.anchorMin = normalizedPosition;
                    viewRect.anchorMax = normalizedPosition;
                    viewRect.pivot = new Vector2(0.5f, 0.5f);
                    viewRect.anchoredPosition = Vector2.zero;
                }
                bool canEdit = selectedCombatant.Team == Team.Ally;
                view.Bind(
                    slot,
                    loadout.GetItemInstance(slot),
                    canEdit ? loadout : null,
                    _ => { },
                    canEdit ? ShowEquippedItemContextMenu : null);
                if (view.Item?.Definition is AdditionalEquipmentDefinition support
                    && support.PassiveKind == SupportEquipmentPassiveKind.RegeneratingBallisticPlate
                    && loadout.TryGetBallisticPlateState(out int remaining, out int maximum, out _))
                {
                    view.SetDurability(remaining, maximum);
                }
            }
        }

        private void BuildInventoryViews()
        {
            UnitInventory inventory = selectedCombatant.Inventory;
            if (inventory == null || inventoryListContent == null || inventoryItemPrefab == null)
                return;
            if (inventory.Items.Count == 0)
            {
                CreateEmptyInventoryMessage();
                return;
            }
            foreach (ItemInstance item in inventory.Items)
            {
                EquipmentItemCardView view = Instantiate(inventoryItemPrefab, inventoryListContent);
                generatedInventoryViews.Add(view.gameObject);
                bool canEdit = selectedCombatant.Team == Team.Ally;
                view.Bind(
                    item,
                    canEdit ? inventory : null,
                    _ => { },
                    canEdit ? ShowInventoryItemContextMenu : null);
            }
        }

        private void CreateEmptyInventoryMessage()
        {
            GameObject messageObject = new("EmptyInventory", typeof(RectTransform), typeof(Text));
            messageObject.transform.SetParent(inventoryListContent, false);
            Text message = messageObject.GetComponent<Text>();
            message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            message.fontSize = 16;
            message.alignment = TextAnchor.MiddleCenter;
            message.color = new Color(0.62f, 0.68f, 0.74f, 1f);
            message.text = "인벤토리가 비어 있습니다.";
            if (messageObject.transform is RectTransform rect)
                rect.sizeDelta = new Vector2(0f, 56f);
            generatedInventoryViews.Add(messageObject);
        }

        private void ShowInventoryItemContextMenu(ItemInstance item, Vector2 screenPosition)
        {
            if (item?.Definition == null || floatingMenu == null)
                return;
            List<ContextCommand> commands = new();
            ContextCommandQuery query = new(
                selectedCombatant,
                selectedCombatant.Entity,
                this,
                hud,
                item,
                null,
                null,
                selectedCombatant.Inventory);
            CollectSelectedCombatantItemCommands(query, commands);
            floatingMenu.ShowContextCommandsAtPointer(screenPosition, commands);
        }

        private void ShowEquippedItemContextMenu(EquipmentSlotView view, Vector2 screenPosition)
        {
            if (view?.Item == null || floatingMenu == null)
                return;
            List<ContextCommand> commands = new();
            ContextCommandQuery query = new(
                selectedCombatant,
                selectedCombatant.Entity,
                this,
                hud,
                view.Item,
                view.Slot,
                view.Loadout,
                selectedCombatant.Inventory);
            CollectSelectedCombatantItemCommands(query, commands);
            floatingMenu.ShowContextCommandsAtPointer(screenPosition, commands);
        }

        private void CollectSelectedCombatantItemCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands)
        {
            foreach (IContextCommandProvider provider in selectedCombatant.GetComponents<IContextCommandProvider>())
                provider.CollectAvailableContextCommands(query, commands);
        }

        private void SubscribeToSelectedCombatant()
        {
            if (selectedCombatant?.Inventory != null)
                selectedCombatant.Inventory.InventoryChanged += MarkRebuildDirty;
            if (selectedCombatant?.EquipmentLoadout != null)
                selectedCombatant.EquipmentLoadout.EquipmentChanged += MarkRebuildDirty;
            if (selectedCombatant?.Health != null)
                selectedCombatant.Health.HealthChanged += HandleHealthChanged;
            if (selectedCombatant != null)
                selectedCombatant.StatsChanged += HandleStatsChanged;
        }

        private void UnsubscribeFromSelectedCombatant()
        {
            if (selectedCombatant?.Inventory != null)
                selectedCombatant.Inventory.InventoryChanged -= MarkRebuildDirty;
            if (selectedCombatant?.EquipmentLoadout != null)
                selectedCombatant.EquipmentLoadout.EquipmentChanged -= MarkRebuildDirty;
            if (selectedCombatant?.Health != null)
                selectedCombatant.Health.HealthChanged -= HandleHealthChanged;
            if (selectedCombatant != null)
                selectedCombatant.StatsChanged -= HandleStatsChanged;
        }

        private void MarkRebuildDirty()
        {
            rebuildDirty = true;
        }

        private void HandleHealthChanged(EntityHealth _)
        {
            MarkRebuildDirty();
        }

        private void HandleStatsChanged(Combatant _)
        {
            MarkRebuildDirty();
        }

        private void BindButtons()
        {
            statsTabButton?.onClick.AddListener(() => Show(selectedCombatant, UnitDetailTab.Stats));
            equipmentInventoryTabButton?.onClick.AddListener(
                () => Show(selectedCombatant, UnitDetailTab.EquipmentInventory));
            traitsTabButton?.onClick.AddListener(() => Show(selectedCombatant, UnitDetailTab.Traits));
            closeButton?.onClick.AddListener(Hide);
        }

        private void EnsureDetailList()
        {
            if (summaryText == null || detailListContent != null)
                return;
            detailRowPrefab = Resources.Load<UiTooltipRowView>("UI/UiTooltipRow");

            GameObject scrollObject = new("DetailScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(summaryText.transform, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = scrollRectTransform.offsetMax = Vector2.zero;

            GameObject viewportObject = new(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            detailListContent = contentObject.GetComponent<RectTransform>();
            detailListContent.anchorMin = new Vector2(0f, 1f);
            detailListContent.anchorMax = Vector2.one;
            detailListContent.pivot = new Vector2(0.5f, 1f);
            detailListContent.anchoredPosition = Vector2.zero;
            detailListContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = detailListContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
        }

        private void CreateDetailRow(
            Sprite icon,
            string displayName,
            string value,
            UiTooltipContent tooltip,
            bool showTooltip = true)
        {
            EnsureDetailList();
            if (detailListContent == null)
                return;
            UiTooltipRowView row = detailRowPrefab != null
                ? Instantiate(detailRowPrefab, detailListContent)
                : UiTooltipRowView.Create(detailListContent);
            row.Bind(icon, displayName, value, tooltip, showTooltip);
            generatedDetailViews.Add(row.gameObject);
        }

        private void CreateDynamicDetailRow(
            Sprite icon,
            string displayName,
            string value,
            Func<UiTooltipContent> tooltipProvider,
            bool showTooltip = true)
        {
            EnsureDetailList();
            if (detailListContent == null)
                return;
            UiTooltipRowView row = detailRowPrefab != null
                ? Instantiate(detailRowPrefab, detailListContent)
                : UiTooltipRowView.Create(detailListContent);
            row.Bind(icon, displayName, value, tooltipProvider, showTooltip);
            generatedDetailViews.Add(row.gameObject);
        }

        private static string GetStatCategoryLabel(UnitStatCategory category)
        {
            return category switch
            {
                UnitStatCategory.Survivability => "생존",
                UnitStatCategory.Offense => "공격",
                UnitStatCategory.Mobility => "기동",
                UnitStatCategory.Utility => "지원",
                _ => "기타"
            };
        }

        private static string GetTabLabel(UnitDetailTab tab)
            => tab switch
            {
                UnitDetailTab.Stats => "스탯",
                UnitDetailTab.EquipmentInventory => "장비 / 인벤토리",
                UnitDetailTab.Traits => "특성",
                _ => "상세"
            };

        private static void SetContainerVisible(Transform container, bool visible)
        {
            if (container != null)
                container.gameObject.SetActive(visible);
        }

        private void SetInventoryPanelVisible(bool visible)
        {
            if (inventoryListContent == null)
                return;
            Transform current = inventoryListContent;
            while (current != null && current != windowRoot?.transform)
            {
                if (current.name == "InventoryPanel")
                {
                    current.gameObject.SetActive(visible);
                    return;
                }
                current = current.parent;
            }
            inventoryListContent.gameObject.SetActive(visible);
        }

        private static void ClearGeneratedViews(List<GameObject> views)
        {
            foreach (GameObject view in views)
                if (view != null)
                    Destroy(view);
            views.Clear();
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            GameObject newWindowRoot,
            Text newTitleText,
            Text newSummaryText,
            Text newWeightText,
            RectTransform newPaperDollPanel,
            Transform newInventoryListContent,
            InventoryDropZone newInventoryDropTarget,
            WorldItemDropZone newWorldDropTarget,
            EquipmentSlotView newEquipmentSlotPrefab,
            EquipmentItemCardView newInventoryItemPrefab,
            Button newStatsTabButton,
            Button newEquipmentInventoryTabButton,
            Button newTraitsTabButton,
            Button newCloseButton,
            ContextFloatingMenuController newFloatingMenu,
            CombatHudController newHud)
        {
            windowRoot = newWindowRoot;
            titleText = newTitleText;
            summaryText = newSummaryText;
            weightText = newWeightText;
            paperDollPanel = newPaperDollPanel;
            inventoryListContent = newInventoryListContent;
            inventoryDropTarget = newInventoryDropTarget;
            worldDropTarget = newWorldDropTarget;
            equipmentSlotPrefab = newEquipmentSlotPrefab;
            inventoryItemPrefab = newInventoryItemPrefab;
            statsTabButton = newStatsTabButton;
            equipmentInventoryTabButton = newEquipmentInventoryTabButton;
            traitsTabButton = newTraitsTabButton;
            closeButton = newCloseButton;
            floatingMenu = newFloatingMenu;
            hud = newHud;
        }
#endif
    }
}
