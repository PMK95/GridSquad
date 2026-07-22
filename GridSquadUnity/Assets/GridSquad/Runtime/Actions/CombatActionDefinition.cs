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

    public enum CombatActionTargetingMode
    {
        None,
        Self,
        GridCell,
        ShootableTarget
    }

    [Flags]
    public enum CombatActionCapabilityFlags
    {
        None = 0,
        DefaultAttack = 1 << 0,
        ChangesPosition = 1 << 1,
        AllowedWithForcedTarget = 1 << 2,
        Exclusive = 1 << 3,
        PlayerVisible = 1 << 4,
        MovementCommand = 1 << 5
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
        Failed,
        Interrupted
    }

    public enum CombatActionDisplayKind
    {
        Active,
        Passive
    }

    public enum CombatActionInterruptReason
    {
        None,
        PlayerCommand,
        ControlModeChanged,
        TargetInvalid,
        CombatEnded,
        OwnerDied,
        Stunned
    }

    [CreateAssetMenu(menuName = "GridSquad/Combat Action Definition", fileName = "CombatActionDefinition")]
    public sealed class CombatActionDefinition : ScriptableObject
    {
        [SerializeField] private string actionId = "action";
        [SerializeField] private string displayName = "ACTION";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private CombatActionBehaviorDefinition behavior;
        [SerializeField] private bool automaticInFullAuto = true;
        [SerializeField] private bool automaticInSemiAuto = true;
        [SerializeField, Min(-1)] private int startingCharges = -1;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0f)] private float windupSeconds = 0.5f;
        [SerializeField] private int playerSlotOrder = 100;
        [SerializeField] private CombatActionDisplayKind displayKind;

        public string ActionId => actionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public CombatActionBehaviorDefinition Behavior => behavior;
        public CombatActionTargetingMode TargetingMode => behavior != null
            ? behavior.TargetingMode
            : CombatActionTargetingMode.None;
        public CombatActionCapabilityFlags Capabilities => behavior != null
            ? behavior.Capabilities
            : CombatActionCapabilityFlags.None;
        public bool AutomaticInFullAuto => automaticInFullAuto;
        public bool AutomaticInSemiAuto => automaticInSemiAuto;
        public int StartingCharges => startingCharges;
        public float CooldownSeconds => cooldownSeconds;
        public float WindupSeconds => windupSeconds;
        public int PlayerSlotOrder => playerSlotOrder;
        public CombatActionDisplayKind DisplayKind => displayKind;

        public bool HasCapability(CombatActionCapabilityFlags capability)
            => (Capabilities & capability) != 0;

        internal CombatActionRuntime CreateRuntime(
            CombatActionController owner,
            CombatActionGrant grant)
        {
            if (behavior == null)
                return null;

            CombatActionRuntime runtime = new(grant);
            CombatActionRuntimeParts parts = behavior.CreateRuntimeParts(owner, runtime);
            if (parts.CandidateProvider == null || parts.Executor == null)
                return null;
            runtime.Attach(parts.CandidateProvider, parts.Executor);
            return runtime;
        }


        internal CombatActionRuntime CreateRuntime(CombatActionController owner)
            => CreateRuntime(
                owner,
                new CombatActionGrant(
                    $"legacy:{ActionId}",
                    DisplayName,
                    this,
                    null));

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newActionId,
            string newDisplayName,
            CombatActionBehaviorDefinition newBehavior,
            bool newAutomaticInFullAuto,
            bool newAutomaticInSemiAuto,
            int newStartingCharges,
            float newCooldownSeconds,
            float newWindupSeconds)
        {
            actionId = newActionId;
            displayName = newDisplayName;
            behavior = newBehavior;
            automaticInFullAuto = newAutomaticInFullAuto;
            automaticInSemiAuto = newAutomaticInSemiAuto;
            startingCharges = newStartingCharges;
            cooldownSeconds = newCooldownSeconds;
            windupSeconds = newWindupSeconds;
        }

        public void SetEditorPresentation(string newDescription, Sprite newIcon)
        {
            description = newDescription;
            icon = newIcon;
        }
#endif
    }

    public abstract class CombatActionBehaviorDefinition : ScriptableObject
    {
        public abstract CombatActionTargetingMode TargetingMode { get; }
        public abstract CombatActionCapabilityFlags Capabilities { get; }

        internal abstract CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime);

        internal virtual void CollectValidTargetCells(
            CombatActionController owner,
            CombatActionRuntime runtime,
            HashSet<GridCoordinate> results)
        {
        }

        internal virtual void BuildTargetPreview(
            CombatActionController owner,
            CombatActionRuntime runtime,
            CombatActionTargetSelection selection,
            CombatActionPreview preview)
        {
        }

        internal virtual CombatActionCandidate CreatePlayerCandidate(
            CombatActionController owner,
            CombatActionRuntime runtime,
            CombatActionTargetSelection selection)
        {
            GridCoordinate cell = selection.HasTargetCell
                ? selection.TargetCell
                : owner.OwnerCombatant.CurrentCell;
            return new CombatActionCandidate(
                runtime.Definition,
                selection.Target,
                cell,
                selection.HasTargetCell,
                new UtilityScoreBreakdown().Add("플레이어 명령", 100f));
        }

        internal virtual string GetRuntimeStatusText(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => string.Empty;
    }

    internal readonly struct CombatActionRuntimeParts
    {
        public readonly ICombatActionCandidateProvider CandidateProvider;
        public readonly ICombatActionExecutor Executor;

        public CombatActionRuntimeParts(
            ICombatActionCandidateProvider candidateProvider,
            ICombatActionExecutor executor)
        {
            CandidateProvider = candidateProvider;
            Executor = executor;
        }
    }

    public readonly struct CombatActionTargetSelection
    {
        public readonly ShootableTarget Target;
        public readonly GridCoordinate TargetCell;
        public readonly bool HasTargetCell;

        public CombatActionTargetSelection(
            ShootableTarget target,
            GridCoordinate targetCell,
            bool hasTargetCell)
        {
            Target = target;
            TargetCell = targetCell;
            HasTargetCell = hasTargetCell;
        }

        public static CombatActionTargetSelection None()
            => new(null, default, false);

        public static CombatActionTargetSelection Self(GridCoordinate cell)
            => new(null, cell, false);

        public static CombatActionTargetSelection Cell(GridCoordinate cell)
            => new(null, cell, true);

        public static CombatActionTargetSelection Shootable(ShootableTarget target)
            => new(target, target != null ? target.CurrentCell : default, false);
    }

    public sealed class CombatActionPreview
    {
        private readonly List<GridCoordinate> affectedCells = new();

        public IReadOnlyList<GridCoordinate> AffectedCells => affectedCells;
        public Color Color { get; private set; } = new(0.2f, 0.9f, 1f, 0.55f);

        public void Reset(Color color)
        {
            affectedCells.Clear();
            Color = color;
        }

        public void AddAffectedCell(GridCoordinate cell)
        {
            if (!affectedCells.Contains(cell))
                affectedCells.Add(cell);
        }
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
        public readonly CombatActionDefinition Definition;
        public readonly ShootableTarget Target;
        public readonly GridCoordinate TargetCell;
        public readonly bool HasTargetCell;
        public readonly float UtilityScore;
        public readonly UtilityScoreBreakdown Breakdown;
        public readonly string RuntimeKey;

        public CombatActionCandidate(
            CombatActionDefinition definition,
            ShootableTarget target,
            GridCoordinate targetCell,
            bool hasTargetCell,
            UtilityScoreBreakdown breakdown,
            string runtimeKey = null)
        {
            Definition = definition;
            Target = target;
            TargetCell = targetCell;
            HasTargetCell = hasTargetCell;
            Breakdown = breakdown;
            UtilityScore = Mathf.Clamp(breakdown?.Total ?? 0f, 0f, 100f);
            RuntimeKey = runtimeKey;
        }

        public CombatActionCandidate WithRuntimeKey(string runtimeKey)
            => new(
                Definition,
                Target,
                TargetCell,
                HasTargetCell,
                Breakdown,
                runtimeKey);
    }

    public readonly struct CombatActionIntent
    {
        public readonly CombatActionCandidate Candidate;
        public readonly CombatActionSelectionSource Source;
        public readonly float ExecutionDurationSeconds;

        public CombatActionIntent(
            CombatActionCandidate candidate,
            CombatActionSelectionSource source,
            float executionDurationSeconds = 0f)
        {
            Candidate = candidate;
            Source = source;
            ExecutionDurationSeconds = Mathf.Max(0f, executionDurationSeconds);
        }
    }

    public readonly struct CombatActionCommand
    {
        public readonly CombatActionCandidate Candidate;

        public CombatActionCommand(CombatActionCandidate candidate)
        {
            Candidate = candidate;
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
        public readonly ShootableTarget PriorityTarget;
        public readonly bool AutomaticPeekAllowed;

        public CombatActionContext(
            Combatant actor,
            CombatDirector director,
            GridMap gridMap,
            ShotEvaluator shotEvaluator,
            TacticalPositionEvaluator positionEvaluator,
            CombatControlMode controlMode,
            ShootableTarget priorityTarget,
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

    public interface ICombatActionCandidateProvider
    {
        CombatActionDefinition Definition { get; }
        void CollectCandidates(CombatActionContext context, List<CombatActionCandidate> results);
    }

    public interface ICombatActionExecutor
    {
        CombatActionDefinition Definition { get; }
        bool CanBegin(CombatActionCandidate candidate, out string failureReason);
        bool Begin(CombatActionIntent intent, out string failureReason);
        CombatActionExecutionStatus Tick(float deltaTime);
        void Interrupt(CombatActionInterruptReason reason);
    }
}
