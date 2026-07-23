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

        private readonly List<MissionCombatantStateBridge> activeBridges = new();
        private GameFlowCoordinator flow;
        private bool transitionRunning;
        private bool requestedStageSceneLoaded;
        private string requestedStageScenePath;

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
            DontDestroyOnLoad(gameObject);
            if (catalog == null)
            {
                Debug.LogError("[게임 흐름] 콘텐츠 카탈로그가 지정되지 않았습니다.", this);
                enabled = false;
                return;
            }
            catalog.BuildIndexes();
            GameSessionState session = new(new BaseStateFactory().Create(catalog));
            flow = new GameFlowCoordinator(session, catalog);
            flow.StateChanged += HandleFlowStateChanged;
            flow.EnterBase();
        }

        private void OnDestroy()
        {
            if (flow != null)
                flow.StateChanged -= HandleFlowStateChanged;
            if (Instance == this)
                Instance = null;
        }

        public bool TryStartDefaultMission(
            IReadOnlyList<string> selectedUnitIds,
            out string failureReason)
        {
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
            StartCoroutine(LoadNextStage());
            return true;
        }

        public void ContinueToNextStage()
        {
            if (!CanContinueStage || transitionRunning)
                return;
            StartCoroutine(LoadNextStage());
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

        private IEnumerator LoadNextStage()
        {
            transitionRunning = true;
            StageLaunchRequest request = flow.CreateNextStageLaunchRequest();
            flow.NotifyStageInitializationStarted();
            Time.timeScale = 1f;
            requestedStageSceneLoaded = false;
            requestedStageScenePath = request.Stage.ScenePath;
            SceneManager.sceneLoaded += HandleRequestedStageSceneLoaded;
            AsyncOperation load = SceneManager.LoadSceneAsync(request.Stage.ScenePath);
            if (load == null)
            {
                SceneManager.sceneLoaded -= HandleRequestedStageSceneLoaded;
                flow.NotifyFailure($"스테이지 씬 로드를 시작하지 못했습니다: {request.Stage.ScenePath}");
                transitionRunning = false;
                yield break;
            }

            float timeoutAt = Time.realtimeSinceStartup + 30f;
            while (!requestedStageSceneLoaded && Time.realtimeSinceStartup < timeoutAt)
                yield return null;
            SceneManager.sceneLoaded -= HandleRequestedStageSceneLoaded;
            if (!requestedStageSceneLoaded)
            {
                flow.NotifyFailure($"스테이지 씬 로드 시간이 초과되었습니다: {request.Stage.ScenePath}");
                transitionRunning = false;
                yield break;
            }
            yield return null;

            try
            {
                ConfigureLoadedCombatStage(request);
                flow.NotifyStageStarted();
            }
            catch (Exception exception)
            {
                flow.NotifyFailure(exception.Message);
                transitionRunning = false;
                yield break;
            }
            transitionRunning = false;
        }

        private void HandleRequestedStageSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            requestedStageSceneLoaded =
                string.Equals(
                    scene.path,
                    requestedStageScenePath,
                    StringComparison.Ordinal)
                || string.Equals(
                    scene.name,
                    System.IO.Path.GetFileNameWithoutExtension(requestedStageScenePath),
                    StringComparison.Ordinal);
        }

        private void ConfigureLoadedCombatStage(StageLaunchRequest request)
        {
            CombatDirector director = FindFirstObjectByType<CombatDirector>()
                ?? throw new InvalidOperationException("전투 씬에 CombatDirector가 없습니다.");
            List<Combatant> allies = FindObjectsByType<Combatant>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(unit => unit.Team == Team.Ally)
                .OrderBy(unit => unit.name, StringComparer.Ordinal)
                .ToList();
            List<Combatant> enemies = FindObjectsByType<Combatant>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
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
            director.BattleConcluded += HandleBattleConcluded;
            director.SetAllyControlMode(CombatControlMode.PlayerMovementAutomaticActions);
            director.StartBattleWithCurrentLoadouts();
        }

        private void HandleBattleConcluded(BattleResult result)
        {
            CombatDirector director = FindFirstObjectByType<CombatDirector>();
            if (director != null)
                director.BattleConcluded -= HandleBattleConcluded;
            foreach (MissionCombatantStateBridge bridge in activeBridges)
                bridge?.CommitCurrentHealth();
            activeBridges.Clear();
            StartCoroutine(CompleteBattleAfterPresentation(result));
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
            StartCoroutine(LoadBaseScene());
        }

        private IEnumerator LoadBaseScene()
        {
            transitionRunning = true;
            Time.timeScale = 1f;
            AsyncOperation load = SceneManager.LoadSceneAsync(baseScenePath);
            while (load != null && !load.isDone)
                yield return null;
            flow.NotifyReturnedToBase();
            transitionRunning = false;
        }

        private void HandleFlowStateChanged(GameFlowState state)
        {
            StateChanged?.Invoke();
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            GameContentCatalog newCatalog,
            string newBaseScenePath)
        {
            catalog = newCatalog;
            baseScenePath = newBaseScenePath;
        }
#endif
    }
}
