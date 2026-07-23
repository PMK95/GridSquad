using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGameApplication : MonoBehaviour
    {
        [SerializeField] private GameContentCatalog catalog;
        [SerializeField] private string baseScenePath = "Assets/GridSquad/Scenes/BasePrototype.unity";
        [SerializeField] private bool persistAcrossSingleSceneLoads = true;

        private readonly List<MissionCombatantStateBridge> activeBridges = new();
        private GameFlowCoordinator flow;
        private CombatDirector activeDirector;
        private GameSceneTransitionManager sceneTransitionManager;
        private StageLaunchRequest pendingStageRequest;
        private PendingSceneTransition pendingSceneTransition;
        private bool transitionRunning;
        private bool applicationInitialized;

        private enum PendingSceneTransition
        {
            None,
            Stage,
            Base
        }

        public static PrototypeGameApplication Instance { get; private set; }
        public event Action StateChanged;
        public GameSessionState Session => flow?.Session;
        public GameFlowState FlowState => flow?.State ?? GameFlowState.Booting;
        public GameContentCatalog Catalog => catalog;
        public MissionDefinition DefaultMission
            => catalog != null && catalog.Missions.Count > 0 ? catalog.Missions[0] : null;
        public bool CanContinueStage
            => flow != null
               && flow.State == GameFlowState.BetweenStages
               && flow.Session.ActiveMission != null;
        public bool CanExtract
        {
            get
            {
                if (!CanContinueStage
                    || !flow.ActiveMissionDefinition.TryGetStage(
                        flow.Session.ActiveMission.NextStageIndex - 1,
                        out MissionStageDefinition completedStage))
                {
                    return false;
                }
                return completedStage.ExtractionAllowed;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossSingleSceneLoads)
                DontDestroyOnLoad(gameObject);
        }

        public void InitializeApplication()
        {
            if (applicationInitialized)
                return;
            if (catalog == null)
            {
                Debug.LogError("[게임 흐름] 콘텐츠 카탈로그가 지정되지 않았습니다.", this);
                throw new InvalidOperationException("콘텐츠 카탈로그가 지정되지 않았습니다.");
            }
            catalog.BuildIndexes();
            GameSessionState session = new(new BaseStateFactory().Create(catalog));
            flow = new GameFlowCoordinator(session, catalog);
            flow.StateChanged += HandleFlowStateChanged;
            sceneTransitionManager =
                GetComponent<GameSceneTransitionManager>()
                ?? gameObject.AddComponent<GameSceneTransitionManager>();
            sceneTransitionManager.InitializeRuntime();
            sceneTransitionManager.DestinationSceneLoaded -= HandleDestinationSceneLoaded;
            sceneTransitionManager.DestinationSceneLoaded += HandleDestinationSceneLoaded;
            InitializeApplicationUserInterface();
            applicationInitialized = true;
        }

        public void InitializeLoadedBaseScene(Scene scene)
        {
            if (!applicationInitialized || !scene.IsValid() || !scene.isLoaded)
                return;
            if (!MatchesBaseScene(scene))
                return;
            InitializeApplicationUserInterface();
            if (!transitionRunning && flow.State == GameFlowState.Booting)
                flow.EnterBase();
        }

        private void OnDestroy()
        {
            if (sceneTransitionManager != null)
                sceneTransitionManager.DestinationSceneLoaded -= HandleDestinationSceneLoaded;
            if (flow != null)
                flow.StateChanged -= HandleFlowStateChanged;
            ReleaseActiveCombatSceneBindings(false);
            if (Instance == this)
                Instance = null;
        }

        public bool TryStartDefaultMission(
            IReadOnlyList<string> selectedUnitIds,
            out string failureReason)
        {
            if (!applicationInitialized || flow == null)
            {
                failureReason = "게임 애플리케이션 초기화가 완료되지 않았습니다.";
                return false;
            }
            if (transitionRunning)
            {
                failureReason = "현재 화면을 전환하고 있습니다.";
                return false;
            }
            MissionDefinition mission = DefaultMission;
            if (!flow.TryStartMission(
                    mission,
                    selectedUnitIds,
                    Environment.TickCount,
                    out failureReason))
            {
                return false;
            }
            return BeginLoadNextStage(out failureReason);
        }

        public void ContinueToNextStage()
        {
            if (!CanContinueStage || transitionRunning)
                return;
            if (!BeginLoadNextStage(out string failureReason))
                Debug.LogError($"[게임 흐름] 다음 스테이지 이동에 실패했습니다: {failureReason}", this);
        }

        public void ExtractToBase()
        {
            if (!CanExtract || transitionRunning)
                return;
            SettleAndReturnToBase(MissionEndReason.Extracted);
        }

        public int RepairAllEquipment()
        {
            if (flow == null || flow.State != GameFlowState.BaseReady)
                return 0;
            int repaired = 0;
            EquipmentRepairService repairService = new();
            foreach (UnitState unit in flow.Session.BaseState.Units)
            {
                foreach (ItemState item in unit.Items)
                {
                    if (catalog.GetRequiredItem(item.ItemDefinitionId) is not EquippableDefinition equipment
                        || item.Durability >= equipment.MaximumDurability)
                    {
                        continue;
                    }
                    if (repairService.TryRepair(
                            flow.Session.BaseState,
                            unit.UnitInstanceId,
                            item.ItemInstanceId,
                            catalog,
                            out _))
                    {
                        repaired++;
                    }
                }
            }
            StateChanged?.Invoke();
            return repaired;
        }

        public bool TryLoadInitialBaseScene(out string failureReason)
        {
            if (!applicationInitialized || flow == null)
            {
                failureReason = "게임 애플리케이션 초기화가 완료되지 않았습니다.";
                return false;
            }
            if (flow.State != GameFlowState.Booting)
            {
                failureReason = flow.State == GameFlowState.BaseReady
                    ? string.Empty
                    : $"초기 기지 이동을 시작할 수 없는 상태입니다: {flow.State}";
                return flow.State == GameFlowState.BaseReady;
            }
            flow.NotifyBaseSceneLoading();
            return BeginSceneTransition(
                PendingSceneTransition.Base,
                baseScenePath,
                true,
                out failureReason);
        }

        private bool BeginLoadNextStage(out string failureReason)
        {
            transitionRunning = true;
            StageLaunchRequest request;
            try
            {
                request = flow.CreateNextStageLaunchRequest();
                flow.NotifyStageInitializationStarted();
            }
            catch (Exception exception)
            {
                flow.NotifyFailure(exception.Message);
                transitionRunning = false;
                failureReason = exception.Message;
                return false;
            }
            pendingStageRequest = request;
            return BeginSceneTransition(
                PendingSceneTransition.Stage,
                request.Stage.ScenePath,
                false,
                out failureReason);
        }

        private bool BeginSceneTransition(
            PendingSceneTransition transition,
            string scenePath,
            bool preserveActiveScene,
            out string failureReason)
        {
            if (transitionRunning && pendingSceneTransition != PendingSceneTransition.None)
            {
                failureReason = "이미 다른 씬으로 이동하고 있습니다.";
                return false;
            }
            transitionRunning = true;
            pendingSceneTransition = transition;
            if (sceneTransitionManager.TryLoadScene(
                    scenePath,
                    preserveActiveScene,
                    out failureReason))
                return true;

            pendingSceneTransition = PendingSceneTransition.None;
            transitionRunning = false;
            flow.NotifyFailure(failureReason);
            return false;
        }

        private void HandleDestinationSceneLoaded(Scene scene)
        {
            PendingSceneTransition completedTransition = pendingSceneTransition;
            pendingSceneTransition = PendingSceneTransition.None;
            try
            {
                switch (completedTransition)
                {
                    case PendingSceneTransition.Stage:
                        ConfigureLoadedCombatStage(pendingStageRequest, scene);
                        flow.NotifyStageStarted();
                        break;
                    case PendingSceneTransition.Base:
                        InitializeLoadedBaseScene(scene);
                        flow.NotifyReturnedToBase();
                        break;
                    case PendingSceneTransition.None:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception exception)
            {
                ReleaseActiveCombatSceneBindings(false);
                flow.NotifyFailure(exception.Message);
            }
            finally
            {
                transitionRunning = false;
            }
        }

        private void ConfigureLoadedCombatStage(
            StageLaunchRequest request,
            Scene combatScene)
        {
            CombatSceneRuntimeContext runtime =
                CombatSceneRuntimeInitializer.PrepareCombatScene(combatScene);
            CombatDirector director = runtime.Director;
            List<Combatant> allies = runtime.Combatants
                .Where(unit => unit.Team == Team.Ally)
                .OrderBy(unit => unit.name, StringComparer.Ordinal)
                .ToList();
            List<Combatant> enemies = runtime.Combatants
                .Where(unit => unit.Team == Team.Enemy)
                .OrderBy(unit => unit.name, StringComparer.Ordinal)
                .ToList();
            if (allies.Count < request.MissionState.DeployedUnits.Count)
                throw new InvalidOperationException("전투 씬의 아군 배치 수가 선발 인원보다 적습니다.");
            int enemyCount = request.Stage.Encounter.EnemySpawns.Count;
            if (enemies.Count < enemyCount)
                throw new InvalidOperationException("전투 씬의 적 배치 수가 조우 데이터보다 적습니다.");

            activeBridges.Clear();
            List<Combatant> stageCombatants = new();
            for (int index = 0; index < allies.Count; index++)
            {
                bool deployed = index < request.MissionState.DeployedUnits.Count;
                if (!deployed)
                {
                    allies[index].GetComponent<GridMovementController>()?.RemoveFromStageGrid();
                    allies[index].gameObject.SetActive(false);
                    continue;
                }
                allies[index].gameObject.SetActive(true);
                MissionUnitState missionUnit = request.MissionState.DeployedUnits[index];
                allies[index].ApplyMissionState(
                    missionUnit,
                    catalog.GetRequiredUnit(missionUnit.UnitDefinitionId),
                    catalog);
                MissionCombatantStateBridge bridge =
                    allies[index].GetComponent<MissionCombatantStateBridge>()
                    ?? allies[index].gameObject.AddComponent<MissionCombatantStateBridge>();
                bridge.Bind(missionUnit);
                activeBridges.Add(bridge);
                stageCombatants.Add(allies[index]);
            }
            for (int index = 0; index < enemies.Count; index++)
            {
                bool deployed = index < enemyCount;
                if (!deployed)
                {
                    enemies[index].GetComponent<GridMovementController>()?.RemoveFromStageGrid();
                    enemies[index].gameObject.SetActive(false);
                    continue;
                }
                enemies[index].gameObject.SetActive(true);
                stageCombatants.Add(enemies[index]);
            }

            director.ConfigureCombatants(stageCombatants);
            if (activeDirector != null)
                activeDirector.BattleConcluded -= HandleBattleConcluded;
            activeDirector = director;
            activeDirector.BattleConcluded -= HandleBattleConcluded;
            activeDirector.BattleConcluded += HandleBattleConcluded;
            director.SetAllyControlMode(CombatControlMode.PlayerMovementAutomaticActions);
            CombatSceneRuntimeInitializer.InitializeCombatUserInterface(runtime);
            if (!director.TryStartBattleWithCurrentLoadouts(out string failureReason))
                throw new InvalidOperationException(failureReason);
        }

        private void HandleBattleConcluded(BattleResult result)
        {
            ReleaseActiveCombatSceneBindings(true);
            StartCoroutine(CompleteBattleAfterPresentation(result));
        }

        private void ReleaseActiveCombatSceneBindings(bool commitMissionState)
        {
            if (activeDirector != null)
                activeDirector.BattleConcluded -= HandleBattleConcluded;
            activeDirector = null;
            foreach (MissionCombatantStateBridge bridge in activeBridges)
            {
                if (bridge == null)
                    continue;
                if (commitMissionState)
                    bridge.CommitCurrentHealth();
                bridge.UnbindFromMission();
            }
            activeBridges.Clear();
        }

        private IEnumerator CompleteBattleAfterPresentation(BattleResult result)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (result == BattleResult.Defeat)
            {
                flow.NotifyStageDefeated();
                SettleAndReturnToBase(MissionEndReason.Defeated);
                yield break;
            }

            flow.CompleteStage(out bool missionCompleted);
            if (missionCompleted)
                SettleAndReturnToBase(MissionEndReason.Completed);
            else
                StateChanged?.Invoke();
        }

        private void SettleAndReturnToBase(MissionEndReason reason)
        {
            flow.SettleMission(reason);
            if (!BeginSceneTransition(
                    PendingSceneTransition.Base,
                    baseScenePath,
                    false,
                    out string failureReason))
            {
                Debug.LogError($"[게임 흐름] 기지 복귀에 실패했습니다: {failureReason}", this);
            }
        }

        private void InitializeApplicationUserInterface()
        {
            PrototypeLoopUiController[] controllers =
                GetComponentsInChildren<PrototypeLoopUiController>(true);
            foreach (PrototypeLoopUiController controller in controllers)
            {
                if (controller != null)
                    controller.InitializeRuntime(this);
            }
        }

        private bool MatchesBaseScene(Scene scene)
            => string.Equals(scene.path, baseScenePath, StringComparison.Ordinal)
               || string.Equals(
                   scene.name,
                   System.IO.Path.GetFileNameWithoutExtension(baseScenePath),
                   StringComparison.Ordinal);

        private void HandleFlowStateChanged(GameFlowState state)
        {
            StateChanged?.Invoke();
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            GameContentCatalog newCatalog,
            string newBaseScenePath,
            bool shouldPersistAcrossSingleSceneLoads = true)
        {
            catalog = newCatalog;
            baseScenePath = newBaseScenePath;
            persistAcrossSingleSceneLoads = shouldPersistAcrossSingleSceneLoads;
        }
#endif
    }
}
