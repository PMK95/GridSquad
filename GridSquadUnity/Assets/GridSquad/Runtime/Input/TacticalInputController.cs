using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GridSquad
{
    public sealed class TacticalInputController : MonoBehaviour
    {
        private static readonly float[] SelectableGameSpeeds = { 0.5f, 1f, 2f, 4f };

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatHudController hud;
        [SerializeField] private LineRenderer selectedPathLine;
        [SerializeField] private GridCombatIndicator gridCombatIndicator;
        [SerializeField] private MMTimeManager timeManager;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask unitLayerMask;

        private readonly List<Vector3> pathPoints = new();
        private InputActionMap tacticalMap;
        private Combatant selectedCombatant;
        private bool targetingMode;
        private CombatActionKind? targetingActionKind;
        private bool paused;
        private float activeTimeScale = 1f;

        public bool Paused => paused;
        public float ActiveTimeScale => activeTimeScale;

        private void Awake()
        {
            if (timeManager == null)
                timeManager = FindFirstObjectByType<MMTimeManager>();
            tacticalMap = inputActions.FindActionMap("Tactical", true);
            EnsureRuntimeCombatInputActions();
            hud.SetTimeScaleDisplay(activeTimeScale, false);
            hud.SetTargetingState(false);
            hud.SetDebugState(false);
            hud.SetSelectedCombatant(null);
            gridCombatIndicator.SetSelectedCombatant(null);
        }

        private void OnEnable() => tacticalMap?.Enable();
        private void OnDisable() => tacticalMap?.Disable();

        private void Update()
        {
            if (director.BattleFinished)
            {
                if (ActionPressed("ToggleDebug"))
                    director.SetDebugVisible(!director.DebugVisible);
                if (ActionPressed("Restart"))
                    RestartScene();
                RefreshSelectedPathLine();
                return;
            }

            if (!director.BattleStarted)
            {
                if (ActionPressed("ToggleDebug"))
                    director.SetDebugVisible(!director.DebugVisible);
                if (ActionPressed("Restart"))
                    RestartScene();
                RefreshSelectedPathLine();
                return;
            }

            if (ActionPressed("Select"))
                HandleLeftClick();
            if (ActionPressed("MoveCommand"))
                HandleMoveCommand();
            if (ActionPressed("TargetCommand"))
                SetEnemyTargetingMode(!targetingMode || targetingActionKind.HasValue);
            if (ActionPressed("CycleControlMode"))
                director.CycleAllyControlMode();
            if (ActionPressed("Grenade"))
                BeginActionCellTargeting(CombatActionKind.Grenade);
            if (ActionPressed("Stim"))
                TryUseStim();
            if (ActionPressed("Dash"))
                BeginActionCellTargeting(CombatActionKind.Dash);
            if (ActionPressed("SwitchWeapon"))
                TrySwitchWeapon();
            if (ActionPressed("TogglePeek") && selectedCombatant != null && selectedCombatant.Team == Team.Ally)
            {
                UnitTacticalBehaviorController behaviorController =
                    selectedCombatant.GetComponent<UnitTacticalBehaviorController>();
                if (behaviorController != null)
                    behaviorController.SetAutomaticPeekAllowed(
                        !behaviorController.AutomaticPeekAllowed);
            }
            if (ActionPressed("TogglePause"))
                TogglePause();
            if (ActionPressed("Speed1"))
                SetGameSpeed(0.5f);
            if (ActionPressed("Speed2"))
                SetGameSpeed(1f);
            if (ActionPressed("Speed3"))
                SetGameSpeed(2f);
            if (ActionPressed("Speed4"))
                SetGameSpeed(4f);
            if (ActionPressed("Cancel"))
                CancelTargeting();
            if (ActionPressed("ToggleDebug"))
                director.SetDebugVisible(!director.DebugVisible);
            if (ActionPressed("Restart"))
                RestartScene();

            RefreshSelectedPathLine();
            RefreshActionTargetPreview();
        }

        private bool ActionPressed(string actionName)
            => tacticalMap.FindAction(actionName, true).WasPressedThisFrame();

        private void EnsureRuntimeCombatInputActions()
        {
            EnsureRuntimeButtonAction("CycleControlMode", "<Keyboard>/tab");
            EnsureRuntimeButtonAction("Grenade", "<Keyboard>/g");
            EnsureRuntimeButtonAction("Stim", "<Keyboard>/v");
            EnsureRuntimeButtonAction("Dash", "<Keyboard>/x");
            EnsureRuntimeButtonAction("SwitchWeapon", "<Keyboard>/c");
        }

        private void EnsureRuntimeButtonAction(string actionName, string bindingPath)
        {
            InputAction action = tacticalMap.FindAction(actionName, false);
            if (action == null)
                action = tacticalMap.AddAction(actionName, InputActionType.Button);
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.path == bindingPath)
                    return;
            }
            action.AddBinding(bindingPath, groups: "Keyboard&Mouse");
        }

        private void RestartScene()
        {
            if (timeManager != null)
            {
                timeManager.NormalTimeScale = 1f;
                timeManager.ResetTimeScale();
            }
            else
            {
                Time.timeScale = 1f;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleLeftClick()
        {
            if (IsPointerOverHud())
                return;

            if (targetingActionKind.HasValue)
            {
                TryExecuteTargetedAction();
                return;
            }

            if (!TryRaycastUnit(out Combatant clicked))
            {
                if (!targetingMode)
                    SelectCombatant(null);
                return;
            }

            if (targetingMode)
            {
                if (selectedCombatant != null && clicked.Team != selectedCombatant.Team)
                {
                    UnitTacticalBehaviorController behaviorController =
                        selectedCombatant.GetComponent<UnitTacticalBehaviorController>();
                    behaviorController?.SetPriorityTargetCommand(clicked);
                    CancelTargeting();
                }
                return;
            }

            SelectCombatant(clicked);
        }

        private void HandleMoveCommand()
        {
            if (selectedCombatant == null
                || selectedCombatant.Team != Team.Ally
                || targetingMode
                || targetingActionKind.HasValue)
                return;
            if (IsPointerOverHud())
                return;
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask, QueryTriggerInteraction.Ignore))
                return;
            UnitTacticalBehaviorController behaviorController =
                selectedCombatant.GetComponent<UnitTacticalBehaviorController>();
            behaviorController?.QueueMoveCommand(gridMap.WorldToGrid(hit.point));
        }

        private static bool IsPointerOverHud()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private bool TryRaycastUnit(out Combatant combatant)
        {
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, unitLayerMask, QueryTriggerInteraction.Ignore))
            {
                combatant = hit.collider.GetComponentInParent<Combatant>();
                return combatant != null && combatant.IsAlive;
            }
            combatant = null;
            return false;
        }

        private void SelectCombatant(Combatant combatant)
        {
            selectedCombatant?.SetSelected(false);
            selectedCombatant = combatant;
            selectedCombatant?.SetSelected(true);
            hud.SetSelectedCombatant(selectedCombatant);
            gridCombatIndicator.SetSelectedCombatant(selectedCombatant);
            CancelTargeting();
        }

        private void SetEnemyTargetingMode(bool value)
        {
            targetingMode = value && selectedCombatant != null && selectedCombatant.Team == Team.Ally;
            targetingActionKind = null;
            gridCombatIndicator.SetActionTargeting(null, null);
            hud.SetTargetingState(targetingMode);
        }

        private void BeginActionCellTargeting(CombatActionKind kind)
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null)
                return;

            targetingMode = false;
            targetingActionKind = kind;
            gridCombatIndicator.SetActionTargeting(kind, actionController);
            hud.SetActionTargetingState(kind);
        }

        private void TryUseStim()
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null)
                return;

            if (!actionController.TryStartPlayerStim(out string failureReason))
                hud.SetActionMessage(failureReason);
            else
                hud.SetActionMessage("자극제 사용");
            CancelTargeting();
        }

        private void TrySwitchWeapon()
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null)
                return;

            if (!actionController.TryStartPlayerWeaponSwap(out string failureReason))
                hud.SetActionMessage(failureReason);
            else
                hud.SetActionMessage("무기 교체 시작");
            CancelTargeting();
        }

        private void TryExecuteTargetedAction()
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null || !TryRaycastGroundCell(out GridCoordinate targetCell))
                return;

            bool started;
            string failureReason;
            if (targetingActionKind == CombatActionKind.Grenade)
                started = actionController.TryStartPlayerGrenade(targetCell, out failureReason);
            else
                started = actionController.TryStartPlayerDash(targetCell, out failureReason);
            if (!started)
            {
                hud.SetActionMessage(failureReason);
                return;
            }

            hud.SetActionMessage(targetingActionKind == CombatActionKind.Grenade
                ? "수류탄 투척"
                : "돌진 시작");
            CancelTargeting();
        }

        private void RefreshActionTargetPreview()
        {
            if (!targetingActionKind.HasValue || IsPointerOverHud())
                return;
            if (TryRaycastGroundCell(out GridCoordinate targetCell))
                gridCombatIndicator.SetActionPreviewCell(targetCell);
        }

        private bool TryRaycastGroundCell(out GridCoordinate cell)
        {
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                cell = gridMap.WorldToGrid(hit.point);
                return true;
            }
            cell = default;
            return false;
        }

        private CombatActionController GetSelectedAllyActionController()
        {
            if (selectedCombatant == null
                || !selectedCombatant.IsAlive
                || selectedCombatant.Team != Team.Ally)
            {
                hud.SetActionMessage("아군을 선택해야 합니다");
                return null;
            }

            UnitTacticalBehaviorController behaviorController =
                selectedCombatant.GetComponent<UnitTacticalBehaviorController>();
            return behaviorController != null ? behaviorController.ActionController : null;
        }

        private void CancelTargeting()
        {
            targetingMode = false;
            targetingActionKind = null;
            gridCombatIndicator.SetActionTargeting(null, null);
            hud.SetTargetingState(false);
        }

        private void TogglePause()
        {
            SetPaused(!paused);
        }

        private void SetPaused(bool shouldPause)
        {
            paused = shouldPause;
            if (timeManager != null)
            {
                if (paused)
                    timeManager.SetTimeScaleTo(0f);
                else
                    timeManager.ResetTimeScale();
            }
            else
            {
                Time.timeScale = paused ? 0f : activeTimeScale;
            }
            hud.SetTimeScaleDisplay(activeTimeScale, paused);
        }

        private void SetGameSpeed(float requestedSpeed)
        {
            activeTimeScale = FindClosestSelectableGameSpeed(requestedSpeed);
            paused = false;
            if (timeManager != null)
            {
                timeManager.NormalTimeScale = activeTimeScale;
                timeManager.ResetTimeScale();
            }
            else
            {
                Time.timeScale = activeTimeScale;
            }
            hud.SetTimeScaleDisplay(activeTimeScale, false);
        }

        private static float FindClosestSelectableGameSpeed(float requestedSpeed)
        {
            float closestSpeed = SelectableGameSpeeds[0];
            float closestDifference = Mathf.Abs(requestedSpeed - closestSpeed);
            for (int i = 1; i < SelectableGameSpeeds.Length; i++)
            {
                float difference = Mathf.Abs(requestedSpeed - SelectableGameSpeeds[i]);
                if (difference >= closestDifference)
                    continue;

                closestSpeed = SelectableGameSpeeds[i];
                closestDifference = difference;
            }
            return closestSpeed;
        }

        public void SetPauseFromDebugMenu(bool shouldPause)
        {
            SetPaused(shouldPause);
        }

        public float SetGameSpeedFromDebugMenu(float speed)
        {
            SetGameSpeed(speed);
            return activeTimeScale;
        }

        public void RestartCombatFromDebugMenu()
        {
            RestartScene();
        }

        private void RefreshSelectedPathLine()
        {
            if (selectedPathLine == null)
                return;
            pathPoints.Clear();
            if (selectedCombatant != null && selectedCombatant.Team == Team.Ally)
                selectedCombatant.AppendRemainingPathWorldPoints(pathPoints);
            selectedPathLine.enabled = pathPoints.Count > 1;
            selectedPathLine.positionCount = pathPoints.Count;
            if (pathPoints.Count > 0)
                selectedPathLine.SetPositions(pathPoints.ToArray());
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            InputActionAsset newInputActions,
            Camera newSceneCamera,
            GridMap newGridMap,
            CombatDirector newDirector,
            CombatHudController newHud,
            LineRenderer newSelectedPathLine,
            GridCombatIndicator newGridCombatIndicator,
            MMTimeManager newTimeManager,
            LayerMask newGroundLayerMask,
            LayerMask newUnitLayerMask)
        {
            inputActions = newInputActions;
            sceneCamera = newSceneCamera;
            gridMap = newGridMap;
            director = newDirector;
            hud = newHud;
            selectedPathLine = newSelectedPathLine;
            gridCombatIndicator = newGridCombatIndicator;
            timeManager = newTimeManager;
            groundLayerMask = newGroundLayerMask;
            unitLayerMask = newUnitLayerMask;
        }

        public void SetEditorGridCombatIndicator(GridCombatIndicator newGridCombatIndicator)
        {
            gridCombatIndicator = newGridCombatIndicator;
        }
#endif
    }
}
