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
        Grenade,
        Stim,
        Dash,
        SwitchWeapon
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

        private readonly List<Combatant> friendlyCombatants = new();
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
            BindRosterButtons();
            BindActionButtons();
            if (detailsButton != null)
                detailsButton.onClick.AddListener(ToggleSelectedUnitDetails);
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
            if (detailsButton != null)
                detailsButton.onClick.RemoveListener(ToggleSelectedUnitDetails);

            foreach (RosterCardView card in rosterCards)
                card?.Button?.onClick.RemoveAllListeners();
            foreach (ActionButtonView view in actionButtons)
                view?.Button?.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (friendlyCombatants.Count == 0)
                RefreshFriendlyCombatants();

            Combatant selected = inputController != null
                ? inputController.SelectedCombatant
                : null;
            RefreshRosterCards(selected);
            RefreshSelectedUnitPanel(selected);
            RefreshSelectedActionBar(selected);
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
                case SelectedHudActionSlot.Grenade:
                    inputController.BeginSelectedActionFromHud(CombatActionKind.Grenade);
                    break;
                case SelectedHudActionSlot.Stim:
                    inputController.BeginSelectedActionFromHud(CombatActionKind.Stim);
                    break;
                case SelectedHudActionSlot.Dash:
                    inputController.BeginSelectedActionFromHud(CombatActionKind.Dash);
                    break;
                case SelectedHudActionSlot.SwitchWeapon:
                    inputController.BeginSelectedActionFromHud(CombatActionKind.SwitchWeapon);
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

        private void RefreshSelectedUnitPanel(Combatant selected)
        {
            if (selectedUnitPanel == null)
                return;

            selectedUnitPanel.gameObject.SetActive(selected != null);
            if (selected == null)
                return;

            if (selectedPortrait != null)
                selectedPortrait.sprite = selected.Portrait;
            if (selectedNameText != null)
                selectedNameText.text = selected.DisplayName;
            if (selectedRoleText != null)
            {
                selectedRoleText.text = selected.RoleName;
                selectedRoleText.color = selected.AccentColor;
            }
            if (selectedHealthText != null)
                selectedHealthText.text = $"체력  {selected.CurrentHealth} / {selected.MaximumHealth}";
            if (selectedWeaponText != null)
                selectedWeaponText.text = BuildSelectedWeaponSummary(selected);
            if (selectedTraitsText != null)
                selectedTraitsText.text = BuildTraitNameSummary(selected);
            if (detailsText != null)
                detailsText.text = BuildDetailedUnitInformation(selected);

            CombatActionController actionController = GetActionController(selected);
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
                    CombatActionKind kind = ConvertToCombatActionKind(view.Slot);
                    CombatActionRuntimeState runtimeState = actionController != null
                        ? actionController.GetActionRuntimeState(kind)
                        : default;
                    equipped = isControllableAlly && runtimeState.IsEquipped;
                    interactable = isControllableAlly && runtimeState.IsInteractable;
                    active = runtimeState.IsRunning
                        || (inputController != null && inputController.TargetingActionKind == kind);
                    status = isControllableAlly ? runtimeState.StatusText : "명령 불가";
                    if (!runtimeState.IsEquipped)
                        status = "미장착";
                    if (runtimeState.Definition != null && runtimeState.Definition.Icon != null)
                        icon = runtimeState.Definition.Icon;
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
            UnitStatBlock stats = selected.EffectiveStats;
            builder.Append("유효 스탯\n")
                .Append("이동 x").Append(stats.MovementSpeedMultiplier.ToString("0.00"))
                .Append("   명중 +").Append(stats.HitChanceBonusPercent.ToString("0"))
                .Append("   피해 x").Append(stats.DamageMultiplier.ToString("0.00"));

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

            ShotEvaluation shot = selected.CurrentShotEvaluation;
            Combatant target = selected.CurrentTarget;
            string state = !selected.IsAlive ? "전투 불능" : selected.IsMoving ? "이동 중" : "대기";
            builder.Append("\n\n전술 상태")
                .Append("\n상태  ").Append(state)
                .Append("   셀  ").Append(selected.CurrentCell)
                .Append("\n대상  ").Append(target != null && target.IsAlive ? target.DisplayName : "없음")
                .Append("   엄폐 회피  ").Append(shot.CoverEvasionPercent.ToString("0")).Append("%")
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

        private static CombatActionController GetActionController(Combatant selected)
        {
            UnitTacticalBehaviorController behavior = selected != null
                ? selected.GetComponent<UnitTacticalBehaviorController>()
                : null;
            return behavior != null ? behavior.ActionController : null;
        }

        private static CombatActionKind ConvertToCombatActionKind(SelectedHudActionSlot slot)
            => slot switch
            {
                SelectedHudActionSlot.Grenade => CombatActionKind.Grenade,
                SelectedHudActionSlot.Stim => CombatActionKind.Stim,
                SelectedHudActionSlot.Dash => CombatActionKind.Dash,
                SelectedHudActionSlot.SwitchWeapon => CombatActionKind.SwitchWeapon,
                _ => CombatActionKind.BasicAttack
            };

        private static string GetControlModeLabel(CombatControlMode mode)
            => mode switch
            {
                CombatControlMode.FullAutomatic => "완전 자동",
                CombatControlMode.PlayerMovementAutomaticActions => "이동 수동 · 액션 자동",
                _ => "완전 수동"
            };

#if UNITY_EDITOR
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
