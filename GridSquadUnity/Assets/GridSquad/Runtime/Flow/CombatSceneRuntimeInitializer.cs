using System;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GridSquad
{
    public sealed class CombatSceneRuntimeContext
    {
        public Scene Scene { get; }
        public GridMap GridMap { get; }
        public CombatDirector Director { get; }
        public CombatHudController Hud { get; }
        public TacticalInputController InputController { get; }
        public SquadRosterHudController RosterHud { get; }
        public MMTimeManager TimeManager { get; }
        public IReadOnlyList<Combatant> Combatants { get; }

        public CombatSceneRuntimeContext(
            Scene scene,
            GridMap gridMap,
            CombatDirector director,
            CombatHudController hud,
            TacticalInputController inputController,
            SquadRosterHudController rosterHud,
            MMTimeManager timeManager,
            IReadOnlyList<Combatant> combatants)
        {
            Scene = scene;
            GridMap = gridMap;
            Director = director;
            Hud = hud;
            InputController = inputController;
            RosterHud = rosterHud;
            TimeManager = timeManager;
            Combatants = combatants;
        }
    }

    public static class CombatSceneRuntimeInitializer
    {
        public static void SuspendCombatSceneUntilMissionConfiguration(Scene scene)
        {
            foreach (UnitTacticalBehaviorController behavior
                     in FindComponentsInScene<UnitTacticalBehaviorController>(scene))
            {
                behavior.SuspendUntilBattleStart();
            }
        }

        public static CombatSceneRuntimeContext PrepareCombatScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("초기화할 전투 씬이 로드되지 않았습니다.");

            GridMap gridMap = GetRequiredComponent<GridMap>(scene, "GridMap");
            CombatDirector director =
                GetRequiredComponent<CombatDirector>(scene, "CombatDirector");
            CombatHudController hud =
                GetRequiredComponent<CombatHudController>(scene, "CombatHudController");
            TacticalInputController input =
                GetRequiredComponent<TacticalInputController>(scene, "TacticalInputController");
            SquadRosterHudController roster =
                GetRequiredComponent<SquadRosterHudController>(scene, "SquadRosterHudController");
            MMTimeManager timeManager =
                GetRequiredComponent<MMTimeManager>(scene, "MMTimeManager");
            List<Combatant> combatants = FindComponentsInScene<Combatant>(scene);

            gridMap.InitializeRuntime();
            foreach (DestructibleCover cover
                     in FindComponentsInScene<DestructibleCover>(scene))
            {
                cover.ConfigureRuntime(gridMap);
            }
            director.InitializeRuntime(timeManager);
            foreach (Combatant combatant in combatants)
                combatant.InitializeRuntime();
            foreach (UnitItemInteractionController interaction
                     in FindComponentsInScene<UnitItemInteractionController>(scene))
            {
                interaction.InitializeRuntime(hud);
            }
            foreach (UnitTacticalBehaviorController behavior
                     in FindComponentsInScene<UnitTacticalBehaviorController>(scene))
            {
                behavior.InitializeRuntime();
            }

            return new CombatSceneRuntimeContext(
                scene,
                gridMap,
                director,
                hud,
                input,
                roster,
                timeManager,
                combatants);
        }

        public static void InitializeCombatUserInterface(CombatSceneRuntimeContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            SelectionInspectController selectionUi =
                SelectionUiRuntimeBootstrap.CreateOrGetSelectionUi(
                context.InputController,
                context.Hud);
            context.InputController.InitializeRuntime(context.TimeManager);
            context.RosterHud.InitializeRuntime(
                context.Director,
                context.InputController,
                selectionUi);
            foreach (CombatDebugMenuBridge debugBridge
                     in FindComponentsInScene<CombatDebugMenuBridge>(context.Scene))
            {
                debugBridge.InitializeRuntime(
                    context.Director,
                    context.InputController,
                    FindComponentsInScene<UnitTacticalBehaviorController>(context.Scene)
                        .ToArray());
            }
        }

        public static void InitializeStandaloneCombatScene(Scene scene)
        {
            CombatDirector existingDirector =
                FindComponentInScene<CombatDirector>(scene);
            if (existingDirector != null && existingDirector.BattleStarted)
                return;
            CombatSceneRuntimeContext context = PrepareCombatScene(scene);
            context.Director.SetAllyControlMode(
                CombatControlMode.PlayerMovementAutomaticActions);
            InitializeCombatUserInterface(context);
            if (!context.Director.TryStartBattleWithCurrentLoadouts(
                    out string failureReason))
            {
                throw new InvalidOperationException(failureReason);
            }
        }

        private static T GetRequiredComponent<T>(Scene scene, string displayName)
            where T : Component
        {
            T component = FindComponentInScene<T>(scene);
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"{scene.name} 씬에 {displayName}가 없습니다.");
            }
            return component;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            List<T> components = FindComponentsInScene<T>(scene);
            return components.Count > 0 ? components[0] : null;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            List<T> sceneComponents = new();
            foreach (T component in found)
            {
                if (component != null && component.gameObject.scene == scene)
                    sceneComponents.Add(component);
            }
            sceneComponents.Sort(
                (left, right) => string.CompareOrdinal(
                    BuildHierarchyPath(left.transform),
                    BuildHierarchyPath(right.transform)));
            return sceneComponents;
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
                return string.Empty;
            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }
            return path;
        }
    }
}
