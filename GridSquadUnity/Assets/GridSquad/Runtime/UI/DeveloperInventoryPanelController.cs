using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class DeveloperInventoryPanelController : MonoBehaviour
    {
        [SerializeField] private TacticalInputController inputController;
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private Button developerModeButton;
        [SerializeField] private Text developerModeButtonText;
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private Text targetText;
        [SerializeField] private Dropdown itemDropdown;
        [SerializeField] private InputField quantityInput;
        [SerializeField] private Button addItemButton;
        [SerializeField] private Text statusText;

        private readonly List<ItemDefinition> availableItems = new();
        private bool developerModeEnabled;
        private bool runtimeInitialized;

        private void Awake()
        {
            developerModeButton?.onClick.AddListener(ToggleDeveloperMode);
            addItemButton?.onClick.AddListener(AddSelectedItemToSelectedAlly);
            PopulateItemDropdown();
            SetDeveloperModeEnabled(false);
        }

        public void InitializeRuntime(TacticalInputController newInputController)
        {
            inputController = newInputController != null
                ? newInputController
                : inputController;
            runtimeInitialized = true;
        }

        private void Update()
        {
            if (!runtimeInitialized || !developerModeEnabled || targetText == null)
                return;

            Combatant selectedAlly = GetSelectedAlly();
            targetText.text = selectedAlly != null
                ? $"대상: {selectedAlly.DisplayName}"
                : "대상: 아군 유닛을 선택하세요";
            if (addItemButton != null)
                addItemButton.interactable = selectedAlly != null && availableItems.Count > 0;
        }

        private void ToggleDeveloperMode()
        {
            SetDeveloperModeEnabled(!developerModeEnabled);
        }

        private void SetDeveloperModeEnabled(bool enabled)
        {
            developerModeEnabled = enabled;
            if (inventoryPanel != null)
                inventoryPanel.SetActive(enabled);
            if (developerModeButtonText != null)
                developerModeButtonText.text = enabled ? "개발자 모드: ON" : "개발자 모드: OFF";
            if (statusText != null)
                statusText.text = string.Empty;
        }

        private void PopulateItemDropdown()
        {
            availableItems.Clear();
            itemDropdown?.ClearOptions();
            if (itemCatalog == null)
            {
                if (statusText != null)
                    statusText.text = "아이템 카탈로그가 연결되지 않았습니다.";
                return;
            }

            List<Dropdown.OptionData> options = new();
            foreach (ItemDefinition item in itemCatalog.Items)
            {
                if (item == null)
                    continue;
                availableItems.Add(item);
                options.Add(new Dropdown.OptionData(item.DisplayName, item.Icon));
            }
            itemDropdown?.AddOptions(options);
        }

        private void AddSelectedItemToSelectedAlly()
        {
            Combatant selectedAlly = GetSelectedAlly();
            if (selectedAlly?.Inventory == null)
            {
                SetStatus("인벤토리가 있는 아군 유닛을 선택하세요.", false);
                return;
            }
            if (itemDropdown == null
                || itemDropdown.value < 0
                || itemDropdown.value >= availableItems.Count)
            {
                SetStatus("추가할 아이템을 선택하세요.", false);
                return;
            }

            int quantity = 1;
            if (quantityInput != null
                && !int.TryParse(quantityInput.text, out quantity))
            {
                SetStatus("수량을 숫자로 입력하세요.", false);
                return;
            }
            quantity = Mathf.Clamp(quantity, 1, 999);
            quantityInput?.SetTextWithoutNotify(quantity.ToString());

            ItemDefinition item = availableItems[itemDropdown.value];
            if (!selectedAlly.Inventory.TryAdd(item, quantity, out string failureReason))
            {
                SetStatus(failureReason, false);
                return;
            }

            SetStatus($"{selectedAlly.DisplayName} 인벤토리에 {item.DisplayName} x{quantity} 추가", true);
        }

        private Combatant GetSelectedAlly()
        {
            Combatant selected = inputController != null
                ? inputController.SelectedCombatant
                : null;
            return selected != null && selected.Team == Team.Ally ? selected : null;
        }

        private void SetStatus(string message, bool succeeded)
        {
            if (statusText == null)
                return;
            statusText.text = message;
            statusText.color = succeeded
                ? new Color(0.45f, 0.9f, 0.55f)
                : new Color(1f, 0.48f, 0.4f);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            ItemCatalog newItemCatalog,
            Button newDeveloperModeButton,
            Text newDeveloperModeButtonText,
            GameObject newInventoryPanel,
            Text newTargetText,
            Dropdown newItemDropdown,
            InputField newQuantityInput,
            Button newAddItemButton,
            Text newStatusText)
        {
            itemCatalog = newItemCatalog;
            developerModeButton = newDeveloperModeButton;
            developerModeButtonText = newDeveloperModeButtonText;
            inventoryPanel = newInventoryPanel;
            targetText = newTargetText;
            itemDropdown = newItemDropdown;
            quantityInput = newQuantityInput;
            addItemButton = newAddItemButton;
            statusText = newStatusText;
        }
#endif
    }
}
