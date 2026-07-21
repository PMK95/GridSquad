using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum CombatControlMode
    {
        FullAutomatic,
        PlayerMovementAutomaticActions,
        PlayerMovementPlayerActions
    }

    public enum CombatActionKind
    {
        BasicAttack,
        Reposition,
        Grenade,
        Stim,
        Dash
    }

    public enum CombatActionTargetType
    {
        None,
        Self,
        Combatant,
        GridCell
    }

    public enum CombatActionSelectionSource
    {
        Automatic,
        Player
    }

    public enum CombatActionExecutionStatus
    {
        Idle,
        Running,
        Completed,
        Failed
    }

    [CreateAssetMenu(menuName = "GridSquad/Combat Action Definition", fileName = "CombatActionDefinition")]
    public sealed class CombatActionDefinition : ScriptableObject
    {
        [SerializeField] private CombatActionKind kind;
        [SerializeField] private string displayName = "ACTION";
        [SerializeField] private CombatActionTargetType targetType;
        [SerializeField] private bool automaticInFullAuto = true;
        [SerializeField] private bool automaticInSemiAuto = true;
        [SerializeField, Min(-1)] private int startingCharges = -1;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0f)] private float windupSeconds = 0.5f;

        [Header("수류탄")]
        [SerializeField, Min(1)] private int grenadeRangeCells = 5;
        [SerializeField, Min(0)] private int grenadeRadiusCells = 1;
        [SerializeField, Min(1)] private int grenadeDamage = 40;
        [SerializeField, Min(0.05f)] private float grenadeTravelSeconds = 0.4f;
        [SerializeField, Min(0.1f)] private float grenadeFuseSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float grenadeCameraShakeDuration = 0.28f;
        [SerializeField, Min(0f)] private float grenadeCameraShakeAmplitude = 1.4f;
        [SerializeField, Min(0f)] private float grenadeCameraShakeFrequency = 24f;

        [Header("자극제")]
        [SerializeField, Min(0.1f)] private float stimDurationSeconds = 8f;
        [SerializeField, Min(1f)] private float stimMovementSpeedMultiplier = 1.35f;
        [SerializeField, Range(0.1f, 1f)] private float stimFireIntervalMultiplier = 0.75f;

        [Header("돌진")]
        [SerializeField, Min(1)] private int dashMaximumCells = 3;
        [SerializeField, Min(1f)] private float dashMovementSpeedMultiplier = 3f;
        [SerializeField, Min(0f)] private float dashMinimumPositionImprovement = 15f;

        public CombatActionKind Kind => kind;
        public string DisplayName => displayName;
        public CombatActionTargetType TargetType => targetType;
        public bool AutomaticInFullAuto => automaticInFullAuto;
        public bool AutomaticInSemiAuto => automaticInSemiAuto;
        public int StartingCharges => startingCharges;
        public float CooldownSeconds => cooldownSeconds;
        public float WindupSeconds => windupSeconds;
        public int GrenadeRangeCells => grenadeRangeCells;
        public int GrenadeRadiusCells => grenadeRadiusCells;
        public int GrenadeDamage => grenadeDamage;
        public float GrenadeTravelSeconds => grenadeTravelSeconds;
        public float GrenadeFuseSeconds => grenadeFuseSeconds;
        public float GrenadeCameraShakeDuration => grenadeCameraShakeDuration;
        public float GrenadeCameraShakeAmplitude => grenadeCameraShakeAmplitude;
        public float GrenadeCameraShakeFrequency => grenadeCameraShakeFrequency;
        public float StimDurationSeconds => stimDurationSeconds;
        public float StimMovementSpeedMultiplier => stimMovementSpeedMultiplier;
        public float StimFireIntervalMultiplier => stimFireIntervalMultiplier;
        public int DashMaximumCells => dashMaximumCells;
        public float DashMovementSpeedMultiplier => dashMovementSpeedMultiplier;
        public float DashMinimumPositionImprovement => dashMinimumPositionImprovement;

        public static CombatActionDefinition CreateRuntimeDefault(CombatActionKind actionKind)
        {
            CombatActionDefinition definition = CreateInstance<CombatActionDefinition>();
            definition.kind = actionKind;
            definition.displayName = actionKind switch
            {
                CombatActionKind.Grenade => "수류탄",
                CombatActionKind.Stim => "자극제",
                CombatActionKind.Dash => "돌진",
                _ => actionKind.ToString()
            };
            definition.targetType = actionKind switch
            {
                CombatActionKind.Stim => CombatActionTargetType.Self,
                CombatActionKind.Grenade => CombatActionTargetType.GridCell,
                CombatActionKind.Dash => CombatActionTargetType.GridCell,
                _ => CombatActionTargetType.None
            };
            definition.startingCharges = actionKind == CombatActionKind.Dash ? -1 : 1;
            definition.cooldownSeconds = actionKind == CombatActionKind.Dash ? 6f : 0f;
            definition.windupSeconds = actionKind == CombatActionKind.Dash ? 0f : 0.5f;
            definition.automaticInSemiAuto = actionKind != CombatActionKind.Dash;
            return definition;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            CombatActionKind newKind,
            string newDisplayName,
            CombatActionTargetType newTargetType,
            bool newAutomaticInFullAuto,
            bool newAutomaticInSemiAuto,
            int newStartingCharges,
            float newCooldownSeconds,
            float newWindupSeconds)
        {
            kind = newKind;
            displayName = newDisplayName;
            targetType = newTargetType;
            automaticInFullAuto = newAutomaticInFullAuto;
            automaticInSemiAuto = newAutomaticInSemiAuto;
            startingCharges = newStartingCharges;
            cooldownSeconds = newCooldownSeconds;
            windupSeconds = newWindupSeconds;
        }

        public void SetEditorGrenadeConfiguration(
            int rangeCells,
            int radiusCells,
            int damage,
            float travelSeconds,
            float fuseSeconds,
            float cameraShakeDuration,
            float cameraShakeAmplitude,
            float cameraShakeFrequency)
        {
            grenadeRangeCells = rangeCells;
            grenadeRadiusCells = radiusCells;
            grenadeDamage = damage;
            grenadeTravelSeconds = travelSeconds;
            grenadeFuseSeconds = fuseSeconds;
            grenadeCameraShakeDuration = cameraShakeDuration;
            grenadeCameraShakeAmplitude = cameraShakeAmplitude;
            grenadeCameraShakeFrequency = cameraShakeFrequency;
        }

        public void SetEditorStimConfiguration(
            float durationSeconds,
            float movementMultiplier,
            float fireIntervalMultiplier)
        {
            stimDurationSeconds = durationSeconds;
            stimMovementSpeedMultiplier = movementMultiplier;
            stimFireIntervalMultiplier = fireIntervalMultiplier;
        }

        public void SetEditorDashConfiguration(
            int maximumCells,
            float movementMultiplier,
            float minimumPositionImprovement)
        {
            dashMaximumCells = maximumCells;
            dashMovementSpeedMultiplier = movementMultiplier;
            dashMinimumPositionImprovement = minimumPositionImprovement;
        }
#endif
    }

    public sealed class UtilityScoreBreakdown
    {
        private readonly List<string> entries = new();

        public float Total { get; private set; }
        public IReadOnlyList<string> Entries => entries;

        public UtilityScoreBreakdown Add(string label, float value)
        {
            Total += value;
            entries.Add($"{label} {value:+0.0;-0.0;0.0}");
            return this;
        }

        public override string ToString()
            => entries.Count == 0 ? "-" : string.Join(" | ", entries);
    }

    public readonly struct CombatActionCandidate
    {
        public readonly CombatActionKind Kind;
        public readonly Combatant Target;
        public readonly GridCoordinate TargetCell;
        public readonly bool HasTargetCell;
        public readonly float UtilityScore;
        public readonly UtilityScoreBreakdown Breakdown;

        public CombatActionCandidate(
            CombatActionKind kind,
            Combatant target,
            GridCoordinate targetCell,
            bool hasTargetCell,
            UtilityScoreBreakdown breakdown)
        {
            Kind = kind;
            Target = target;
            TargetCell = targetCell;
            HasTargetCell = hasTargetCell;
            Breakdown = breakdown;
            UtilityScore = Mathf.Clamp(breakdown?.Total ?? 0f, 0f, 100f);
        }
    }

    public readonly struct CombatActionIntent
    {
        public readonly CombatActionCandidate Candidate;
        public readonly CombatActionSelectionSource Source;

        public CombatActionIntent(
            CombatActionCandidate candidate,
            CombatActionSelectionSource source)
        {
            Candidate = candidate;
            Source = source;
        }
    }

    public readonly struct CombatActionContext
    {
        public readonly Combatant Actor;
        public readonly CombatDirector Director;
        public readonly GridMap GridMap;
        public readonly ShotEvaluator ShotEvaluator;
        public readonly TacticalPositionEvaluator PositionEvaluator;
        public readonly CombatControlMode ControlMode;
        public readonly Combatant PriorityTarget;
        public readonly bool AutomaticPeekAllowed;

        public CombatActionContext(
            Combatant actor,
            CombatDirector director,
            GridMap gridMap,
            ShotEvaluator shotEvaluator,
            TacticalPositionEvaluator positionEvaluator,
            CombatControlMode controlMode,
            Combatant priorityTarget,
            bool automaticPeekAllowed)
        {
            Actor = actor;
            Director = director;
            GridMap = gridMap;
            ShotEvaluator = shotEvaluator;
            PositionEvaluator = positionEvaluator;
            ControlMode = controlMode;
            PriorityTarget = priorityTarget;
            AutomaticPeekAllowed = automaticPeekAllowed;
        }
    }

    public interface ICombatAction
    {
        CombatActionKind Kind { get; }
        void CollectAutomaticCandidates(CombatActionContext context, List<CombatActionCandidate> results);
        bool CanStart(CombatActionCandidate candidate, out string failureReason);
        bool Start(CombatActionIntent intent);
        CombatActionExecutionStatus Tick(float deltaTime);
        void RequestStop();
    }
}
