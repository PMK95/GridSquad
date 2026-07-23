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
        [SerializeField] private LayerMask coverLayerMask;
        [SerializeField] private ContextFloatingMenuController contextMenu;
        [SerializeField] private SelectionDetailWindowController detailWindow;

        private readonly List<Vector3> pathPoints = new();
        private InputActionMap tacticalMap;
        private TacticalEntity selectedEntity;
        private Combatant selectedCombatant;
        private bool targetingMode;
        private CombatActionDefinition targetingActionDefinition;
        private string targetingActionRuntimeKey;
        private bool paused;
        private float activeTimeScale = 1f;
        private bool runtimeInitialized;

        public bool Paused => paused;
        public float ActiveTimeScale => activeTimeScale;
        public TacticalEntity SelectedEntity => selectedEntity;
        public Combatant SelectedCombatant => selectedCombatant;
        public bool IsTargetCommandActive => targetingMode && targetingActionDefinition == null;
        public CombatActionDefinition TargetingActionDefinition => targetingActionDefinition;

        public void InitializeRuntime(MMTimeManager newTimeManager = null)
        {
            if (runtimeInitialized)
                return;
            timeManager = newTimeManager != null ? newTimeManager : timeManager;
            if (coverLayerMask.value == 0)
            {
                int coverLayer = LayerMask.NameToLayer("Cover");
                if (coverLayer >= 0)
                    coverLayerMask = 1 << coverLayer;
            }
            tacticalMap = inputActions.FindActionMap("Tactical", true);
            EnsureRuntimeCombatInputActions();
            hud.SetTimeScaleDisplay(activeTimeScale, false);
            hud.SetTargetingState(false);
            hud.SetDebugState(false);
            hud.SetSelectedEntity(null);
            gridCombatIndicator.SetSelectedCombatant(null);
            runtimeInitialized = true;
            if (isActiveAndEnabled)
                tacticalMap.Enable();
        }

        private void OnEnable()
        {
            if (runtimeInitialized)
                tacticalMap?.Enable();
            if (selectedEntity != null)
            {
                selectedEntity.BecameUnavailable -= HandleSelectedEntityUnavailable;
                selectedEntity.BecameUnavailable += HandleSelectedEntityUnavailable;
            }
            if (selectedCombatant != null)
            {
                selectedCombatant.Died -= HandleSelectedCombatantDied;
                selectedCombatant.Died += HandleSelectedCombatantDied;
            }
        }

        private void OnDisable()
        {
            selectedCombatant?.ClearManualTargetHoverPreview();
            if (selectedEntity != null)
                selectedEntity.BecameUnavailable -= HandleSelectedEntityUnavailable;
            if (selectedCombatant != null)
                selectedCombatant.Died -= HandleSelectedCombatantDied;
            tacticalMap?.Disable();
        }

        private void Update()
        {
            if (!runtimeInitialized)
                return;
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
                BeginSelectedTargetCommandFromHud();
            if (ActionPressed("CycleControlMode"))
                director.CycleAllyControlMode();
            if (ActionPressed("ActionSlot1"))
                BeginSelectedActionFromHud(0);
            if (ActionPressed("ActionSlot2"))
                BeginSelectedActionFromHud(1);
            if (ActionPressed("ActionSlot3"))
                BeginSelectedActionFromHud(2);
            if (ActionPressed("ActionSlot4"))
                BeginSelectedActionFromHud(3);
            if (ActionPressed("TogglePeek"))
                ToggleSelectedAutomaticPeekFromHud();
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
            RefreshEnemyTargetHoverPreview();
            RefreshActionTargetPreview();
        }

        private bool ActionPressed(string actionName)
            => tacticalMap.FindAction(actionName, true).WasPressedThisFrame();

        private void EnsureRuntimeCombatInputActions()
        {
            EnsureRuntimeButtonAction("CycleControlMode", "<Keyboard>/tab");
            EnsureRuntimeButtonAction("ActionSlot1", "<Keyboard>/g");
            EnsureRuntimeButtonAction("ActionSlot2", "<Keyboard>/v");
            EnsureRuntimeButtonAction("ActionSlot3", "<Keyboard>/x");
            EnsureRuntimeButtonAction("ActionSlot4", "<Keyboard>/c");
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

            if (targetingActionDefinition != null)
            {
                if (targetingActionDefinition.TargetingMode == CombatActionTargetingMode.ShootableTarget)
                    TryExecuteShootableTargetedAction();
                else
                    TryExecuteCellTargetedAction();
                return;
            }

            if (targetingMode)
            {
                if (selectedCombatant != null
                    && TryRaycastShootableTarget(out ShootableTarget target)
                    && IsValidTargetCommand(target))
                {
                    UnitTacticalBehaviorController behaviorController =
                        selectedCombatant.GetComponent<UnitTacticalBehaviorController>();
                    behaviorController?.SetPriorityTargetCommand(target);
                    CancelTargeting();
                }
                return;
            }

            if (!TryRaycastSelectableEntity(out TacticalEntity clicked))
            {
                SetSelectedEntity(null);
                return;
            }

            SetSelectedEntity(clicked);
        }

        private void HandleMoveCommand()
        {
            if (targetingMode
                || targetingActionDefinition != null)
                return;
            if (IsPointerOverHud())
                return;
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask, QueryTriggerInteraction.Ignore))
                return;
            GridCoordinate clickedCell = gridMap.WorldToGrid(hit.point);
            contextMenu?.HideMenu();
            List<ContextCommand> commands = new();
            Combatant commandActor = selectedCombatant != null
                && selectedCombatant.Team == Team.Ally
                && selectedCombatant.IsAlive
                    ? selectedCombatant
                    : null;
            if (commandActor == null)
                return;
            TacticalEntity targetEntity = TryRaycastSelectableEntity(out TacticalEntity pointedEntity)
                ? pointedEntity
                : null;
            ContextCommandQuery query = new(commandActor, targetEntity, detailWindow, hud);
            if (targetEntity?.ShootableTarget != null)
                AddAttackDesignationCommand(commandActor, targetEntity.ShootableTarget, commands);
            WorldItemPickup pointedPickup = targetEntity != null
                ? targetEntity.GetComponent<WorldItemPickup>()
                : null;
            pointedPickup?.CollectAvailableContextCommands(query, commands);
            foreach (WorldItemPickup pickup in WorldItemPickup.GetItemsAt(clickedCell))
            {
                if (pickup != null && pickup != pointedPickup)
                    pickup.CollectAvailableContextCommands(query, commands);
            }
            if (commands.Count > 0 && contextMenu != null)
            {
                contextMenu.ShowContextCommandsAtPointer(pointer, commands);
                return;
            }
            if (targetEntity?.Combatant != null)
                return;
            UnitTacticalBehaviorController behaviorController =
                commandActor.GetComponent<UnitTacticalBehaviorController>();
            behaviorController?.QueueMoveCommand(clickedCell);
        }

        private void AddAttackDesignationCommand(
            Combatant commandActor,
            ShootableTarget target,
            List<ContextCommand> commands)
        {
            if (commandActor == null
                || target == null
                || !target.IsAlive
                || target == commandActor.ShootableTarget
                || target.TargetTeam == commandActor.Team)
            {
                return;
            }

            CombatActionController actionController =
                commandActor.GetComponent<CombatActionController>();
            ShotEvaluation evaluation = actionController != null
                ? actionController.ShotEvaluator.EvaluateShot(commandActor, target)
                : default;
            string detail = evaluation.CanShoot
                ? $"현재 사격 가능 · 명중 {evaluation.HitChancePercent:0}%"
                : $"현재 사격 불가 · {GetShotFailureLabel(evaluation.FailureReason)}";
            UnitTacticalBehaviorController behaviorController =
                commandActor.GetComponent<UnitTacticalBehaviorController>();
            commands.Add(new ContextCommand(
                $"combat.attack-designation.{target.GetInstanceID()}",
                "공격 지정",
                commandActor.Weapon != null ? commandActor.Weapon.Icon : null,
                0,
                behaviorController != null,
                behaviorController != null ? string.Empty : "전투 명령을 처리할 수 없습니다.",
                () =>
                {
                    behaviorController?.SetPriorityTargetCommand(target);
                    hud?.SetActionMessage($"{target.DisplayName} 공격 지정");
                },
                detail));
        }

        private static string GetShotFailureLabel(ShotFailureReason reason)
            => reason switch
            {
                ShotFailureReason.NoTarget => "대상 없음",
                ShotFailureReason.TargetDead => "대상 전투 불능",
                ShotFailureReason.OutOfRange => "사거리 밖",
                ShotFailureReason.FullyBlocked => "완전 엄폐",
                ShotFailureReason.NoPeekPosition => "사격 위치 없음",
                _ => "사격 조건 미충족"
            };

        private static bool IsPointerOverHud()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private bool TryRaycastSelectableEntity(out TacticalEntity entity)
        {
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                500f,
                ~0,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                entity = hit.collider.GetComponentInParent<TacticalEntity>();
                if (entity != null && entity.IsAvailable && entity.IsSelectable)
                    return true;
            }
            entity = null;
            return false;
        }

        private bool TryRaycastShootableTarget(out ShootableTarget target)
        {
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            int targetLayerMask = unitLayerMask.value | coverLayerMask.value;
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, targetLayerMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<ShootableTarget>();
                return target != null && target.IsAlive;
            }
            target = null;
            return false;
        }

        private bool IsValidTargetCommand(ShootableTarget target)
            => selectedCombatant != null
                && target != null
                && target != selectedCombatant.ShootableTarget
                && target.IsAlive
                && target.TargetTeam != selectedCombatant.Team;

        public void SelectCombatantFromRoster(Combatant combatant)
        {
            if (combatant == null
                || combatant.Team != Team.Ally
                || !combatant.IsAlive)
            {
                return;
            }

            SetSelectedEntity(combatant.Entity);
        }

        public void BeginSelectedTargetCommandFromHud()
        {
            SetEnemyTargetingMode(!targetingMode || targetingActionDefinition != null);
        }

        public void ToggleSelectedAutomaticPeekFromHud()
        {
            if (selectedCombatant == null
                || selectedCombatant.Team != Team.Ally
                || !selectedCombatant.IsAlive)
            {
                hud.SetActionMessage("생존한 아군을 선택해야 합니다");
                return;
            }

            UnitTacticalBehaviorController behaviorController =
                selectedCombatant.GetComponent<UnitTacticalBehaviorController>();
            if (behaviorController == null)
                return;

            behaviorController.SetAutomaticPeekAllowed(
                !behaviorController.AutomaticPeekAllowed);
            hud.SetActionMessage(
                behaviorController.AutomaticPeekAllowed
                    ? "자동 엄폐 사용"
                    : "자동 엄폐 해제");
        }

        public void BeginSelectedActionFromHud(int slotIndex)
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null)
                return;
            CombatActionRuntimeState actionState = actionController.GetPlayerActionRuntimeState(slotIndex);
            if (!actionState.IsEquipped || !actionState.IsInteractable || actionState.Definition == null)
            {
                hud.SetActionMessage(actionState.StatusText);
                return;
            }

            CombatActionDefinition definition = actionState.Definition;
            if (definition.TargetingMode is CombatActionTargetingMode.GridCell
                or CombatActionTargetingMode.ShootableTarget)
            {
                BeginActionTargeting(actionState.RuntimeKey, definition, actionController);
                return;
            }

            CombatActionTargetSelection selection = definition.TargetingMode == CombatActionTargetingMode.Self
                ? CombatActionTargetSelection.Self(selectedCombatant.CurrentCell)
                : CombatActionTargetSelection.None();
            if (!actionController.TryQueuePlayerAction(actionState.RuntimeKey, selection, out string failureReason))
                hud.SetActionMessage(failureReason);
            else
                hud.SetActionMessage($"{definition.DisplayName} 시작");
            CancelTargeting();
        }

        private void SetSelectedCombatant(Combatant combatant)
        {
            SetSelectedEntity(combatant != null ? combatant.Entity : null);
        }

        private void SetSelectedEntity(TacticalEntity entity)
        {
            if (selectedEntity == entity)
                return;

            if (selectedEntity != null)
                selectedEntity.BecameUnavailable -= HandleSelectedEntityUnavailable;
            if (selectedCombatant != null)
                selectedCombatant.Died -= HandleSelectedCombatantDied;
            selectedCombatant?.ClearManualTargetHoverPreview();
            selectedEntity?.SetSelected(false);
            selectedEntity = entity != null && entity.IsAvailable && entity.IsSelectable
                ? entity
                : null;
            selectedCombatant = selectedEntity != null ? selectedEntity.Combatant : null;
            selectedEntity?.SetSelected(true);
            if (selectedEntity != null)
                selectedEntity.BecameUnavailable += HandleSelectedEntityUnavailable;
            if (selectedCombatant != null)
                selectedCombatant.Died += HandleSelectedCombatantDied;
            hud.SetSelectedEntity(selectedEntity);
            contextMenu?.HideMenu();
            gridCombatIndicator.SetSelectedCombatant(selectedCombatant);
            CancelTargeting();
        }

        private void HandleSelectedEntityUnavailable(TacticalEntity unavailableEntity)
        {
            if (unavailableEntity == null || unavailableEntity != selectedEntity)
                return;
            if (unavailableEntity.Combatant != null)
                return;
            SetSelectedEntity(null);
        }

        private void HandleSelectedCombatantDied(Combatant deadCombatant)
        {
            deadCombatant.Died -= HandleSelectedCombatantDied;
            if (deadCombatant.Team != Team.Ally)
            {
                SetSelectedCombatant(null);
                return;
            }

            foreach (Combatant combatant in director.Combatants)
            {
                if (combatant != null
                    && combatant != deadCombatant
                    && combatant.Team == Team.Ally
                    && combatant.IsAlive)
                {
                    SetSelectedCombatant(combatant);
                    return;
                }
            }

            SetSelectedCombatant(null);
        }

        private void SetEnemyTargetingMode(bool value)
        {
            selectedCombatant?.ClearManualTargetHoverPreview();
            targetingMode = value && selectedCombatant != null && selectedCombatant.Team == Team.Ally;
            targetingActionDefinition = null;
            targetingActionRuntimeKey = null;
            gridCombatIndicator.SetActionTargeting(null, null);
            hud.SetTargetingState(targetingMode);
        }

        private void BeginActionTargeting(
            string runtimeKey,
            CombatActionDefinition definition,
            CombatActionController actionController)
        {
            targetingMode = false;
            targetingActionDefinition = definition;
            targetingActionRuntimeKey = runtimeKey;
            selectedCombatant?.ClearManualTargetHoverPreview();
            gridCombatIndicator.SetActionTargeting(definition, actionController);
            hud.SetActionTargetingState(definition);
        }

        private void TryExecuteCellTargetedAction()
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null || !TryRaycastGroundCell(out GridCoordinate targetCell))
                return;

            if (!actionController.TryQueuePlayerAction(
                    targetingActionRuntimeKey,
                    CombatActionTargetSelection.Cell(targetCell),
                    out string failureReason))
            {
                hud.SetActionMessage(failureReason);
                return;
            }

            hud.SetActionMessage($"{targetingActionDefinition.DisplayName} 시작");
            CancelTargeting();
        }

        private void TryExecuteShootableTargetedAction()
        {
            CombatActionController actionController = GetSelectedAllyActionController();
            if (actionController == null
                || !TryRaycastShootableTarget(out ShootableTarget target))
            {
                return;
            }
            if (!actionController.TryQueuePlayerAction(
                    targetingActionRuntimeKey,
                    CombatActionTargetSelection.Shootable(target),
                    out string failureReason))
            {
                hud.SetActionMessage(failureReason);
                return;
            }
            hud.SetActionMessage($"{targetingActionDefinition.DisplayName} 시작");
            CancelTargeting();
        }

        private void RefreshActionTargetPreview()
        {
            if (targetingActionDefinition == null
                || targetingActionDefinition.TargetingMode != CombatActionTargetingMode.GridCell
                || IsPointerOverHud())
                return;
            if (TryRaycastGroundCell(out GridCoordinate targetCell))
                gridCombatIndicator.SetActionPreviewCell(targetCell);
        }

        private void RefreshEnemyTargetHoverPreview()
        {
            if (!targetingMode
                || selectedCombatant == null
                || !selectedCombatant.IsAlive
                || IsPointerOverHud()
                || !TryRaycastShootableTarget(out ShootableTarget hoveredTarget)
                || !IsValidTargetCommand(hoveredTarget))
            {
                selectedCombatant?.ClearManualTargetHoverPreview();
                return;
            }

            selectedCombatant.SetManualTargetHoverPreview(hoveredTarget);
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
            selectedCombatant?.ClearManualTargetHoverPreview();
            targetingMode = false;
            targetingActionDefinition = null;
            targetingActionRuntimeKey = null;
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

        public void SetRuntimeContextUi(
            ContextFloatingMenuController newContextMenu,
            SelectionDetailWindowController newDetailWindow)
        {
            contextMenu = newContextMenu;
            detailWindow = newDetailWindow;
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
            LayerMask newUnitLayerMask,
            LayerMask newCoverLayerMask)
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
            coverLayerMask = newCoverLayerMask;
        }

        public void SetEditorGridCombatIndicator(GridCombatIndicator newGridCombatIndicator)
        {
            gridCombatIndicator = newGridCombatIndicator;
        }


        public void SetEditorContextUi(
            ContextFloatingMenuController newContextMenu,
            SelectionDetailWindowController newDetailWindow)
        {
            SetRuntimeContextUi(newContextMenu, newDetailWindow);
        }
#endif
    }
}
