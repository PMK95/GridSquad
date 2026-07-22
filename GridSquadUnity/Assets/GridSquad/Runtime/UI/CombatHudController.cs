using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class CombatHudController : MonoBehaviour
    {
        [SerializeField] private Text stateText;
        [SerializeField] private Text modeText;
        [SerializeField] private Text debugText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private GameObject selectedInfoPanel;
        [SerializeField] private Text selectedInfoTitleText;
        [SerializeField] private Text selectedInfoBodyText;
        [SerializeField] private Button allyFullAutoButton;
        [SerializeField] private Text allyFullAutoButtonText;
        [SerializeField] private CombatDirector director;
        [SerializeField] private MMF_Player selectionChangedFeedbacks;
        [SerializeField] private MMF_Player automaticModeChangedFeedbacks;
        [SerializeField] private MMF_Player resultPanelFeedbacks;

        private TacticalEntity selectedEntity;
        private Combatant selectedCombatant;
        private UnitTacticalBehaviorController selectedBehaviorController;
        private bool automaticModeInitialized;
        private CombatControlMode previousControlMode;
        private string actionMessage;
        private float actionMessageEndTime;
        private bool legacySelectedInfoVisible = true;

        private static readonly Color CommandButtonColor =
            new(0.24f, 0.28f, 0.34f, 0.96f);
        private static readonly Color FullAutoButtonColor =
            new(0.12f, 0.55f, 0.3f, 0.96f);

        private void Awake()
        {
            if (allyFullAutoButton != null)
                allyFullAutoButton.onClick.AddListener(HandleAllyFullAutoClicked);
        }

        private void OnDestroy()
        {
            if (allyFullAutoButton != null)
                allyFullAutoButton.onClick.RemoveListener(HandleAllyFullAutoClicked);
        }

        private void Update()
        {
            RefreshSelectedEntityInfo();
        }

        public void SetTimeScaleDisplay(float scale, bool paused)
        {
            if (stateText != null)
                stateText.text = paused ? "PAUSED" : $"SPEED x{scale:0.#}";
        }

        public void SetTargetingState(bool value)
        {
            if (modeText != null)
                modeText.text = value ? "TARGET MODE: ON" : "TARGET MODE: OFF";
        }

        public void SetActionTargetingState(CombatActionDefinition definition)
        {
            if (modeText != null)
                modeText.text = definition != null
                    ? $"{definition.DisplayName}: 대상 선택"
                    : "TARGET MODE: OFF";
        }

        public void SetActionMessage(string message)
        {
            actionMessage = message;
            actionMessageEndTime = Time.unscaledTime + 2f;
        }

        public void SetDebugState(bool value)
        {
            if (debugText != null)
                debugText.text = value ? "DEBUG: ALL" : "DEBUG: SELECTED";
        }

        public void SetSelectedEntity(TacticalEntity entity)
        {
            bool selectionChanged = selectedEntity != entity;
            selectedEntity = entity;
            selectedCombatant = entity != null ? entity.Combatant : null;
            selectedBehaviorController = selectedCombatant != null
                ? selectedCombatant.GetComponent<UnitTacticalBehaviorController>()
                : null;
            RefreshSelectedEntityInfo();
            if (selectionChanged && entity != null)
                selectionChangedFeedbacks?.PlayFeedbacks();
        }

        public void SetSelectedCombatant(Combatant combatant)
        {
            SetSelectedEntity(combatant != null ? combatant.Entity : null);
        }

        public void SetLegacySelectedInfoVisible(bool visible)
        {
            legacySelectedInfoVisible = visible;
            if (selectedInfoPanel != null)
                selectedInfoPanel.SetActive(visible);
        }

        public void SetAllyFullAutoState(bool enabled)
        {
            SetAllyControlMode(enabled
                ? CombatControlMode.FullAutomatic
                : CombatControlMode.PlayerMovementAutomaticActions);
        }

        public void SetAllyControlMode(CombatControlMode mode)
        {
            if (allyFullAutoButtonText != null)
                allyFullAutoButtonText.text = $"ALLY AI: {GetControlModeLabel(mode)}";
            if (allyFullAutoButton != null
                && allyFullAutoButton.targetGraphic is Image buttonImage)
            {
                buttonImage.color =
                    mode == CombatControlMode.FullAutomatic
                        ? FullAutoButtonColor
                        : CommandButtonColor;
            }

            if (automaticModeInitialized && previousControlMode != mode)
                automaticModeChangedFeedbacks?.PlayFeedbacks();
            previousControlMode = mode;
            automaticModeInitialized = true;
        }

        public void SetAllyFullAutoInteractable(bool interactable)
        {
            if (allyFullAutoButton != null)
                allyFullAutoButton.interactable = interactable;
        }

        public void ShowResult(string result)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                CanvasGroup canvasGroup = resultPanel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                resultPanel.transform.localScale = Vector3.one * 0.85f;
            }
            if (resultText != null)
                resultText.text = $"{result}\nPress R to Restart";
            resultPanelFeedbacks?.PlayFeedbacks();
        }

        private void RefreshSelectedEntityInfo()
        {
            if (!legacySelectedInfoVisible)
            {
                if (selectedInfoPanel != null)
                    selectedInfoPanel.SetActive(false);
                return;
            }

            if (selectedInfoPanel != null)
                selectedInfoPanel.SetActive(true);
            if (selectedInfoTitleText != null)
                selectedInfoTitleText.text = selectedCombatant != null
                    ? $"SELECTED: {selectedCombatant.DisplayName}"
                    : selectedEntity != null
                        ? $"SELECTED: {selectedEntity.DisplayName}"
                        : "SELECTED ENTITY";
            if (selectedInfoBodyText == null)
                return;

            if (selectedEntity == null)
            {
                selectedInfoBodyText.text = "NO ENTITY SELECTED\n\nLMB: SELECT UNIT OR COVER";
                return;
            }

            if (selectedCombatant == null)
            {
                EntityHealth selectedHealth = selectedEntity.GetComponent<EntityHealth>();
                ShootableTarget selectedTarget = selectedEntity.ShootableTarget;
                string entityType = selectedTarget != null && selectedTarget.IsCover
                    ? "COVER"
                    : "TACTICAL ENTITY";
                string healthText = selectedHealth != null
                    ? $"{selectedHealth.CurrentHealth} / {selectedHealth.MaximumHealth}"
                    : "-";
                selectedInfoBodyText.text =
                    $"TYPE  {entityType}\n" +
                    $"HP    {healthText}\n" +
                    $"CELL  {selectedEntity.CurrentCell}\n" +
                    $"STATE {(selectedEntity.IsAvailable ? "ACTIVE" : "DESTROYED")}\n\n" +
                    $"SHOOTABLE   {(selectedTarget != null && selectedTarget.IsAlive ? "YES" : "NO")}\n" +
                    $"DESTRUCTIBLE {(selectedHealth != null ? "YES" : "NO")}\n" +
                    "COMMANDS     NONE";
                return;
            }

            ShotEvaluation shot = selectedCombatant.CurrentShotEvaluation;
            ShootableTarget target = selectedCombatant.CurrentTarget;
            WeaponDefinition weapon = selectedCombatant.Weapon;
            string team = selectedCombatant.Team == Team.Ally ? "ALLY" : "ENEMY";
            string movement = !selectedCombatant.IsAlive ? "DEAD" : selectedCombatant.IsMoving ? "MOVING" : "IDLE";
            string targetName = target != null && target.IsAlive ? target.DisplayName : "-";
            string targetHealth = target != null && target.IsAlive
                ? $"{target.CurrentHealth} / {target.MaximumHealth}"
                : "-";
            string shotState = shot.CanShoot ? "READY" : $"BLOCKED ({shot.FailureReason})";
            string weaponInfo = weapon != null
                ? BuildWeaponInfo(selectedCombatant, weapon)
                : "WEAPON -";
            string fireState = selectedCombatant.FireStateRemainingSeconds > 0.01f
                ? $"{selectedCombatant.FireState}  {selectedCombatant.FireStateRemainingSeconds:0.0}s"
                : selectedCombatant.FireState.ToString();
            string coverAngle = shot.CoverAngleDegrees >= 0f ? $"{shot.CoverAngleDegrees:0} deg" : "-";
            string controlMode = selectedCombatant.Team == Team.Enemy
                ? "FULL AUTO"
                : selectedBehaviorController != null
                    ? GetControlModeLabel(selectedBehaviorController.ControlMode)
                    : "-";
            string automaticPeek = selectedBehaviorController != null
                && selectedBehaviorController.AutomaticPeekAllowed
                    ? "ON"
                    : "OFF";

            CombatActionController actionController = selectedBehaviorController != null
                ? selectedBehaviorController.ActionController
                : null;
            string actionInfo = actionController != null
                ? BuildActionInfo(actionController)
                : "ACTIONS -";
            string message = Time.unscaledTime < actionMessageEndTime
                ? $"\n\n{actionMessage}"
                : string.Empty;
            string utilityDebug = director != null && director.DebugVisible && actionController != null
                ? $"\n\n{actionController.BuildUtilityDebugText()}"
                : string.Empty;

            selectedInfoBodyText.text =
                $"TEAM  {team}\n" +
                $"CONTROL {controlMode}\n" +
                $"HP    {selectedCombatant.CurrentHealth} / {selectedCombatant.MaximumHealth}\n" +
                $"CELL  {selectedCombatant.CurrentCell}\n" +
                $"STATE {movement}\n\n" +
                $"TARGET  {targetName}\n" +
                $"TARGET HP {targetHealth}\n" +
                $"SHOT    {shotState}\n" +
                $"FIRE    {fireState}\n" +
                $"HIT     {shot.HitChancePercent:0}%\n" +
                $"FF RISK {shot.FriendlyFireRiskPercent:0}%\n" +
                $"COVER   {shot.CoverEvasionPercent:0}%\n" +
                $"COVER ANG {coverAngle}\n" +
                $"PEEK    {(selectedCombatant.PeekEnabled ? "ON" : "OFF")}  AUTO {automaticPeek}\n\n" +
                weaponInfo + "\n" + BuildEquipmentInfo(selectedCombatant) +
                "\n\n" + actionInfo + message + utilityDebug;
        }

        private static string BuildEquipmentInfo(Combatant combatant)
        {
            EquipmentLoadout equipment = combatant.EquipmentLoadout;
            if (equipment == null)
                return "ARMOR -  EXTRA -";
            string armorText = equipment.TryGetArmorState(out ArmorDefinition armor, out int remaining, out int maximum)
                ? $"ARMOR {armor.DisplayName} {remaining}/{maximum}"
                : "ARMOR -";
            return $"{armorText}  EXTRA {equipment.BuildAdditionalEquipmentSummary()}";
        }

        private static string BuildWeaponInfo(Combatant combatant, WeaponDefinition activeWeapon)
        {
            string header = $"[{activeWeapon.DisplayName}]  DMG {combatant.EffectiveWeaponDamage}  RANGE {activeWeapon.RangeInCells:0}\n" +
                $"AIM {activeWeapon.AimEnterDuration:0.0}s  INTERVAL {activeWeapon.AimedShotInterval:0.00}s  RELOAD {activeWeapon.ReloadDuration:0.0}s\n" +
                $"AMMO {combatant.CurrentMagazineAmmo}/{combatant.ReserveAmmo}";
            WeaponLoadout weaponLoadout = combatant.WeaponLoadout;
            if (weaponLoadout == null || !weaponLoadout.IsBattleInitialized)
                return header;

            string slots = string.Empty;
            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                WeaponDefinition definition = weaponLoadout.GetDefinition(slotIndex);
                WeaponAmmoState ammo = weaponLoadout.GetAmmoState(slotIndex);
                string marker = slotIndex == weaponLoadout.ActiveSlotIndex ? ">" : " ";
                slots += $"\n{marker}S{slotIndex + 1} {(definition != null ? definition.DisplayName : "-")}  {ammo.MagazineAmmo}/{ammo.ReserveAmmo}";
            }
            return header + slots;
        }

        private static string BuildActionInfo(CombatActionController actionController)
        {
            string[] hotkeys = { "G", "V", "X", "C" };
            System.Text.StringBuilder builder = new();
            for (int slotIndex = 0; slotIndex < CombatActionLoadout.KeyboardShortcutActionCount; slotIndex++)
            {
                CombatActionRuntimeState state = actionController.GetPlayerActionRuntimeState(slotIndex);
                if (state.Definition == null)
                    continue;
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(hotkeys[slotIndex])
                    .Append(' ')
                    .Append(state.Definition.DisplayName)
                    .Append("  ")
                    .Append(state.StatusText);
            }
            return builder.Length > 0 ? builder.ToString() : "ACTIONS -";
        }

        private void HandleAllyFullAutoClicked()
        {
            director?.CycleAllyControlMode();
        }

        private static string GetControlModeLabel(CombatControlMode mode)
            => mode switch
            {
                CombatControlMode.FullAutomatic => "FULL AUTO",
                CombatControlMode.PlayerMovementAutomaticActions => "MOVE MANUAL / ACTION AUTO",
                _ => "FULL MANUAL"
            };

#if UNITY_EDITOR
        public void SetEditorReferences(
            Text newStateText,
            Text newModeText,
            Text newDebugText,
            GameObject newResultPanel,
            Text newResultText,
            GameObject newSelectedInfoPanel,
            Text newSelectedInfoTitleText,
            Text newSelectedInfoBodyText,
            Button newAllyFullAutoButton,
            Text newAllyFullAutoButtonText,
            CombatDirector newDirector,
            MMF_Player newSelectionChangedFeedbacks,
            MMF_Player newAutomaticModeChangedFeedbacks,
            MMF_Player newResultPanelFeedbacks)
        {
            stateText = newStateText;
            modeText = newModeText;
            debugText = newDebugText;
            resultPanel = newResultPanel;
            resultText = newResultText;
            selectedInfoPanel = newSelectedInfoPanel;
            selectedInfoTitleText = newSelectedInfoTitleText;
            selectedInfoBodyText = newSelectedInfoBodyText;
            allyFullAutoButton = newAllyFullAutoButton;
            allyFullAutoButtonText = newAllyFullAutoButtonText;
            director = newDirector;
            selectionChangedFeedbacks = newSelectionChangedFeedbacks;
            automaticModeChangedFeedbacks = newAutomaticModeChangedFeedbacks;
            resultPanelFeedbacks = newResultPanelFeedbacks;
        }
#endif
    }
}
