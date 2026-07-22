using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public enum SelectedHudActionSlot
    {
        TargetCommand,
        AutomaticPeek,
        PlayerAction1,
        PlayerAction2,
        PlayerAction3,
        PlayerAction4
    }

    public sealed class SquadRosterHudController : MonoBehaviour
    {
        [Serializable]
        public sealed class RosterCardView
        {
            public Button Button;
            public Image Portrait;
            public Text NameText;
            public Text RoleText;
            public Image HealthFill;
            public Image ArmorDurabilityFill;
            public Text HealthText;
            public Image SelectionBorder;
            public Outline SelectionOutline;
            public GameObject DeathOverlay;
        }

        [Serializable]
        public sealed class ActionButtonView
        {
            public SelectedHudActionSlot Slot;
            public Button Button;
            public Image Background;
            public Image Icon;
            public Text NameText;
            public Text HotkeyText;
            public Text StatusText;
            public Sprite DefaultIcon;
        }

        [Header("연결")]
        [SerializeField] private CombatDirector director;
        [SerializeField] private TacticalInputController inputController;
        [SerializeField] private CombatHudController legacyHud;

        [Header("아군 로스터")]
        [SerializeField] private GameObject friendlyRosterPanel;
        [SerializeField] private RosterCardView[] rosterCards = Array.Empty<RosterCardView>();

        [Header("선택 유닛 정보")]
        [SerializeField] private RectTransform selectedUnitPanel;
        [SerializeField] private Image selectedPortrait;
        [SerializeField] private Text selectedNameText;
        [SerializeField] private Text selectedRoleText;
        [SerializeField] private Text selectedHealthText;
        [SerializeField] private Image selectedArmorDurabilityFill;
        [SerializeField] private Text selectedWeaponText;
        [SerializeField] private Text selectedTraitsText;
        [SerializeField] private Button detailsButton;
        [SerializeField] private Text detailsButtonText;
        [SerializeField] private GameObject detailsPanel;
        [SerializeField] private Text detailsText;
        [SerializeField] private Text utilityDebugText;
        [SerializeField] private float collapsedPanelHeight = 286f;
        [SerializeField] private float expandedPanelHeight = 610f;

        [Header("선택 액션")]
        [SerializeField] private GameObject selectedActionBar;
        [SerializeField] private ActionButtonView[] actionButtons = Array.Empty<ActionButtonView>();
        [SerializeField] private Transform playerActionButtonContainer;
        [SerializeField] private CombatActionButtonView playerActionButtonPrefab;

        private readonly List<Combatant> friendlyCombatants = new();
        private readonly List<CombatActionButtonView> generatedActionButtons = new();
        private bool detailsVisible;

        private static readonly Color EnabledActionColor = new(0.12f, 0.18f, 0.23f, 0.96f);
        private static readonly Color ActiveActionColor = new(0.12f, 0.55f, 0.7f, 1f);
        private static readonly Color DisabledActionColor = new(0.055f, 0.065f, 0.075f, 0.92f);
        private static readonly Color DisabledIconColor = new(0.35f, 0.38f, 0.4f, 0.72f);

        private void Awake()
        {
            if (director == null)
                director = FindFirstObjectByType<CombatDirector>();
            if (inputController == null)
                inputController = FindFirstObjectByType<TacticalInputController>();
            if (legacyHud == null)
                legacyHud = GetComponent<CombatHudController>();

            legacyHud?.SetLegacySelectedInfoVisible(false);
            if (selectedUnitPanel != null)
                selectedUnitPanel.gameObject.SetActive(false);
            if (selectedActionBar != null)
                selectedActionBar.SetActive(false);
            BindRosterButtons();
            SetDetailsVisible(false);
        }

        private void Start()
        {
            RefreshFriendlyCombatants();
            if (inputController != null
                && inputController.SelectedCombatant == null
                && friendlyCombatants.Count > 0)
            {
                inputController.SelectCombatantFromRoster(friendlyCombatants[0]);
            }
        }

        private void OnDestroy()
        {
            foreach (RosterCardView card in rosterCards)
                card?.Button?.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (friendlyCombatants.Count == 0)
                RefreshFriendlyCombatants();

            Combatant selected = inputController != null
                ? inputController.SelectedCombatant
                : null;
            RefreshRosterCards(selected);
        }

        private void RefreshFriendlyCombatants()
        {
            friendlyCombatants.Clear();
            if (director == null)
                return;

            foreach (Combatant combatant in director.Combatants)
            {
                if (combatant != null && combatant.Team == Team.Ally)
                    friendlyCombatants.Add(combatant);
            }
        }

        private void BindRosterButtons()
        {
            for (int cardIndex = 0; cardIndex < rosterCards.Length; cardIndex++)
            {
                int capturedIndex = cardIndex;
                RosterCardView card = rosterCards[cardIndex];
                if (card?.Button == null)
                    continue;
                card.Button.onClick.RemoveAllListeners();
                card.Button.onClick.AddListener(() => SelectFriendlyRosterCard(capturedIndex));
            }
        }

        private void BindActionButtons()
        {
            foreach (ActionButtonView view in actionButtons)
            {
                if (view?.Button == null)
                    continue;
                SelectedHudActionSlot capturedSlot = view.Slot;
                view.Button.onClick.RemoveAllListeners();
                view.Button.onClick.AddListener(() => ExecuteSelectedHudAction(capturedSlot));
            }
        }

        private void BuildPlayerActionButtonsFromPrefab()
        {
            if (playerActionButtonPrefab == null || playerActionButtonContainer == null)
                return;

            generatedActionButtons.Clear();
            foreach (ActionButtonView view in actionButtons)
            {
                if (view?.Button != null
                    && view.Slot is >= SelectedHudActionSlot.PlayerAction1
                        and <= SelectedHudActionSlot.PlayerAction4)
                {
                    view.Button.gameObject.SetActive(false);
                }
            }

            string[] hotkeys = { "G", "V", "X", "C" };
            for (int slotIndex = 0; slotIndex < CombatActionLoadout.KeyboardShortcutActionCount; slotIndex++)
            {
                int capturedSlotIndex = slotIndex;
                CombatActionButtonView view = Instantiate(
                    playerActionButtonPrefab,
                    playerActionButtonContainer);
                view.name = $"PlayerActionSlot{slotIndex + 1}";
                view.Bind(() => inputController?.BeginSelectedActionFromHud(capturedSlotIndex));
                view.SetContent("비어 있음", hotkeys[slotIndex], "미장착", null);
                generatedActionButtons.Add(view);
            }
        }

        private void SelectFriendlyRosterCard(int cardIndex)
        {
            if (inputController == null
                || cardIndex < 0
                || cardIndex >= friendlyCombatants.Count)
            {
                return;
            }

            inputController.SelectCombatantFromRoster(friendlyCombatants[cardIndex]);
        }

        private void ExecuteSelectedHudAction(SelectedHudActionSlot slot)
        {
            if (inputController == null)
                return;

            switch (slot)
            {
                case SelectedHudActionSlot.TargetCommand:
                    inputController.BeginSelectedTargetCommandFromHud();
                    break;
                case SelectedHudActionSlot.AutomaticPeek:
                    inputController.ToggleSelectedAutomaticPeekFromHud();
                    break;
                case SelectedHudActionSlot.PlayerAction1:
                case SelectedHudActionSlot.PlayerAction2:
                case SelectedHudActionSlot.PlayerAction3:
                case SelectedHudActionSlot.PlayerAction4:
                    inputController.BeginSelectedActionFromHud(GetPlayerActionSlotIndex(slot));
                    break;
            }
        }

        private void RefreshRosterCards(Combatant selected)
        {
            if (friendlyRosterPanel != null)
                friendlyRosterPanel.SetActive(true);

            for (int cardIndex = 0; cardIndex < rosterCards.Length; cardIndex++)
            {
                RosterCardView card = rosterCards[cardIndex];
                if (card == null)
                    continue;

                Combatant combatant = cardIndex < friendlyCombatants.Count
                    ? friendlyCombatants[cardIndex]
                    : null;
                if (card.Button != null)
                    card.Button.gameObject.SetActive(combatant != null);
                if (combatant == null)
                    continue;

                bool alive = combatant.IsAlive;
                if (card.Button != null)
                    card.Button.interactable = alive;
                if (card.Portrait != null)
                    card.Portrait.sprite = combatant.Portrait;
                if (card.NameText != null)
                    card.NameText.text = combatant.DisplayName;
                if (card.RoleText != null)
                {
                    card.RoleText.text = combatant.RoleName;
                    card.RoleText.color = combatant.AccentColor;
                }
                if (card.HealthFill != null)
                {
                    card.HealthFill.fillAmount = combatant.MaximumHealth > 0
                        ? Mathf.Clamp01((float)combatant.CurrentHealth / combatant.MaximumHealth)
                        : 0f;
                }
                if (card.HealthText != null)
                    card.HealthText.text = $"{combatant.CurrentHealth}/{combatant.MaximumHealth}";
                RefreshArmorDurabilityFill(card.ArmorDurabilityFill, combatant);
                if (card.SelectionOutline != null)
                {
                    card.SelectionOutline.enabled = selected == combatant;
                    card.SelectionOutline.effectColor = combatant.AccentColor;
                }
                else if (card.SelectionBorder != null)
                {
                    card.SelectionBorder.enabled = selected == combatant;
                    Color selectionColor = combatant.AccentColor;
                    selectionColor.a = 0.2f;
                    card.SelectionBorder.color = selectionColor;
                }
                if (card.DeathOverlay != null)
                    card.DeathOverlay.SetActive(!alive);
            }
        }

        private void RefreshSelectedEntityPanel(
            TacticalEntity selectedEntity,
            Combatant selectedCombatant)
        {
            if (selectedUnitPanel == null)
                return;

            selectedUnitPanel.gameObject.SetActive(selectedEntity != null);
            if (selectedEntity == null)
                return;

            if (selectedCombatant == null)
            {
                if (selectedArmorDurabilityFill != null)
                    selectedArmorDurabilityFill.gameObject.SetActive(false);
                EntityHealth health = selectedEntity.GetComponent<EntityHealth>();
                ShootableTarget shootableTarget = selectedEntity.ShootableTarget;
                if (selectedPortrait != null)
                    selectedPortrait.sprite = null;
                if (selectedNameText != null)
                    selectedNameText.text = selectedEntity.DisplayName;
                if (selectedRoleText != null)
                {
                    selectedRoleText.text = shootableTarget != null && shootableTarget.IsCover
                        ? "파괴 가능한 엄폐물"
                        : "전술 개체";
                    selectedRoleText.color = new Color(0.78f, 0.66f, 0.34f, 1f);
                }
                if (selectedHealthText != null)
                {
                    selectedHealthText.text = health != null
                        ? $"체력  {health.CurrentHealth} / {health.MaximumHealth}"
                        : "체력  -";
                }
                if (selectedWeaponText != null)
                    selectedWeaponText.text = shootableTarget != null ? "사격 대상 지정 가능" : "사격 대상 아님";
                if (selectedTraitsText != null)
                    selectedTraitsText.text = "명령 수행 능력 없음";
                if (detailsText != null)
                {
                    detailsText.text =
                        $"전술 상태\n셀  {selectedEntity.CurrentCell}\n" +
                        $"상태  {(selectedEntity.IsAvailable ? "사용 가능" : "파괴됨")}\n" +
                        $"파괴 가능  {(health != null ? "예" : "아니오")}\n" +
                        $"사격 가능  {(shootableTarget != null && shootableTarget.IsAlive ? "예" : "아니오")}";
                }
                if (utilityDebugText != null)
                    utilityDebugText.gameObject.SetActive(false);
                return;
            }

            if (selectedPortrait != null)
                selectedPortrait.sprite = selectedCombatant.Portrait;
            if (selectedNameText != null)
                selectedNameText.text = selectedCombatant.DisplayName;
            if (selectedRoleText != null)
            {
                selectedRoleText.text = selectedCombatant.RoleName;
                selectedRoleText.color = selectedCombatant.AccentColor;
            }
            if (selectedHealthText != null)
                selectedHealthText.text = $"체력  {selectedCombatant.CurrentHealth} / {selectedCombatant.MaximumHealth}";
            RefreshArmorDurabilityFill(selectedArmorDurabilityFill, selectedCombatant);
            if (selectedWeaponText != null)
                selectedWeaponText.text = BuildSelectedWeaponSummary(selectedCombatant);
            if (selectedTraitsText != null)
                selectedTraitsText.text = BuildTraitNameSummary(selectedCombatant);
            if (detailsText != null)
                detailsText.text = BuildDetailedUnitInformation(selectedCombatant);

            CombatActionController actionController = GetActionController(selectedCombatant);
            bool showUtility = director != null && director.DebugVisible && actionController != null;
            if (utilityDebugText != null)
            {
                utilityDebugText.gameObject.SetActive(showUtility);
                if (showUtility)
                    utilityDebugText.text = actionController.BuildUtilityDebugText();
            }
        }

        private void RefreshSelectedActionBar(Combatant selected)
        {
            if (selectedActionBar != null)
                selectedActionBar.SetActive(selected != null);

            bool isControllableAlly = selected != null
                && selected.IsAlive
                && selected.Team == Team.Ally;
            UnitTacticalBehaviorController behavior = selected != null
                ? selected.GetComponent<UnitTacticalBehaviorController>()
                : null;
            CombatActionController actionController = behavior != null
                ? behavior.ActionController
                : null;

            foreach (ActionButtonView view in actionButtons)
            {
                if (view == null)
                    continue;

                bool equipped = isControllableAlly;
                bool interactable = isControllableAlly;
                bool active = false;
                string status = "사용 가능";
                Sprite icon = view.DefaultIcon;

                if (view.Slot == SelectedHudActionSlot.TargetCommand)
                {
                    active = inputController != null && inputController.IsTargetCommandActive;
                    status = active ? "대상 선택 중" : "적 지정";
                }
                else if (view.Slot == SelectedHudActionSlot.AutomaticPeek)
                {
                    equipped = isControllableAlly && behavior != null;
                    interactable = equipped;
                    active = behavior != null && behavior.AutomaticPeekAllowed;
                    status = active ? "자동 엄폐 켜짐" : "자동 엄폐 꺼짐";
                }
                else
                {
                    int slotIndex = GetPlayerActionSlotIndex(view.Slot);
                    CombatActionRuntimeState runtimeState = actionController != null
                        ? actionController.GetPlayerActionRuntimeState(slotIndex)
                        : default;
                    equipped = isControllableAlly && runtimeState.IsEquipped;
                    interactable = isControllableAlly && runtimeState.IsInteractable;
                    active = runtimeState.IsRunning
                        || (inputController != null
                            && inputController.TargetingActionDefinition == runtimeState.Definition);
                    status = isControllableAlly ? runtimeState.StatusText : "명령 불가";
                    if (!runtimeState.IsEquipped)
                        status = "미장착";
                    if (runtimeState.Definition != null && runtimeState.Definition.Icon != null)
                        icon = runtimeState.Definition.Icon;
                    if (view.NameText != null)
                        view.NameText.text = runtimeState.Definition != null
                            ? runtimeState.Definition.DisplayName
                            : "비어 있음";
                }

                if (!isControllableAlly)
                {
                    equipped = false;
                    interactable = false;
                    active = false;
                    status = selected != null && !selected.IsAlive ? "전투 불능" : "명령 불가";
                }

                ApplyActionButtonState(view, equipped, interactable, active, status, icon);
            }

            RefreshGeneratedPlayerActionButtons(isControllableAlly, actionController);
        }

        private void RefreshGeneratedPlayerActionButtons(
            bool isControllableAlly,
            CombatActionController actionController)
        {
            string[] hotkeys = { "G", "V", "X", "C" };
            for (int slotIndex = 0; slotIndex < generatedActionButtons.Count; slotIndex++)
            {
                CombatActionButtonView view = generatedActionButtons[slotIndex];
                CombatActionRuntimeState state = actionController != null
                    ? actionController.GetPlayerActionRuntimeState(slotIndex)
                    : default;
                bool equipped = isControllableAlly && state.IsEquipped;
                bool interactable = isControllableAlly && state.IsInteractable;
                bool active = state.IsRunning
                    || (inputController != null
                        && inputController.TargetingActionDefinition == state.Definition);
                string status = !isControllableAlly
                    ? "명령 불가"
                    : state.Definition == null ? "미장착" : state.StatusText;
                Sprite icon = state.Definition != null ? state.Definition.Icon : null;
                view.SetContent(
                    state.Definition != null ? state.Definition.DisplayName : "비어 있음",
                    hotkeys[slotIndex],
                    status,
                    icon);
                view.SetState(
                    interactable,
                    active ? ActiveActionColor : equipped ? EnabledActionColor : DisabledActionColor,
                    equipped ? Color.white : DisabledIconColor);
            }
        }

        private static void ApplyActionButtonState(
            ActionButtonView view,
            bool equipped,
            bool interactable,
            bool active,
            string status,
            Sprite icon)
        {
            if (view.Button != null)
                view.Button.interactable = interactable;
            if (view.Background != null)
                view.Background.color = active
                    ? ActiveActionColor
                    : equipped ? EnabledActionColor : DisabledActionColor;
            if (view.Icon != null)
            {
                view.Icon.sprite = icon;
                view.Icon.color = equipped ? Color.white : DisabledIconColor;
            }
            if (view.StatusText != null)
                view.StatusText.text = status;
        }

        private void ToggleSelectedUnitDetails()
        {
            SetDetailsVisible(!detailsVisible);
        }

        private void SetDetailsVisible(bool visible)
        {
            detailsVisible = visible;
            if (detailsPanel != null)
                detailsPanel.SetActive(visible);
            if (detailsButtonText != null)
                detailsButtonText.text = visible ? "상세 닫기" : "상세 정보";
            if (selectedUnitPanel != null)
            {
                Vector2 size = selectedUnitPanel.sizeDelta;
                size.y = visible ? expandedPanelHeight : collapsedPanelHeight;
                selectedUnitPanel.sizeDelta = size;
            }
        }

        private static string BuildSelectedWeaponSummary(Combatant selected)
        {
            WeaponDefinition weapon = selected.Weapon;
            if (weapon == null)
                return "무기 없음";

            return $"{weapon.DisplayName}  피해 {selected.EffectiveWeaponDamage}\n" +
                $"탄약 {selected.CurrentMagazineAmmo} / {selected.ReserveAmmo}";
        }

        private static string BuildTraitNameSummary(Combatant selected)
        {
            IReadOnlyList<UnitTraitDefinition> traits = selected.Traits;
            if (traits == null || traits.Count == 0)
                return "특성  없음";

            StringBuilder builder = new("특성  ");
            for (int index = 0; index < traits.Count; index++)
            {
                if (traits[index] == null)
                    continue;
                if (builder.Length > 4)
                    builder.Append(" · ");
                builder.Append(traits[index].DisplayName);
            }
            return builder.ToString();
        }

        private static string BuildDetailedUnitInformation(Combatant selected)
        {
            StringBuilder builder = new();
            builder.Append("유효 스탯");
            IReadOnlyList<UnitRuntimeStatEntry> stats = selected.EffectiveStats;
            foreach (UnitRuntimeStatEntry stat in stats)
            {
                if (stat.Definition == null)
                    continue;
                builder.Append("\n")
                    .Append(stat.Definition.DisplayName)
                    .Append("  ")
                    .Append(stat.Definition.RoundToInteger
                        ? Mathf.RoundToInt(stat.Value).ToString()
                        : stat.Value.ToString("0.##"));
            }

            if (!string.IsNullOrWhiteSpace(selected.Description))
                builder.Append("\n\n").Append(selected.Description);

            IReadOnlyList<UnitTraitDefinition> traits = selected.Traits;
            if (traits != null && traits.Count > 0)
            {
                builder.Append("\n\n특성");
                foreach (UnitTraitDefinition trait in traits)
                {
                    if (trait != null)
                        builder.Append("\n• ").Append(trait.DisplayName).Append(" — ").Append(trait.Description);
                }
            }

            builder.Append("\n\n무기 슬롯");
            WeaponLoadout loadout = selected.WeaponLoadout;
            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                WeaponDefinition weapon = loadout != null ? loadout.GetDefinition(slotIndex) : null;
                WeaponAmmoState ammo = loadout != null ? loadout.GetAmmoState(slotIndex) : default;
                string marker = loadout != null && loadout.ActiveSlotIndex == slotIndex ? "▶" : "  ";
                builder.Append("\n").Append(marker).Append(" ").Append(slotIndex + 1).Append(". ")
                    .Append(weapon != null ? weapon.DisplayName : "미장착")
                    .Append(weapon != null ? $"  {ammo.MagazineAmmo}/{ammo.ReserveAmmo}" : string.Empty);
            }

            EquipmentLoadout equipment = selected.EquipmentLoadout;
            if (equipment != null)
            {
                builder.Append("\n\n장비");
                if (equipment.TryGetArmorState(out ArmorDefinition armor, out int remaining, out int maximum))
                    builder.Append("\n방어구  ").Append(armor.DisplayName).Append("  ").Append(remaining).Append("/").Append(maximum);
                else
                    builder.Append("\n방어구  미장착");
                builder.Append("\n추가장비  ").Append(equipment.BuildAdditionalEquipmentSummary());
            }

            ShotEvaluation shot = selected.CurrentShotEvaluation;
            ShootableTarget target = selected.CurrentTarget;
            string state = !selected.IsAlive ? "전투 불능" : selected.IsMoving ? "이동 중" : "대기";
            builder.Append("\n\n전술 상태")
                .Append("\n상태  ").Append(state)
                .Append("   셀  ").Append(selected.CurrentCell)
                .Append("\n대상  ").Append(target != null && target.IsAlive ? target.DisplayName : "없음")
                .Append("   엄폐 회피  ").Append(shot.CoverEvasionPercent.ToString("0")).Append("%")
                .Append("   아군 오사  ").Append(shot.FriendlyFireRiskPercent.ToString("0")).Append("%")
                .Append("\n사격  ").Append(shot.CanShoot ? "가능" : shot.FailureReason)
                .Append("   명중률  ").Append(shot.HitChancePercent.ToString("0")).Append("%");

            UnitTacticalBehaviorController behavior = selected.GetComponent<UnitTacticalBehaviorController>();
            if (behavior != null)
            {
                builder.Append("\n제어  ").Append(GetControlModeLabel(behavior.ControlMode))
                    .Append("   자동 엄폐  ").Append(behavior.AutomaticPeekAllowed ? "켜짐" : "꺼짐");
            }
            return builder.ToString();
        }

        private void EnsureArmorDurabilityVisuals()
        {
            foreach (RosterCardView card in rosterCards)
            {
                if (card == null || card.ArmorDurabilityFill != null || card.Button == null)
                    continue;
                card.ArmorDurabilityFill = CreateArmorDurabilityFill(
                    "ArmorDurability",
                    card.Button.transform,
                    new Vector2(0.08f, 0.03f),
                    new Vector2(0.92f, 0.07f));
            }
            if (selectedArmorDurabilityFill == null && selectedUnitPanel != null)
            {
                selectedArmorDurabilityFill = CreateArmorDurabilityFill(
                    "SelectedArmorDurability",
                    selectedUnitPanel,
                    new Vector2(0.06f, 0.015f),
                    new Vector2(0.94f, 0.03f));
            }
        }

        private static Image CreateArmorDurabilityFill(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject fillObject = new(objectName, typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(parent, false);
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return fill;
        }

        private static void RefreshArmorDurabilityFill(Image fill, Combatant combatant)
        {
            if (fill == null)
                return;
            int remaining = 0;
            int maximum = 0;
            bool hasArmor = combatant != null
                && combatant.EquipmentLoadout != null
                && combatant.EquipmentLoadout.TryGetArmorState(
                    out ArmorDefinition armor,
                    out remaining,
                    out maximum);
            fill.gameObject.SetActive(hasArmor);
            if (!hasArmor)
                return;
            fill.fillAmount = maximum > 0 ? Mathf.Clamp01(remaining / (float)maximum) : 0f;
            fill.color = remaining <= 0
                ? new Color(0.9f, 0.08f, 0.08f, 1f)
                : new Color(0.15f, 0.72f, 0.95f, 1f);
        }

        private static CombatActionController GetActionController(Combatant selected)
        {
            UnitTacticalBehaviorController behavior = selected != null
                ? selected.GetComponent<UnitTacticalBehaviorController>()
                : null;
            return behavior != null ? behavior.ActionController : null;
        }

        private static int GetPlayerActionSlotIndex(SelectedHudActionSlot slot)
            => (int)slot - (int)SelectedHudActionSlot.PlayerAction1;

        private static string GetControlModeLabel(CombatControlMode mode)
            => mode switch
            {
                CombatControlMode.FullAutomatic => "완전 자동",
                CombatControlMode.PlayerMovementAutomaticActions => "이동 수동 · 액션 자동",
                _ => "완전 수동"
            };

#if UNITY_EDITOR
        public void SetEditorActionButtonPrefab(
            Transform newContainer,
            CombatActionButtonView newPrefab)
        {
            playerActionButtonContainer = newContainer;
            playerActionButtonPrefab = newPrefab;
        }

        public void SetEditorReferences(
            CombatDirector newDirector,
            TacticalInputController newInputController,
            CombatHudController newLegacyHud,
            GameObject newFriendlyRosterPanel,
            RosterCardView[] newRosterCards,
            RectTransform newSelectedUnitPanel,
            Image newSelectedPortrait,
            Text newSelectedNameText,
            Text newSelectedRoleText,
            Text newSelectedHealthText,
            Text newSelectedWeaponText,
            Text newSelectedTraitsText,
            Button newDetailsButton,
            Text newDetailsButtonText,
            GameObject newDetailsPanel,
            Text newDetailsText,
            Text newUtilityDebugText,
            GameObject newSelectedActionBar,
            ActionButtonView[] newActionButtons)
        {
            director = newDirector;
            inputController = newInputController;
            legacyHud = newLegacyHud;
            friendlyRosterPanel = newFriendlyRosterPanel;
            rosterCards = newRosterCards ?? Array.Empty<RosterCardView>();
            selectedUnitPanel = newSelectedUnitPanel;
            selectedPortrait = newSelectedPortrait;
            selectedNameText = newSelectedNameText;
            selectedRoleText = newSelectedRoleText;
            selectedHealthText = newSelectedHealthText;
            selectedWeaponText = newSelectedWeaponText;
            selectedTraitsText = newSelectedTraitsText;
            detailsButton = newDetailsButton;
            detailsButtonText = newDetailsButtonText;
            detailsPanel = newDetailsPanel;
            detailsText = newDetailsText;
            utilityDebugText = newUtilityDebugText;
            selectedActionBar = newSelectedActionBar;
            actionButtons = newActionButtons ?? Array.Empty<ActionButtonView>();
        }
#endif
    }
}
