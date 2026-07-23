using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class SelectionInspectSnapshot
    {
        public string DisplayName;
        public string Subtitle;
        public string HealthText;
        public float HealthRatio;
        public Sprite Portrait;
        public string StatusText;

        public static SelectionInspectSnapshot Build(TacticalEntity entity)
        {
            SelectionInspectSnapshot snapshot = new()
            {
                DisplayName = entity != null ? entity.DisplayName : string.Empty,
                Subtitle = string.Empty,
                HealthText = string.Empty,
                HealthRatio = 0f,
                StatusText = string.Empty
            };
            if (entity == null)
                return snapshot;

            Combatant combatant = entity.Combatant;
            if (combatant != null)
            {
                snapshot.Portrait = combatant.Portrait;
                snapshot.Subtitle = combatant.RoleName;
                snapshot.HealthText = $"HP {combatant.CurrentHealth} / {combatant.MaximumHealth}";
                snapshot.HealthRatio = combatant.MaximumHealth > 0
                    ? combatant.CurrentHealth / (float)combatant.MaximumHealth
                    : 0f;
                List<string> states = new();
                states.Add(!combatant.IsAlive ? "전투 불능" : combatant.IsMoving ? "이동 중" : "대기");
                if (combatant.IsStunned)
                    states.Add("기절");
                if (combatant.IsStimActive)
                    states.Add($"자극제 {combatant.StimRemainingSeconds:0.0}s");
                CombatActionController actions = combatant.GetComponent<CombatActionController>();
                if (actions?.CurrentActionDefinition != null)
                    states.Add(actions.CurrentActionDefinition.DisplayName);
                if (combatant.EquipmentLoadout != null
                    && combatant.EquipmentLoadout.TryGetBallisticPlateState(
                        out int remaining,
                        out int maximum,
                        out float recharge))
                {
                    states.Add(remaining < maximum
                        ? $"방탄판 {remaining}/{maximum} · 재생 {recharge * 100f:0}%"
                        : $"방탄판 {remaining}/{maximum}");
                }
                StringBuilder statusBuilder = new(string.Join(" · ", states));
                AppendContributorStatus(entity, statusBuilder);
                snapshot.StatusText = statusBuilder.ToString();
                return snapshot;
            }

            EntityHealth health = entity.GetComponent<EntityHealth>();
            if (health != null)
            {
                snapshot.HealthText = $"HP {health.CurrentHealth} / {health.MaximumHealth}";
                snapshot.HealthRatio = health.MaximumHealth > 0
                    ? health.CurrentHealth / (float)health.MaximumHealth
                    : 0f;
            }
            WorldItemPickup pickup = entity.GetComponent<WorldItemPickup>();
            if (pickup?.Item?.Definition != null)
            {
                ItemInstance item = pickup.Item;
                snapshot.Portrait = item.Definition.Icon;
                snapshot.Subtitle = item.Definition is EquippableDefinition ? "장비 아이템" : "소모품";
                snapshot.StatusText = $"수량 {item.Quantity} · {item.TotalWeight:0.##}kg · 셀 {pickup.Cell}";
            }
            else
            {
                ShootableTarget target = entity.ShootableTarget;
                snapshot.Subtitle = target != null && target.IsCover ? "엄폐물" : "전술 개체";
                snapshot.StatusText = entity.IsAvailable ? $"사용 가능 · 셀 {entity.CurrentCell}" : "사용 불가";
            }
            StringBuilder contributorStatus = new(snapshot.StatusText);
            AppendContributorStatus(entity, contributorStatus);
            snapshot.StatusText = contributorStatus.ToString();
            return snapshot;
        }

        private static void AppendContributorStatus(TacticalEntity entity, StringBuilder builder)
        {
            foreach (MonoBehaviour component in entity.GetComponents<MonoBehaviour>())
            {
                if (component is not ISelectionInspectContributor contributor)
                    continue;
                if (builder.Length > 0)
                    builder.Append(" · ");
                contributor.AppendSelectionInspectStatus(builder);
            }
        }
    }

    public interface ISelectionInspectContributor
    {
        void AppendSelectionInspectStatus(StringBuilder statusBuilder);
    }

    public sealed class SelectionInspectController : MonoBehaviour
    {
        [SerializeField] private TacticalInputController inputController;
        [SerializeField] private GameObject inspectRoot;
        [SerializeField] private Image portrait;
        [SerializeField] private Text nameText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Text statusText;
        [SerializeField] private Transform actionContainer;
        [SerializeField] private CombatActionButtonView actionButtonPrefab;
        [SerializeField] private Button statsButton;
        [SerializeField, FormerlySerializedAs("equipmentButton")]
        private Button equipmentInventoryButton;
        [SerializeField] private Button traitsButton;
        [SerializeField] private SelectionDetailWindowController detailWindow;

        private readonly List<CombatActionButtonView> actionViews = new();
        private TacticalEntity previousSelection;
        private ColorBlock statsButtonColors;
        private ColorBlock equipmentButtonColors;
        private ColorBlock traitsButtonColors;
        private bool detailButtonColorsCached;

        private void Awake()
        {
            inputController = inputController != null
                ? inputController
                : FindFirstObjectByType<TacticalInputController>();
            CacheDetailButtonColors();
            BindDetailButtons();
        }

        private void Update()
        {
            TacticalEntity selected = inputController != null ? inputController.SelectedEntity : null;
            if (selected != previousSelection)
            {
                previousSelection = selected;
                RebuildActionViews(selected?.Combatant);
                FollowSelectionInOpenDetailWindow(selected?.Combatant);
            }
            RefreshInspectPane(selected);
            RefreshActionViews(selected?.Combatant);
            RefreshDetailButtonVisuals();
        }

        private void RefreshInspectPane(TacticalEntity selected)
        {
            if (inspectRoot != null)
                inspectRoot.SetActive(selected != null);
            if (selected == null)
                return;
            SelectionInspectSnapshot snapshot = SelectionInspectSnapshot.Build(selected);
            if (portrait != null)
            {
                portrait.sprite = snapshot.Portrait;
                portrait.enabled = snapshot.Portrait != null;
            }
            if (nameText != null)
                nameText.text = snapshot.DisplayName;
            if (subtitleText != null)
                subtitleText.text = snapshot.Subtitle;
            if (healthText != null)
            {
                healthText.gameObject.SetActive(!string.IsNullOrWhiteSpace(snapshot.HealthText));
                healthText.text = snapshot.HealthText;
            }
            if (healthFill != null)
            {
                healthFill.gameObject.SetActive(!string.IsNullOrWhiteSpace(snapshot.HealthText));
                healthFill.fillAmount = Mathf.Clamp01(snapshot.HealthRatio);
            }
            if (statusText != null)
                statusText.text = snapshot.StatusText;

            bool isUnit = selected.Combatant != null;
            SetButtonVisible(statsButton, isUnit);
            SetButtonVisible(equipmentInventoryButton, isUnit);
            SetButtonVisible(traitsButton, isUnit);
        }

        private void RebuildActionViews(Combatant combatant)
        {
            foreach (CombatActionButtonView view in actionViews)
                if (view != null)
                    Destroy(view.gameObject);
            actionViews.Clear();
            if (combatant == null || actionContainer == null || actionButtonPrefab == null)
                return;
            CombatActionController controller = combatant.GetComponent<CombatActionController>();
            int count = controller != null ? controller.PlayerActionCount : 0;
            bool hasPlate = combatant.EquipmentLoadout != null
                && combatant.EquipmentLoadout.TryGetBallisticPlateState(out _, out _, out _);
            for (int index = 0; index < count + (hasPlate ? 1 : 0); index++)
            {
                CombatActionButtonView view = Instantiate(actionButtonPrefab, actionContainer);
                int capturedIndex = index;
                view.Bind(() => ExecuteActionAt(capturedIndex));
                actionViews.Add(view);
            }
        }

        private void RefreshActionViews(Combatant combatant)
        {
            if (combatant == null)
                return;
            CombatActionController controller = combatant.GetComponent<CombatActionController>();
            int activeCount = controller != null ? controller.PlayerActionCount : 0;
            int plate = 0;
            int plateMax = 0;
            float recharge = 0f;
            bool hasPlate = combatant.EquipmentLoadout != null
                && combatant.EquipmentLoadout.TryGetBallisticPlateState(out plate, out plateMax, out recharge);
            int expectedCount = activeCount + (hasPlate ? 1 : 0);
            if (expectedCount != actionViews.Count)
            {
                RebuildActionViews(combatant);
                return;
            }

            string[] hotkeys = { "G", "V", "X", "C" };
            for (int index = 0; index < activeCount; index++)
            {
                CombatActionRuntimeState state = controller.GetPlayerActionRuntimeState(index);
                string hotkey = state.IsPassive
                    ? "패시브"
                    : index < hotkeys.Length ? hotkeys[index] : string.Empty;
                string source = string.IsNullOrWhiteSpace(state.SourceDisplayName)
                    ? state.StatusText
                    : $"{state.SourceDisplayName} · {state.StatusText}";
                actionViews[index].SetContent(
                    state.Definition != null ? state.Definition.DisplayName : "행동",
                    hotkey,
                    source,
                    CombatActionPresentation.GetDisplayedIcon(combatant, state));
                float cooldownDuration = state.Definition != null
                    ? state.Definition.CooldownSeconds
                    : 0f;
                actionViews[index].SetCooldownProgress(
                    state.CooldownRemaining,
                    cooldownDuration);
                if (state.Definition != null)
                {
                    actionViews[index].SetTooltipContent(
                        state.Definition.DisplayName,
                        state.Definition.Description,
                        hotkey,
                        state.Definition.TargetingMode,
                        cooldownDuration,
                        state.CooldownRemaining,
                        state.StatusText,
                        state.SourceDisplayName);
                }
                else
                {
                    actionViews[index].ClearTooltipContent();
                }
                actionViews[index].SetState(
                    state.IsInteractable && !state.IsPassive,
                    state.IsPassive
                        ? new Color(0.22f, 0.2f, 0.1f, 0.96f)
                        : state.IsRunning
                            ? new Color(0.12f, 0.55f, 0.7f, 1f)
                            : new Color(0.12f, 0.18f, 0.23f, 0.96f),
                    Color.white);
            }
            if (hasPlate)
            {
                CombatActionButtonView passiveView = actionViews[activeCount];
                passiveView.SetContent(
                    "방탄판",
                    "패시브",
                    plate < plateMax ? $"{plate}/{plateMax} · 재생 {recharge * 100f:0}%" : $"{plate}/{plateMax}",
                    null);
                passiveView.SetCooldownProgress(0f, 0f);
                passiveView.ClearTooltipContent();
                passiveView.SetState(false, new Color(0.22f, 0.2f, 0.1f, 0.96f), Color.white);
            }
        }

        private void ExecuteActionAt(int index)
        {
            Combatant selected = inputController != null ? inputController.SelectedCombatant : null;
            CombatActionController controller = selected != null
                ? selected.GetComponent<CombatActionController>()
                : null;
            if (controller == null || index < 0 || index >= controller.PlayerActionCount)
                return;
            inputController.BeginSelectedActionFromHud(index);
        }

        private void BindDetailButtons()
        {
            statsButton?.onClick.AddListener(() => OpenDetail(UnitDetailTab.Stats));
            equipmentInventoryButton?.onClick.AddListener(
                () => OpenDetail(UnitDetailTab.EquipmentInventory));
            traitsButton?.onClick.AddListener(() => OpenDetail(UnitDetailTab.Traits));
        }

        private void FollowSelectionInOpenDetailWindow(Combatant combatant)
        {
            if (detailWindow == null || !detailWindow.IsVisible)
                return;
            detailWindow.FollowSelection(combatant);
        }

        private void OpenDetail(UnitDetailTab tab)
        {
            if (detailWindow == null)
                return;
            Combatant selected = inputController != null
                ? inputController.SelectedCombatant
                : null;
            if (selected == null)
            {
                detailWindow.Hide();
                return;
            }
            if (detailWindow.IsVisible
                && detailWindow.SelectedCombatant == selected
                && detailWindow.ActiveTab == tab)
            {
                detailWindow.Hide();
                return;
            }
            detailWindow.Show(selected, tab);
        }

        private void CacheDetailButtonColors()
        {
            statsButtonColors = statsButton != null ? statsButton.colors : default;
            equipmentButtonColors = equipmentInventoryButton != null
                ? equipmentInventoryButton.colors
                : default;
            traitsButtonColors = traitsButton != null ? traitsButton.colors : default;
            detailButtonColorsCached = true;
        }

        private void RefreshDetailButtonVisuals()
        {
            if (!detailButtonColorsCached)
                CacheDetailButtonColors();
            Combatant selected = inputController != null
                ? inputController.SelectedCombatant
                : null;
            bool matchesSelection = detailWindow != null
                && detailWindow.IsVisible
                && detailWindow.SelectedCombatant == selected;
            ApplyDetailButtonVisual(
                statsButton,
                statsButtonColors,
                matchesSelection && detailWindow.ActiveTab == UnitDetailTab.Stats);
            ApplyDetailButtonVisual(
                equipmentInventoryButton,
                equipmentButtonColors,
                matchesSelection && detailWindow.ActiveTab == UnitDetailTab.EquipmentInventory);
            ApplyDetailButtonVisual(
                traitsButton,
                traitsButtonColors,
                matchesSelection && detailWindow.ActiveTab == UnitDetailTab.Traits);
        }

        private static void ApplyDetailButtonVisual(
            Button button,
            ColorBlock defaultColors,
            bool active)
        {
            if (button == null)
                return;
            ColorBlock colors = defaultColors;
            if (active)
            {
                Color selectedColor = new(0.12f, 0.55f, 0.7f, 1f);
                colors.normalColor = selectedColor;
                colors.highlightedColor = selectedColor * 1.08f;
                colors.selectedColor = selectedColor;
            }
            button.colors = colors;
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
                button.gameObject.SetActive(visible);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            TacticalInputController newInputController,
            GameObject newInspectRoot,
            Image newPortrait,
            Text newNameText,
            Text newSubtitleText,
            Text newHealthText,
            Image newHealthFill,
            Text newStatusText,
            Transform newActionContainer,
            CombatActionButtonView newActionButtonPrefab,
            Button newStatsButton,
            Button newEquipmentInventoryButton,
            Button newTraitsButton,
            SelectionDetailWindowController newDetailWindow)
        {
            inputController = newInputController;
            inspectRoot = newInspectRoot;
            portrait = newPortrait;
            nameText = newNameText;
            subtitleText = newSubtitleText;
            healthText = newHealthText;
            healthFill = newHealthFill;
            statusText = newStatusText;
            actionContainer = newActionContainer;
            actionButtonPrefab = newActionButtonPrefab;
            statsButton = newStatsButton;
            equipmentInventoryButton = newEquipmentInventoryButton;
            traitsButton = newTraitsButton;
            detailWindow = newDetailWindow;
            detailButtonColorsCached = false;
        }
#endif
    }
}
