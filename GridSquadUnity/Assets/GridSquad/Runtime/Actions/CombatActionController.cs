using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GridSquad
{
    public readonly struct CombatActionRuntimeState
    {
        public readonly CombatActionDefinition Definition;
        public readonly int PlayerSlotIndex;
        public readonly bool IsEquipped;
        public readonly bool IsInteractable;
        public readonly bool IsRunning;
        public readonly int RemainingCharges;
        public readonly float CooldownRemaining;
        public readonly CombatActionExecutionPhase ExecutionPhase;
        public readonly float PhaseDuration;
        public readonly float PhaseRemaining;
        public readonly bool IsReloading;
        public readonly float ReloadProgress;
        public readonly float ReloadRemaining;
        public readonly string StatusText;
        public readonly string RuntimeKey;
        public readonly string SourceDisplayName;
        public readonly bool IsPassive;

        public CombatActionRuntimeState(
            CombatActionDefinition definition,
            int playerSlotIndex,
            bool isEquipped,
            bool isInteractable,
            bool isRunning,
            int remainingCharges,
            float cooldownRemaining,
            CombatActionExecutionPhase executionPhase,
            float phaseDuration,
            float phaseRemaining,
            bool isReloading,
            float reloadProgress,
            float reloadRemaining,
            string statusText,
            string runtimeKey = null,
            string sourceDisplayName = null,
            bool isPassive = false)
        {
            Definition = definition;
            PlayerSlotIndex = playerSlotIndex;
            IsEquipped = isEquipped;
            IsInteractable = isInteractable;
            IsRunning = isRunning;
            RemainingCharges = remainingCharges;
            CooldownRemaining = cooldownRemaining;
            ExecutionPhase = executionPhase;
            PhaseDuration = phaseDuration;
            PhaseRemaining = phaseRemaining;
            IsReloading = isReloading;
            ReloadProgress = reloadProgress;
            ReloadRemaining = reloadRemaining;
            StatusText = statusText;
            RuntimeKey = runtimeKey;
            SourceDisplayName = sourceDisplayName;
            IsPassive = isPassive;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatCommandState))]
    public sealed class CombatActionController : MonoBehaviour
    {
        [SerializeField] private Combatant combatant;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private TacticalPositionEvaluator positionEvaluator;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private CombatActionLoadout loadout;
        [SerializeField] private CombatCommandState commandState;

        private readonly List<CombatActionRuntime> actions = new();
        private readonly List<CombatActionRuntime> playerActionRuntimes = new();
        private readonly List<CombatActionRuntime> candidateActions = new();
        private readonly CombatUtilitySelector utilitySelector = new();
        private CombatActionRuntime currentRuntime;
        private CombatActionIntent currentIntent;
        private CombatActionCandidate lastSelectedCandidate;
        private bool hasLastSelectedCandidate;
        private bool automaticPeekAllowed = true;

        public CombatActionDefinition CurrentActionDefinition => currentRuntime?.Definition;
        public bool HasRunningIntent => currentRuntime != null;
        public bool IsPerformingExclusiveAction => currentRuntime != null
            && currentRuntime.Definition.HasCapability(CombatActionCapabilityFlags.Exclusive);
        public CombatActionCandidate LastSelectedCandidate => lastSelectedCandidate;
        public bool HasLastSelectedCandidate => hasLastSelectedCandidate;
        public IReadOnlyList<CombatActionCandidate> LastCandidates => utilitySelector.Candidates;
        public Combatant OwnerCombatant => combatant;
        public CombatActionIntent CurrentIntent => currentIntent;
        public int PlayerActionCount => playerActionRuntimes.Count;

        internal Combatant Actor => combatant;
        internal GridMap GridMap => gridMap;
        internal ShotEvaluator ShotEvaluator => shotEvaluator;
        internal CombatDirector Director => director;
        internal TacticalPositionEvaluator PositionEvaluator => positionEvaluator;
        internal CombatTuning Tuning => tuning;
        internal bool AutomaticPeekAllowed => automaticPeekAllowed;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            if (combatant != null)
                combatant.Died += HandleCombatantDied;
        }

        private void OnDisable()
        {
            if (combatant != null)
                combatant.Died -= HandleCombatantDied;
            InterruptCurrentIntent(CombatActionInterruptReason.CombatEnded);
        }

        public void ConfigureRuntime(
            Combatant newCombatant,
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatDirector newDirector,
            TacticalPositionEvaluator newPositionEvaluator,
            CombatTuning newTuning)
        {
            combatant = newCombatant;
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            director = newDirector;
            positionEvaluator = newPositionEvaluator;
            tuning = newTuning;
            EnsureReferences();
            BuildRuntimeActions();
        }

        public void RefreshRuntimeActionsFromLoadout()
        {
            EnsureReferences();
            InterruptCurrentIntent(CombatActionInterruptReason.ControlModeChanged);
            BuildRuntimeActions();
        }

        public bool TrySelectAutomaticIntent(
            CombatControlMode controlMode,
            ShootableTarget priorityTarget,
            bool allowAutomaticPeek,
            out CombatActionIntent intent)
        {
            intent = default;
            if (currentRuntime != null || combatant == null || !combatant.IsAlive)
                return false;

            automaticPeekAllowed = allowAutomaticPeek;
            CombatActionContext context = CreateContext(controlMode, priorityTarget, allowAutomaticPeek);
            candidateActions.Clear();
            bool restrictToPriorityTarget = priorityTarget != null && priorityTarget.IsAlive;
            foreach (CombatActionRuntime action in actions)
            {
                if (restrictToPriorityTarget
                    && !action.Definition.HasCapability(CombatActionCapabilityFlags.AllowedWithForcedTarget))
                {
                    continue;
                }
                candidateActions.Add(action);
            }
            if (!utilitySelector.TrySelectHighestUtilityAction(context, candidateActions, out CombatActionCandidate selected))
                return false;

            lastSelectedCandidate = selected;
            hasLastSelectedCandidate = true;
            intent = new CombatActionIntent(
                selected,
                CombatActionSelectionSource.Automatic,
                tuning != null ? tuning.EvaluationRefreshInterval : 0.2f);
            return true;
        }

        public bool TrySelectManualBasicAttackIntent(
            ShootableTarget priorityTarget,
            bool allowAutomaticPeek,
            out CombatActionIntent intent)
        {
            intent = default;
            CombatActionRuntime basicAttack = FindActionWithCapability(CombatActionCapabilityFlags.DefaultAttack);
            if (basicAttack == null)
                return false;
            automaticPeekAllowed = allowAutomaticPeek;
            CombatActionContext context = CreateContext(
                CombatControlMode.PlayerMovementPlayerActions,
                priorityTarget,
                allowAutomaticPeek);
            candidateActions.Clear();
            candidateActions.Add(basicAttack);
            if (!utilitySelector.TrySelectHighestUtilityAction(context, candidateActions, out CombatActionCandidate selected))
                return false;
            lastSelectedCandidate = selected;
            hasLastSelectedCandidate = true;
            intent = new CombatActionIntent(
                selected,
                CombatActionSelectionSource.Automatic,
                tuning != null ? tuning.EvaluationRefreshInterval : 0.2f);
            return true;
        }

        public bool TryBeginIntent(CombatActionIntent intent, out string failureReason)
        {
            if (currentRuntime != null)
                return Fail("다른 행동을 실행 중입니다.", out failureReason);
            CombatActionRuntime runtime = !string.IsNullOrWhiteSpace(intent.Candidate.RuntimeKey)
                ? FindAction(intent.Candidate.RuntimeKey)
                : FindAction(intent.Candidate.Definition);
            if (runtime == null)
                return Fail("행동이 장착되지 않았습니다.", out failureReason);
            if (!runtime.Executor.CanBegin(intent.Candidate, out failureReason)
                || !runtime.Executor.Begin(intent, out failureReason))
            {
                return false;
            }

            currentRuntime = runtime;
            currentIntent = intent;
            lastSelectedCandidate = intent.Candidate;
            hasLastSelectedCandidate = true;
            return true;
        }

        public CombatActionExecutionStatus TickCurrentIntent(float deltaTime)
        {
            TickCooldowns(deltaTime);
            if (currentRuntime == null)
                return CombatActionExecutionStatus.Idle;
            if (combatant == null || !combatant.IsAlive)
            {
                InterruptCurrentIntent(CombatActionInterruptReason.OwnerDied);
                return CombatActionExecutionStatus.Interrupted;
            }
            if (director == null || !director.BattleStarted || director.BattleFinished)
            {
                InterruptCurrentIntent(CombatActionInterruptReason.CombatEnded);
                return CombatActionExecutionStatus.Interrupted;
            }

            CombatActionExecutionStatus status = currentRuntime.Executor.Tick(deltaTime);
            if (status != CombatActionExecutionStatus.Running)
            {
                if (status == CombatActionExecutionStatus.Failed)
                {
                    if (currentRuntime.EffectCommitted)
                        currentRuntime.PreserveRecoveryAfterInterruption();
                    else
                        currentRuntime.CancelBeforeEffect();
                }
                currentRuntime = null;
            }
            return status;
        }

        public void TickCooldowns(float deltaTime)
        {
            foreach (CombatActionRuntime action in actions)
                action.TickTimers(deltaTime);
        }

        public void InterruptCurrentIntent(CombatActionInterruptReason reason)
        {
            if (currentRuntime == null)
                return;
            CombatActionRuntime interruptedRuntime = currentRuntime;
            interruptedRuntime.Executor.Interrupt(reason);
            if (interruptedRuntime.EffectCommitted)
                interruptedRuntime.PreserveRecoveryAfterInterruption();
            else
                interruptedRuntime.CancelBeforeEffect();
            currentRuntime = null;
        }

        public CombatActionDefinition GetPlayerActionDefinition(int slotIndex)
            => slotIndex >= 0 && slotIndex < PlayerActionCount
                ? playerActionRuntimes[slotIndex].Definition
                : null;

        public CombatActionRuntimeState GetPlayerActionRuntimeState(int slotIndex)
        {
            CombatActionRuntime runtime = slotIndex >= 0 && slotIndex < playerActionRuntimes.Count
                ? playerActionRuntimes[slotIndex]
                : null;
            return GetActionRuntimeState(runtime, slotIndex);
        }

        public CombatActionRuntimeState GetActionRuntimeState(CombatActionDefinition definition)
            => GetActionRuntimeState(definition, GetPlayerActionSlotIndex(definition));

        public bool TryQueuePlayerAction(
            int slotIndex,
            CombatActionTargetSelection selection,
            out string failureReason)
        {
            CombatActionRuntime runtime = slotIndex >= 0 && slotIndex < playerActionRuntimes.Count
                ? playerActionRuntimes[slotIndex]
                : null;
            return TryQueuePlayerAction(runtime, selection, out failureReason);
        }

        public bool TryQueuePlayerAction(
            CombatActionDefinition definition,
            CombatActionTargetSelection selection,
            out string failureReason)
        {
            CombatActionRuntime runtime = FindAction(definition);
            return TryQueuePlayerAction(runtime, selection, out failureReason);
        }

        public bool TryQueuePlayerAction(
            string runtimeKey,
            CombatActionTargetSelection selection,
            out string failureReason)
        {
            return TryQueuePlayerAction(FindAction(runtimeKey), selection, out failureReason);
        }

        private bool TryQueuePlayerAction(
            CombatActionRuntime runtime,
            CombatActionTargetSelection selection,
            out string failureReason)
        {
            if (runtime == null)
                return Fail("행동이 장착되지 않았습니다.", out failureReason);
            CombatActionDefinition definition = runtime.Definition;
            CombatActionCandidate candidate = definition.Behavior.CreatePlayerCandidate(
                this,
                runtime,
                selection).WithRuntimeKey(runtime.RuntimeKey);
            if (!runtime.Executor.CanBegin(candidate, out failureReason))
                return false;
            commandState.QueueAction(new CombatActionCommand(candidate));
            combatant?.RequestStopMovementAfterCurrentCell();
            failureReason = string.Empty;
            return true;
        }

        public bool TryCreatePlayerMovementIntent(
            GridCoordinate destination,
            out CombatActionIntent intent)
        {
            CombatActionRuntime runtime = FindActionWithCapability(
                CombatActionCapabilityFlags.MovementCommand);
            if (runtime == null || !runtime.CanUse(out _))
            {
                intent = default;
                return false;
            }
            CombatActionCandidate candidate = new(
                runtime.Definition,
                null,
                destination,
                true,
                new UtilityScoreBreakdown().Add("플레이어 이동 명령", 100f));
            intent = new CombatActionIntent(candidate, CombatActionSelectionSource.Player);
            return true;
        }

        public void CollectValidTargetCells(
            CombatActionDefinition definition,
            HashSet<GridCoordinate> results)
        {
            results.Clear();
            CombatActionRuntime runtime = FindAction(definition);
            if (runtime == null || definition?.Behavior == null)
                return;
            definition.Behavior.CollectValidTargetCells(this, runtime, results);
        }

        public void BuildActionPreview(
            CombatActionDefinition definition,
            CombatActionTargetSelection selection,
            CombatActionPreview preview)
        {
            preview.Reset(new Color(0.2f, 0.9f, 1f, 0.55f));
            CombatActionRuntime runtime = FindAction(definition);
            if (runtime == null || definition?.Behavior == null)
                return;
            definition.Behavior.BuildTargetPreview(this, runtime, selection, preview);
        }

        public bool DoesAreaContainFriendly(GridCoordinate targetCell, int radius)
        {
            if (director == null || combatant == null)
                return false;
            foreach (Combatant target in director.Combatants)
            {
                if (target == null || !target.IsAlive || target.Team != combatant.Team)
                    continue;
                int xDistance = Mathf.Abs(target.CurrentCell.X - targetCell.X);
                int zDistance = Mathf.Abs(target.CurrentCell.Z - targetCell.Z);
                if (Mathf.Max(xDistance, zDistance) <= radius)
                    return true;
            }
            return false;
        }

        public string BuildUtilityDebugText()
        {
            StringBuilder builder = new();
            builder.Append("ACTION ")
                .Append(CurrentActionDefinition != null ? CurrentActionDefinition.ActionId : "-");
            if (hasLastSelectedCandidate)
            {
                builder.Append("\nCHOSEN ")
                    .Append(lastSelectedCandidate.Definition != null
                        ? lastSelectedCandidate.Definition.ActionId
                        : "-")
                    .Append(' ')
                    .Append(lastSelectedCandidate.UtilityScore.ToString("0"));
                builder.Append("\n").Append(lastSelectedCandidate.Breakdown);
            }
            int count = Mathf.Min(3, utilitySelector.Candidates.Count);
            for (int index = 0; index < count; index++)
            {
                CombatActionCandidate candidate = utilitySelector.Candidates[index];
                builder.Append("\n#").Append(index + 1).Append(' ')
                    .Append(candidate.Definition != null ? candidate.Definition.ActionId : "-")
                    .Append(' ')
                    .Append(candidate.UtilityScore.ToString("0"));
            }
            return builder.ToString();
        }

        private CombatActionRuntimeState GetActionRuntimeState(
            CombatActionDefinition definition,
            int playerSlotIndex)
        {
            CombatActionRuntime runtime = FindAction(definition);
            return GetActionRuntimeState(runtime, playerSlotIndex);
        }

        private CombatActionRuntimeState GetActionRuntimeState(
            CombatActionRuntime runtime,
            int playerSlotIndex)
        {
            CombatActionDefinition definition = runtime?.Definition;
            if (runtime == null)
                return new CombatActionRuntimeState(
                    definition,
                    playerSlotIndex,
                    false,
                    false,
                    false,
                    0,
                    0f,
                    CombatActionExecutionPhase.Idle,
                    0f,
                    0f,
                    false,
                    0f,
                    0f,
                    "미장착");
            bool isRunning = currentRuntime == runtime
                || runtime.Phase is CombatActionExecutionPhase.Windup
                    or CombatActionExecutionPhase.Active
                    or CombatActionExecutionPhase.Recovery;
            bool currentActionAllowsReplacement = currentRuntime == null
                || isRunning
                || currentRuntime.Definition.HasCapability(CombatActionCapabilityFlags.ChangesPosition);
            bool actionUsesWeapon = definition.HasCapability(
                CombatActionCapabilityFlags.DefaultAttack);
            bool isReloading = actionUsesWeapon
                && combatant != null
                && combatant.IsReloading;
            bool isInteractable = combatant != null
                && combatant.IsAlive
                && currentActionAllowsReplacement
                && runtime.RemainingCharges != 0
                && runtime.Phase == CombatActionExecutionPhase.Idle
                && runtime.CooldownRemaining <= 0f
                && !isReloading;
            return new CombatActionRuntimeState(
                definition,
                playerSlotIndex,
                true,
                isInteractable,
                isRunning,
                runtime.RemainingCharges,
                runtime.CooldownRemaining,
                runtime.Phase,
                runtime.PhaseDuration,
                runtime.PhaseRemaining,
                isReloading,
                isReloading ? combatant.ReloadProgress : 0f,
                isReloading ? combatant.FireStateRemainingSeconds : 0f,
                BuildActionStatusText(runtime, isReloading),
                runtime.RuntimeKey,
                runtime.SourceDisplayName,
                definition.DisplayKind == CombatActionDisplayKind.Passive);
        }

        private string BuildActionStatusText(
            CombatActionRuntime runtime,
            bool isReloading)
        {
            if (runtime == null)
                return "미장착";
            if (currentRuntime != null && currentRuntime != runtime)
                return "다른 행동 실행 중";
            if (runtime.RemainingCharges == 0)
                return "수량 없음";
            if (isReloading)
                return $"재장전 {combatant.FireStateRemainingSeconds:0.0}s";
            if (runtime.Phase == CombatActionExecutionPhase.Windup)
                return $"준비 {runtime.PhaseRemaining:0.0}s";
            if (runtime.Phase == CombatActionExecutionPhase.Active)
                return "실행";
            if (runtime.Phase == CombatActionExecutionPhase.Recovery)
                return $"후딜 {runtime.PhaseRemaining:0.0}s";
            if (runtime.CooldownRemaining > 0f)
                return $"재사용 {runtime.CooldownRemaining:0.0}s";
            string behaviorStatus = runtime.Definition.Behavior.GetRuntimeStatusText(this, runtime);
            if (!string.IsNullOrEmpty(behaviorStatus))
                return behaviorStatus;
            return runtime.RemainingCharges < 0 ? "사용 가능" : $"{runtime.RemainingCharges}회";
        }

        private CombatActionContext CreateContext(
            CombatControlMode controlMode,
            ShootableTarget priorityTarget,
            bool allowAutomaticPeek)
        {
            return new CombatActionContext(
                combatant,
                director,
                gridMap,
                shotEvaluator,
                positionEvaluator,
                controlMode,
                priorityTarget,
                allowAutomaticPeek);
        }

        private void BuildRuntimeActions()
        {
            playerActionRuntimes.Clear();
            if (loadout == null)
            {
                Debug.LogError($"[전투 행동] {name}에 행동 로드아웃이 없습니다.", this);
                return;
            }

            List<CombatActionGrant> grants = new();
            AddDefinitionGrants(grants, "innate", "기본", loadout.InnateDefinitions);
            AddDefinitionGrants(grants, "unit", "유닛", loadout.Definitions);
            if (combatant?.Traits != null)
            {
                foreach (UnitTraitDefinition trait in combatant.Traits)
                {
                    if (trait == null)
                        continue;
                    AddDefinitionGrants(
                        grants,
                        $"trait:{trait.name}",
                        trait.DisplayName,
                        trait.GrantedActions);
                }
            }
            EquipmentLoadout equipmentLoadout = combatant != null ? combatant.EquipmentLoadout : null;
            if (equipmentLoadout != null)
                grants.AddRange(equipmentLoadout.EnumerateCombatActionGrants());
            UnitInventory inventory = combatant != null ? combatant.Inventory : null;
            if (inventory != null)
                grants.AddRange(inventory.EnumerateCarriedCombatActionGrants());

            Dictionary<string, CombatActionRuntime> existing = new();
            foreach (CombatActionRuntime runtime in actions)
                if (runtime != null && !existing.ContainsKey(runtime.RuntimeKey))
                    existing.Add(runtime.RuntimeKey, runtime);
            List<CombatActionRuntime> nextActions = new();
            HashSet<string> runtimeKeys = new();
            foreach (CombatActionGrant grant in grants)
            {
                if (grant.Definition == null || string.IsNullOrWhiteSpace(grant.Definition.ActionId))
                    continue;
                if (!runtimeKeys.Add(grant.RuntimeKey))
                    continue;
                CombatActionRuntime runtime = existing.TryGetValue(grant.RuntimeKey, out CombatActionRuntime found)
                    ? found
                    : grant.Definition.CreateRuntime(this, grant);
                if (runtime == null)
                {
                    Debug.LogError($"[전투 행동] {grant.Definition.DisplayName}의 전략 구성이 올바르지 않습니다.", grant.Definition);
                    continue;
                }
                nextActions.Add(runtime);
            }
            if (currentRuntime != null && !nextActions.Contains(currentRuntime))
                InterruptCurrentIntent(CombatActionInterruptReason.ControlModeChanged);
            actions.Clear();
            actions.AddRange(nextActions);

            foreach (CombatActionRuntime runtime in actions)
            {
                if (runtime.Definition.DisplayKind == CombatActionDisplayKind.Passive
                    || runtime.Definition.HasCapability(CombatActionCapabilityFlags.PlayerVisible))
                {
                    playerActionRuntimes.Add(runtime);
                }
            }
            playerActionRuntimes.Sort((left, right) =>
            {
                int displayKind = left.Definition.DisplayKind.CompareTo(right.Definition.DisplayKind);
                if (displayKind != 0)
                    return displayKind;
                int order = left.Definition.PlayerSlotOrder.CompareTo(right.Definition.PlayerSlotOrder);
                if (order != 0)
                    return order;
                order = string.CompareOrdinal(left.Definition.ActionId, right.Definition.ActionId);
                return order != 0
                    ? order
                    : string.CompareOrdinal(left.SourceDisplayName, right.SourceDisplayName);
            });
        }

        private static void AddDefinitionGrants(
            List<CombatActionGrant> grants,
            string sourceKeyPrefix,
            string sourceDisplayName,
            IReadOnlyList<CombatActionDefinition> definitions)
        {
            if (definitions == null)
                return;
            foreach (CombatActionDefinition definition in definitions)
            {
                if (definition == null)
                    continue;
                grants.Add(new CombatActionGrant(
                    $"{sourceKeyPrefix}:{definition.ActionId}",
                    sourceDisplayName,
                    definition,
                    null));
            }
        }

        private int GetPlayerActionSlotIndex(CombatActionDefinition definition)
        {
            if (definition == null)
                return -1;
            for (int index = 0; index < playerActionRuntimes.Count; index++)
            {
                if (playerActionRuntimes[index].Definition == definition)
                    return index;
            }
            return -1;
        }

        private CombatActionRuntime FindAction(string runtimeKey)
        {
            if (string.IsNullOrWhiteSpace(runtimeKey))
                return null;
            foreach (CombatActionRuntime action in actions)
                if (action.RuntimeKey == runtimeKey)
                    return action;
            return null;
        }

        private CombatActionRuntime FindAction(CombatActionDefinition definition)
        {
            if (definition == null)
                return null;
            foreach (CombatActionRuntime action in actions)
            {
                if (action.Definition == definition)
                    return action;
            }
            return null;
        }

        private CombatActionRuntime FindActionWithCapability(CombatActionCapabilityFlags capability)
        {
            foreach (CombatActionRuntime action in actions)
            {
                if (action.Definition.HasCapability(capability))
                    return action;
            }
            return null;
        }

        private void EnsureReferences()
        {
            if (combatant == null)
                combatant = GetComponent<Combatant>();
            if (loadout == null)
                loadout = GetComponent<CombatActionLoadout>();
            if (commandState == null)
                commandState = GetComponent<CombatCommandState>();
            if (commandState == null)
                commandState = gameObject.AddComponent<CombatCommandState>();
        }

        private void HandleCombatantDied(Combatant deadCombatant)
        {
            InterruptCurrentIntent(CombatActionInterruptReason.OwnerDied);
        }

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Combatant newCombatant,
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatDirector newDirector,
            TacticalPositionEvaluator newPositionEvaluator,
            CombatTuning newTuning,
            CombatActionLoadout newLoadout)
        {
            combatant = newCombatant;
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            director = newDirector;
            positionEvaluator = newPositionEvaluator;
            tuning = newTuning;
            loadout = newLoadout;
            commandState = GetComponent<CombatCommandState>();
        }
#endif
    }
}
