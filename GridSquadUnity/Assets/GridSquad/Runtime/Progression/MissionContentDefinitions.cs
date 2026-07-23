using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [Serializable]
    public struct EnemySpawnDefinition
    {
        [SerializeField] private string spawnId;
        [SerializeField] private UnitDefinition unitDefinition;

        public string SpawnId => spawnId;
        public UnitDefinition UnitDefinition => unitDefinition;

        public EnemySpawnDefinition(string spawnId, UnitDefinition unitDefinition)
        {
            this.spawnId = spawnId;
            this.unitDefinition = unitDefinition;
        }
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Missions/Encounter Definition",
        fileName = "EncounterDefinition")]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [SerializeField] private string encounterId = "encounter";
        [SerializeField] private string[] allySpawnIds = Array.Empty<string>();
        [SerializeField] private EnemySpawnDefinition[] enemySpawns =
            Array.Empty<EnemySpawnDefinition>();

        public string EncounterId => encounterId;
        public IReadOnlyList<string> AllySpawnIds => allySpawnIds;
        public IReadOnlyList<EnemySpawnDefinition> EnemySpawns => enemySpawns;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newEncounterId,
            string[] newAllySpawnIds,
            EnemySpawnDefinition[] newEnemySpawns)
        {
            encounterId = newEncounterId;
            allySpawnIds = newAllySpawnIds ?? Array.Empty<string>();
            enemySpawns = newEnemySpawns ?? Array.Empty<EnemySpawnDefinition>();
        }
#endif
    }

    [Serializable]
    public struct MissionStageDefinition
    {
        [SerializeField] private string stageId;
        [SerializeField] private string scenePath;
        [SerializeField] private EncounterDefinition encounter;
        [SerializeField] private bool extractionAllowed;

        public string StageId => stageId;
        public string ScenePath => scenePath;
        public EncounterDefinition Encounter => encounter;
        public bool ExtractionAllowed => extractionAllowed;

        public MissionStageDefinition(
            string stageId,
            string scenePath,
            EncounterDefinition encounter,
            bool extractionAllowed)
        {
            this.stageId = stageId;
            this.scenePath = scenePath;
            this.encounter = encounter;
            this.extractionAllowed = extractionAllowed;
        }
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Missions/Mission Definition",
        fileName = "MissionDefinition")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField] private string missionId = "mission";
        [SerializeField] private string displayName = "임무";
        [SerializeField, Min(1)] private int minimumSquadSize = 1;
        [SerializeField, Min(1)] private int maximumSquadSize = 3;
        [SerializeField] private MissionStageDefinition[] stages =
            Array.Empty<MissionStageDefinition>();

        public string MissionId => missionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        public int MinimumSquadSize => Mathf.Max(1, minimumSquadSize);
        public int MaximumSquadSize => Mathf.Max(MinimumSquadSize, maximumSquadSize);
        public IReadOnlyList<MissionStageDefinition> Stages => stages;

        public bool TryGetStage(int index, out MissionStageDefinition stage)
        {
            if (index >= 0 && index < stages.Length)
            {
                stage = stages[index];
                return true;
            }
            stage = default;
            return false;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newMissionId,
            string newDisplayName,
            int newMinimumSquadSize,
            int newMaximumSquadSize,
            MissionStageDefinition[] newStages)
        {
            missionId = newMissionId;
            displayName = newDisplayName;
            minimumSquadSize = Mathf.Max(1, newMinimumSquadSize);
            maximumSquadSize = Mathf.Max(minimumSquadSize, newMaximumSquadSize);
            stages = newStages ?? Array.Empty<MissionStageDefinition>();
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Progression/Aftereffect Definition",
        fileName = "AftereffectDefinition")]
    public sealed class AftereffectDefinition : ScriptableObject
    {
        [SerializeField] private string aftereffectId = "aftereffect";
        [SerializeField] private string displayName = "후유증";
        [SerializeField] private AftereffectSeverity severity;
        [SerializeField, Min(1)] private int restMissions = 1;
        [SerializeField] private bool blocksDeployment;
        [SerializeField] private UnitStatModifier[] statModifiers =
            Array.Empty<UnitStatModifier>();

        public string AftereffectId => aftereffectId;
        public string DisplayName => displayName;
        public AftereffectSeverity Severity => severity;
        public int RestMissions => Mathf.Max(1, restMissions);
        public bool BlocksDeployment => blocksDeployment;
        public IReadOnlyList<UnitStatModifier> StatModifiers => statModifiers;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newAftereffectId,
            string newDisplayName,
            AftereffectSeverity newSeverity,
            int newRestMissions,
            bool newBlocksDeployment,
            UnitStatModifier[] newStatModifiers)
        {
            aftereffectId = newAftereffectId;
            displayName = newDisplayName;
            severity = newSeverity;
            restMissions = Mathf.Max(1, newRestMissions);
            blocksDeployment = newBlocksDeployment;
            statModifiers = newStatModifiers ?? Array.Empty<UnitStatModifier>();
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Progression/Aftereffect Rule Set",
        fileName = "AftereffectRuleSet")]
    public sealed class AftereffectRuleSet : ScriptableObject
    {
        [SerializeField, Min(0f)] private float minorThresholdRatio = 0.5f;
        [SerializeField, Min(0f)] private float majorThresholdRatio = 1f;
        [SerializeField, Min(0f)] private float severeThresholdRatio = 2f;
        [SerializeField] private AftereffectDefinition[] definitions =
            Array.Empty<AftereffectDefinition>();

        public float MinorThresholdRatio => minorThresholdRatio;
        public float MajorThresholdRatio => majorThresholdRatio;
        public float SevereThresholdRatio => severeThresholdRatio;
        public IReadOnlyList<AftereffectDefinition> Definitions => definitions;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            float newMinorThresholdRatio,
            float newMajorThresholdRatio,
            float newSevereThresholdRatio,
            AftereffectDefinition[] newDefinitions)
        {
            minorThresholdRatio = Mathf.Max(0f, newMinorThresholdRatio);
            majorThresholdRatio = Mathf.Max(minorThresholdRatio, newMajorThresholdRatio);
            severeThresholdRatio = Mathf.Max(majorThresholdRatio, newSevereThresholdRatio);
            definitions = newDefinitions ?? Array.Empty<AftereffectDefinition>();
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Progression/Game Content Catalog",
        fileName = "GameContentCatalog")]
    public sealed class GameContentCatalog : ScriptableObject
    {
        [SerializeField] private UnitDefinition[] units = Array.Empty<UnitDefinition>();
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [SerializeField] private MissionDefinition[] missions = Array.Empty<MissionDefinition>();
        [SerializeField] private AftereffectRuleSet aftereffectRules;
        [SerializeField] private UnitStatCatalog statCatalog;
        [SerializeField] private EquipmentLayoutDefinition equipmentLayout;

        private readonly Dictionary<string, UnitDefinition> unitsById = new();
        private readonly Dictionary<string, ItemDefinition> itemsById = new();
        private readonly Dictionary<string, MissionDefinition> missionsById = new();

        public IReadOnlyList<UnitDefinition> Units => units;
        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<MissionDefinition> Missions => missions;
        public AftereffectRuleSet AftereffectRules => aftereffectRules;
        public UnitStatCatalog StatCatalog => statCatalog;
        public EquipmentLayoutDefinition EquipmentLayout => equipmentLayout;

        public void BuildIndexes()
        {
            BuildIndex(units, unit => unit.UnitId, unitsById, "유닛");
            BuildIndex(items, item => item.ItemId, itemsById, "아이템");
            BuildIndex(missions, mission => mission.MissionId, missionsById, "임무");
        }

        public UnitDefinition GetRequiredUnit(string unitId)
            => GetRequired(unitsById, unitId, "유닛");

        public ItemDefinition GetRequiredItem(string itemId)
            => GetRequired(itemsById, itemId, "아이템");

        public MissionDefinition GetRequiredMission(string missionId)
            => GetRequired(missionsById, missionId, "임무");

        private static void BuildIndex<T>(
            IEnumerable<T> values,
            Func<T, string> getId,
            Dictionary<string, T> target,
            string contentKind) where T : UnityEngine.Object
        {
            target.Clear();
            foreach (T value in values)
            {
                if (value == null)
                    continue;
                string id = getId(value);
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidOperationException($"{contentKind} ID가 비어 있습니다: {value.name}");
                if (!target.TryAdd(id, value))
                    throw new InvalidOperationException($"{contentKind} ID가 중복되었습니다: {id}");
            }
        }

        private static T GetRequired<T>(
            Dictionary<string, T> values,
            string id,
            string contentKind)
        {
            if (values.Count == 0)
                throw new InvalidOperationException("콘텐츠 인덱스가 아직 생성되지 않았습니다.");
            if (string.IsNullOrWhiteSpace(id) || !values.TryGetValue(id, out T value))
                throw new KeyNotFoundException($"{contentKind} ID를 찾을 수 없습니다: {id}");
            return value;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            UnitDefinition[] newUnits,
            ItemDefinition[] newItems,
            MissionDefinition[] newMissions,
            AftereffectRuleSet newAftereffectRules,
            UnitStatCatalog newStatCatalog,
            EquipmentLayoutDefinition newEquipmentLayout)
        {
            units = newUnits ?? Array.Empty<UnitDefinition>();
            items = newItems ?? Array.Empty<ItemDefinition>();
            missions = newMissions ?? Array.Empty<MissionDefinition>();
            aftereffectRules = newAftereffectRules;
            statCatalog = newStatCatalog;
            equipmentLayout = newEquipmentLayout;
        }
#endif
    }
}
