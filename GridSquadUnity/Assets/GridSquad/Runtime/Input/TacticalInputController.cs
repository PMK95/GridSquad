using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GridSquad
{
    public sealed class TacticalInputController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatHudController hud;
        [SerializeField] private LineRenderer selectedPathLine;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private LayerMask unitLayerMask;

        private readonly List<Vector3> pathPoints = new();
        private InputActionMap tacticalMap;
        private Combatant selectedCombatant;
        private bool targetingMode;
        private bool paused;
        private float activeTimeScale = 1f;

        private void Awake()
        {
            tacticalMap = inputActions.FindActionMap("Tactical", true);
            hud.SetTimeScaleDisplay(activeTimeScale, false);
            hud.SetTargetingState(false);
            hud.SetDebugState(false);
            hud.SetSelectedCombatant(null);
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

            if (ActionPressed("Select"))
                HandleLeftClick();
            if (ActionPressed("MoveCommand"))
                HandleMoveCommand();
            if (ActionPressed("TargetCommand"))
                SetTargetingMode(!targetingMode);
            if (ActionPressed("TogglePeek") && selectedCombatant != null && selectedCombatant.Team == Team.Ally)
                selectedCombatant.SetPeekEnabled(!selectedCombatant.PeekEnabled);
            if (ActionPressed("TogglePause"))
                TogglePause();
            if (ActionPressed("Speed1"))
                SetGameSpeed(1f);
            if (ActionPressed("Speed2"))
                SetGameSpeed(2f);
            if (ActionPressed("Speed3"))
                SetGameSpeed(4f);
            if (ActionPressed("Cancel"))
                SetTargetingMode(false);
            if (ActionPressed("ToggleDebug"))
                director.SetDebugVisible(!director.DebugVisible);
            if (ActionPressed("Restart"))
                RestartScene();

            RefreshSelectedPathLine();
        }

        private bool ActionPressed(string actionName)
            => tacticalMap.FindAction(actionName, true).WasPressedThisFrame();

        private static void RestartScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleLeftClick()
        {
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
                    selectedCombatant.SetPriorityTarget(clicked);
                    SetTargetingMode(false);
                }
                return;
            }

            SelectCombatant(clicked);
        }

        private void HandleMoveCommand()
        {
            if (selectedCombatant == null || selectedCombatant.Team != Team.Ally || targetingMode)
                return;
            Vector2 pointer = tacticalMap.FindAction("PointerPosition", true).ReadValue<Vector2>();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask, QueryTriggerInteraction.Ignore))
                return;
            selectedCombatant.SetMoveDestination(gridMap.WorldToGrid(hit.point));
        }

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
            SetTargetingMode(false);
        }

        private void SetTargetingMode(bool value)
        {
            targetingMode = value && selectedCombatant != null && selectedCombatant.Team == Team.Ally;
            hud.SetTargetingState(targetingMode);
        }

        private void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : activeTimeScale;
            hud.SetTimeScaleDisplay(activeTimeScale, paused);
        }

        private void SetGameSpeed(float speed)
        {
            activeTimeScale = speed;
            paused = false;
            Time.timeScale = activeTimeScale;
            hud.SetTimeScaleDisplay(activeTimeScale, false);
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
            LayerMask newGroundLayerMask,
            LayerMask newUnitLayerMask)
        {
            inputActions = newInputActions;
            sceneCamera = newSceneCamera;
            gridMap = newGridMap;
            director = newDirector;
            hud = newHud;
            selectedPathLine = newSelectedPathLine;
            groundLayerMask = newGroundLayerMask;
            unitLayerMask = newUnitLayerMask;
        }
#endif
    }
}
