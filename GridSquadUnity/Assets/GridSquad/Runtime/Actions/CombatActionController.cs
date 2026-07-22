using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GridSquad
{
    public readonly struct CombatActionRuntimeState
    {
        public readonly CombatActionKind Kind;
        public readonly CombatActionDefinition Definition;
        public readonly bool IsEquipped;
        public readonly bool IsInteractable;
        public readonly bool IsRunning;
        public readonly int RemainingCharges;
        public readonly float CooldownRemaining;
        public readonly string StatusText;

        public CombatActionRuntimeState(
            CombatActionKind kind,
            CombatActionDefinition definition,
            bool isEquipped,
            bool isInteractable,
            bool isRunning,
            int remainingCharges,
            float cooldownRemaining,
            string statusText)
        {
            Kind = kind;
            Definition = definition;
            IsEquipped = isEquipped;
            IsInteractable = isInteractable;
            IsRunning = isRunning;
            RemainingCharges = remainingCharges;
            CooldownRemaining = cooldownRemaining;
            StatusText = statusText;
        }
    }

    public sealed class CombatActionController : MonoBehaviour
    {
        [SerializeField] private Combatant combatant;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private TacticalPositionEvaluator positionEvaluator;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private CombatActionLoadout loadout;

        private readonly List<ICombatAction> actions = new();
        private readonly CombatUtilitySelector utilitySelector = new();
        private RuntimeCombatAction currentAction;
        private CombatActionIntent currentIntent;
        private CombatActionCandidate lastSelectedCandidate;
        private bool hasLastSelectedCandidate;
        private int lastExecutionFrame = -1;

        public CombatActionKind CurrentActionKind => currentAction?.Kind ?? CombatActionKind.BasicAttack;
        public bool IsPerformingExclusiveAction => currentAction != null;
        public CombatActionCandidate LastSelectedCandidate => lastSelectedCandidate;
        public bool HasLastSelectedCandidate => hasLastSelectedCandidate;
        public IReadOnlyList<CombatActionCandidate> LastCandidates => utilitySelector.Candidates;
        public Combatant OwnerCombatant => combatant;

        public void RefreshRuntimeActionsFromLoadout()
        {
            if (combatant == null)
                combatant = GetComponent<Combatant>();
            if (loadout == null)
                loadout = GetComponent<CombatActionLoadout>();
            CancelAllActions();
            BuildRuntimeActions();
        }

        private void Awake()
        {
            if (combatant == null)
                combatant = GetComponent<Combatant>();
            if (loadout == null)
                loadout = GetComponent<CombatActionLoadout>();
            BuildRuntimeActions();
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
            if (loadout == null)
                loadout = GetComponent<CombatActionLoadout>();
            BuildRuntimeActions();
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
            CancelAllActions();
        }

        private void Update()
        {
            TickActionExecutionFromBehavior();
        }

        public bool SelectAndStartAutomaticAction(
            CombatControlMode controlMode,
            Combatant priorityTarget,
            bool automaticPeekAllowed)
        {
            if (currentAction != null || combatant == null || !combatant.IsAlive)
                return currentAction != null;

            CombatActionContext context = new(
                combatant,
                director,
                gridMap,
                shotEvaluator,
                positionEvaluator,
                controlMode,
                priorityTarget,
                automaticPeekAllowed);
            if (!utilitySelector.TrySelectHighestUtilityAction(context, actions, out CombatActionCandidate selected))
                return false;

            lastSelectedCandidate = selected;
            hasLastSelectedCandidate = true;
            if (selected.Kind == CombatActionKind.BasicAttack)
            {
                combatant.SetBehaviorTarget(selected.Target);
                combatant.UpdateAutomaticPeekForCurrentTarget(automaticPeekAllowed);
                combatant.RefreshShotEvaluationForCurrentTarget();
                return false;
            }

            return TryStartCandidate(selected, CombatActionSelectionSource.Automatic, out _);
        }

        public void EnsureManualBasicAttackTarget(Combatant priorityTarget, bool automaticPeekAllowed)
        {
            if (currentAction != null || combatant == null || !combatant.IsAlive)
                return;

            Combatant target = priorityTarget;
            if (target == null || !target.IsAlive || target.Team == combatant.Team)
                target = director.FindClosestShootableEnemy(combatant, automaticPeekAllowed);
            combatant.SetBehaviorTarget(target);
            combatant.UpdateAutomaticPeekForCurrentTarget(automaticPeekAllowed);
            combatant.RefreshShotEvaluationForCurrentTarget();
        }

        public bool TryStartPlayerGrenade(GridCoordinate targetCell, out string failureReason)
            => TryStartCandidate(
                CreatePlayerCandidate(CombatActionKind.Grenade, targetCell),
                CombatActionSelectionSource.Player,
                out failureReason);

        public bool TryStartPlayerStim(out string failureReason)
            => TryStartCandidate(
                CreatePlayerCandidate(CombatActionKind.Stim, combatant.CurrentCell),
                CombatActionSelectionSource.Player,
                out failureReason);

        public bool TryStartPlayerDash(GridCoordinate targetCell, out string failureReason)
            => TryStartCandidate(
                CreatePlayerCandidate(CombatActionKind.Dash, targetCell),
                CombatActionSelectionSource.Player,
                out failureReason);

        public bool TryStartPlayerReposition(GridCoordinate targetCell, out string failureReason)
            => TryStartCandidate(
                CreatePlayerCandidate(CombatActionKind.Reposition, targetCell),
                CombatActionSelectionSource.Player,
                out failureReason);

        public bool TryStartPlayerWeaponSwap(out string failureReason)
        {
            if (combatant == null || combatant.WeaponLoadout == null)
                return Fail("무기 로드아웃이 없습니다.", out failureReason);

            int targetSlotIndex = combatant.WeaponLoadout.GetNextSlotIndex();
            CombatActionCandidate candidate = new(
                CombatActionKind.SwitchWeapon,
                null,
                combatant.CurrentCell,
                false,
                new UtilityScoreBreakdown().Add("플레이어 명령", 100f),
                targetSlotIndex);
            return TryStartCandidate(candidate, CombatActionSelectionSource.Player, out failureReason);
        }

        public void TickActionExecutionFromBehavior()
        {
            if (lastExecutionFrame == Time.frameCount)
                return;
            lastExecutionFrame = Time.frameCount;

            float deltaTime = Time.deltaTime;
            foreach (ICombatAction action in actions)
            {
                if (action is RuntimeCombatAction runtimeAction)
                    runtimeAction.TickCooldown(deltaTime);
            }

            if (combatant == null || !combatant.IsAlive || director == null || !director.BattleStarted || director.BattleFinished)
            {
                CancelAllActions();
                combatant?.ResetBehaviorFireCycle();
                return;
            }

            if (currentAction != null)
            {
                CombatActionExecutionStatus status = currentAction.Tick(deltaTime);
                if (status == CombatActionExecutionStatus.Running)
                    return;
                currentAction = null;
            }

            combatant.TickAutomaticFireCycleFromBehavior();
        }

        public void RequestAutomaticMovementStopAfterCellArrival()
        {
            if (currentAction?.Kind == CombatActionKind.Reposition
                && currentIntent.Source == CombatActionSelectionSource.Automatic)
            {
                currentAction.RequestStop();
            }
        }

        public string GetActionStatusText(CombatActionKind kind)
        {
            RuntimeCombatAction action = FindAction(kind);
            if (action == null)
                return "미장착";
            if (currentAction != null && currentAction != action)
                return "다른 행동 실행 중";
            if (action.RemainingCharges == 0)
                return "수량 없음";
            if (action.CooldownRemaining > 0f)
                return $"재사용 {action.CooldownRemaining:0.0}s";
            if (kind == CombatActionKind.Stim && combatant.IsStimActive)
                return $"효과 {combatant.StimRemainingSeconds:0.0}s";
            return action.RemainingCharges < 0 ? "사용 가능" : $"{action.RemainingCharges}회";
        }

        public CombatActionRuntimeState GetActionRuntimeState(CombatActionKind kind)
        {
            RuntimeCombatAction action = FindAction(kind);
            if (action == null)
            {
                return new CombatActionRuntimeState(
                    kind,
                    null,
                    false,
                    false,
                    false,
                    0,
                    0f,
                    "미장착");
            }

            bool isRunning = currentAction == action;
            bool isInteractable = combatant != null
                && combatant.IsAlive
                && (currentAction == null || isRunning || currentAction.Kind == CombatActionKind.Reposition)
                && action.RemainingCharges != 0
                && action.CooldownRemaining <= 0f;
            return new CombatActionRuntimeState(
                kind,
                action.ActionDefinition,
                true,
                isInteractable,
                isRunning,
                action.RemainingCharges,
                action.CooldownRemaining,
                GetActionStatusText(kind));
        }

        public string BuildUtilityDebugText()
        {
            StringBuilder builder = new();
            builder.Append("ACTION ").Append(CurrentActionKind);
            if (hasLastSelectedCandidate)
            {
                builder.Append("\nCHOSEN ")
                    .Append(lastSelectedCandidate.Kind)
                    .Append(' ')
                    .Append(lastSelectedCandidate.UtilityScore.ToString("0"));
                builder.Append("\n").Append(lastSelectedCandidate.Breakdown);
            }
            int count = Mathf.Min(3, utilitySelector.Candidates.Count);
            for (int index = 0; index < count; index++)
            {
                CombatActionCandidate candidate = utilitySelector.Candidates[index];
                builder.Append("\n#").Append(index + 1).Append(' ')
                    .Append(candidate.Kind).Append(' ')
                    .Append(candidate.UtilityScore.ToString("0"));
            }
            return builder.ToString();
        }

        public bool IsValidGrenadeTarget(GridCoordinate targetCell, out string failureReason)
        {
            GrenadeRuntimeAction action = FindAction(CombatActionKind.Grenade) as GrenadeRuntimeAction;
            return action != null
                ? action.ValidateTargetCell(targetCell, out failureReason)
                : Fail("수류탄 미장착", out failureReason);
        }

        public bool IsValidDashTarget(GridCoordinate targetCell, out string failureReason)
        {
            DashRuntimeAction action = FindAction(CombatActionKind.Dash) as DashRuntimeAction;
            return action != null
                ? action.ValidateTargetCell(targetCell, out failureReason)
                : Fail("돌진 미장착", out failureReason);
        }

        public int GetGrenadeRadiusCells()
        {
            GrenadeRuntimeAction action = FindAction(CombatActionKind.Grenade) as GrenadeRuntimeAction;
            return action != null ? action.DefinitionRadiusCells : 1;
        }

        public bool DoesGrenadeAreaContainFriendly(GridCoordinate targetCell)
        {
            int radius = GetGrenadeRadiusCells();
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

        private void BuildRuntimeActions()
        {
            actions.Clear();
            actions.Add(new BasicAttackRuntimeAction(this));
            actions.Add(new RepositionRuntimeAction(this));
            if (loadout == null)
            {
                actions.Add(new GrenadeRuntimeAction(
                    this,
                    CombatActionDefinition.CreateRuntimeDefault(CombatActionKind.Grenade)));
                actions.Add(new StimRuntimeAction(
                    this,
                    CombatActionDefinition.CreateRuntimeDefault(CombatActionKind.Stim)));
                actions.Add(new DashRuntimeAction(
                    this,
                    CombatActionDefinition.CreateRuntimeDefault(CombatActionKind.Dash)));
                actions.Add(new SwitchWeaponRuntimeAction(
                    this,
                    CombatActionDefinition.CreateRuntimeDefault(CombatActionKind.SwitchWeapon)));
                return;
            }

            foreach (CombatActionDefinition definition in loadout.Definitions)
            {
                if (definition == null)
                    continue;
                RuntimeCombatAction action = definition.Kind switch
                {
                    CombatActionKind.Grenade => new GrenadeRuntimeAction(this, definition),
                    CombatActionKind.Stim => new StimRuntimeAction(this, definition),
                    CombatActionKind.Dash => new DashRuntimeAction(this, definition),
                    CombatActionKind.SwitchWeapon => new SwitchWeaponRuntimeAction(this, definition),
                    _ => null
                };
                if (action != null)
                    actions.Add(action);
            }
        }

        private bool TryStartCandidate(
            CombatActionCandidate candidate,
            CombatActionSelectionSource source,
            out string failureReason)
        {
            RuntimeCombatAction action = FindAction(candidate.Kind);
            if (action == null)
                return Fail("행동 미장착", out failureReason);

            if (currentAction != null)
            {
                if (source == CombatActionSelectionSource.Player
                    && currentAction.Kind == CombatActionKind.Reposition)
                {
                    currentAction.RequestStop();
                    currentAction = null;
                }
                else
                {
                    return Fail("다른 행동 실행 중", out failureReason);
                }
            }

            if (!action.CanStart(candidate, out failureReason))
                return false;

            CombatActionIntent intent = new(candidate, source);
            if (!action.Start(intent))
                return Fail("행동 시작 실패", out failureReason);

            currentAction = action.Kind == CombatActionKind.BasicAttack ? null : action;
            currentIntent = intent;
            lastSelectedCandidate = candidate;
            hasLastSelectedCandidate = true;
            failureReason = string.Empty;
            return true;
        }

        private CombatActionCandidate CreatePlayerCandidate(
            CombatActionKind kind,
            GridCoordinate targetCell)
        {
            UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown().Add("플레이어 명령", 100f);
            return new CombatActionCandidate(kind, null, targetCell, true, breakdown);
        }

        private RuntimeCombatAction FindAction(CombatActionKind kind)
        {
            foreach (ICombatAction action in actions)
            {
                if (action.Kind == kind)
                    return action as RuntimeCombatAction;
            }
            return null;
        }

        private void CancelAllActions()
        {
            currentAction?.RequestStop();
            currentAction = null;
            foreach (ICombatAction action in actions)
                action.RequestStop();
        }

        private void HandleCombatantDied(Combatant deadCombatant)
        {
            CancelAllActions();
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
        }
#endif

        private abstract class RuntimeCombatAction : ICombatAction
        {
            protected readonly CombatActionController Owner;
            protected readonly CombatActionDefinition Definition;

            protected RuntimeCombatAction(
                CombatActionController owner,
                CombatActionDefinition definition = null)
            {
                Owner = owner;
                Definition = definition;
                RemainingCharges = definition?.StartingCharges ?? -1;
            }

            public abstract CombatActionKind Kind { get; }
            public CombatActionDefinition ActionDefinition => Definition;
            public int RemainingCharges { get; protected set; }
            public float CooldownRemaining { get; protected set; }

            public abstract void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results);

            public virtual bool CanStart(
                CombatActionCandidate candidate,
                out string failureReason)
            {
                if (Owner.combatant == null || !Owner.combatant.IsAlive)
                    return Fail("행동 불가 상태", out failureReason);
                if (RemainingCharges == 0)
                    return Fail("수량 없음", out failureReason);
                if (CooldownRemaining > 0f)
                    return Fail("재사용 대기 중", out failureReason);
                failureReason = string.Empty;
                return true;
            }

            public abstract bool Start(CombatActionIntent intent);
            public abstract CombatActionExecutionStatus Tick(float deltaTime);
            public virtual void RequestStop() { }

            public void TickCooldown(float deltaTime)
            {
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            }

            protected void ConsumeChargeAndStartCooldown()
            {
                if (RemainingCharges > 0)
                    RemainingCharges--;
                CooldownRemaining = Definition != null ? Definition.CooldownSeconds : 0f;
            }

            protected bool IsAutomaticUseAllowed(CombatActionContext context)
            {
                if (Definition == null)
                    return true;
                return context.ControlMode == CombatControlMode.FullAutomatic
                    ? Definition.AutomaticInFullAuto
                    : context.ControlMode == CombatControlMode.PlayerMovementAutomaticActions
                        && Definition.AutomaticInSemiAuto;
            }
        }

        private sealed class BasicAttackRuntimeAction : RuntimeCombatAction
        {
            public BasicAttackRuntimeAction(CombatActionController owner) : base(owner) { }
            public override CombatActionKind Kind => CombatActionKind.BasicAttack;

            public override void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results)
            {
                Combatant target = context.PriorityTarget;
                if (target == null || !target.IsAlive || target.Team == context.Actor.Team)
                    target = context.Director.FindClosestShootableEnemy(context.Actor, context.AutomaticPeekAllowed);
                if (target == null)
                    return;

                ShotEvaluation evaluation = context.ShotEvaluator.EvaluateShot(context.Actor, target);
                if (!evaluation.CanShoot)
                    evaluation = context.ShotEvaluator.EvaluateShotFromCell(
                        context.Actor,
                        target,
                        context.Actor.CurrentCell,
                        context.AutomaticPeekAllowed);
                if (!evaluation.CanShoot)
                    return;

                UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                    .Add("기본", 25f)
                    .Add("명중", evaluation.HitChancePercent * 0.6f);
                if (context.Actor.Weapon != null && target.CurrentHealth <= context.Actor.Weapon.Damage)
                    breakdown.Add("처치 가능", 20f);
                if (target == context.PriorityTarget)
                    breakdown.Add("지정 표적", 10f);
                results.Add(new CombatActionCandidate(
                    Kind,
                    target,
                    target.CurrentCell,
                    false,
                    breakdown));
            }

            public override bool Start(CombatActionIntent intent)
            {
                Owner.combatant.SetBehaviorTarget(intent.Candidate.Target);
                return true;
            }

            public override CombatActionExecutionStatus Tick(float deltaTime)
                => CombatActionExecutionStatus.Completed;
        }

        private sealed class RepositionRuntimeAction : RuntimeCombatAction
        {
            private bool stopRequested;

            public RepositionRuntimeAction(CombatActionController owner) : base(owner) { }
            public override CombatActionKind Kind => CombatActionKind.Reposition;

            public override void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results)
            {
                if (context.ControlMode != CombatControlMode.FullAutomatic)
                    return;

                List<TacticalCellChoice> choices = context.PositionEvaluator.EvaluateReachableFiringCells(
                    context.Actor,
                    context.AutomaticPeekAllowed,
                    context.PriorityTarget);
                if (choices.Count == 0)
                    return;

                TacticalCellChoice best = choices[0];
                if (best.Cell == context.Actor.CurrentCell)
                    return;

                float currentScore = float.NegativeInfinity;
                foreach (TacticalCellChoice choice in choices)
                {
                    if (choice.Cell == context.Actor.CurrentCell)
                    {
                        currentScore = choice.Score;
                        break;
                    }
                }
                float improvement = float.IsNegativeInfinity(currentScore)
                    ? Owner.tuning.AiMinimumImprovement + 20f
                    : best.Score - currentScore;
                if (improvement < Owner.tuning.AiMinimumImprovement
                    && !best.IsCoveredFiringPosition)
                {
                    return;
                }

                UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                    .Add("재배치", 35f)
                    .Add("위치 향상", Mathf.Clamp(improvement, 0f, 35f));
                if (best.MovementExposurePenalty > 0f)
                    breakdown.Add("이동 노출", -Mathf.Min(25f, best.MovementExposurePenalty));
                if (best.IsCoveredFiringPosition)
                    breakdown.Add("엄폐", 20f);
                if (float.IsNegativeInfinity(currentScore))
                    breakdown.Add("현재 사격 불가", 20f);
                results.Add(new CombatActionCandidate(
                    Kind,
                    best.Target,
                    best.Cell,
                    true,
                    breakdown));
            }

            public override bool CanStart(
                CombatActionCandidate candidate,
                out string failureReason)
            {
                if (!base.CanStart(candidate, out failureReason))
                    return false;
                if (!candidate.HasTargetCell || !Owner.gridMap.IsWalkable(candidate.TargetCell, Owner.combatant))
                    return Fail("이동 불가 셀", out failureReason);
                return true;
            }

            public override bool Start(CombatActionIntent intent)
            {
                stopRequested = false;
                Owner.combatant.SetBehaviorTarget(intent.Candidate.Target);
                Owner.combatant.SetPeekEnabled(false);
                return Owner.combatant.SetMoveDestination(intent.Candidate.TargetCell);
            }

            public override CombatActionExecutionStatus Tick(float deltaTime)
            {
                if (stopRequested)
                    Owner.combatant.RequestStopMovementAfterCurrentCell();
                return Owner.combatant.IsMoving
                    ? CombatActionExecutionStatus.Running
                    : CombatActionExecutionStatus.Completed;
            }

            public override void RequestStop()
            {
                stopRequested = true;
                Owner.combatant?.RequestStopMovementAfterCurrentCell();
            }
        }

        private sealed class SwitchWeaponRuntimeAction : RuntimeCombatAction
        {
            private float elapsedSeconds;
            private float executionDuration;
            private int targetSlotIndex;
            private bool weaponChanged;

            public SwitchWeaponRuntimeAction(
                CombatActionController owner,
                CombatActionDefinition definition) : base(owner, definition) { }

            public override CombatActionKind Kind => CombatActionKind.SwitchWeapon;

            public override void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results)
            {
                if (!IsAutomaticUseAllowed(context)
                    || CooldownRemaining > 0f
                    || context.Actor.WeaponLoadout == null
                    || !context.Actor.WeaponLoadout.IsBattleInitialized)
                {
                    return;
                }

                WeaponLoadout weaponLoadout = context.Actor.WeaponLoadout;
                int alternateSlotIndex = weaponLoadout.GetNextSlotIndex();
                WeaponDefinition currentWeapon = context.Actor.Weapon;
                WeaponDefinition alternateWeapon = weaponLoadout.GetDefinition(alternateSlotIndex);
                if (currentWeapon == null || alternateWeapon == null || currentWeapon == alternateWeapon)
                    return;

                WeaponAmmoState currentAmmo = weaponLoadout.GetAmmoState(weaponLoadout.ActiveSlotIndex);
                WeaponAmmoState alternateAmmo = weaponLoadout.GetAmmoState(alternateSlotIndex);
                if (alternateAmmo.TotalAmmo <= 0)
                    return;

                UtilityScoreBreakdown breakdown = new();
                if (currentAmmo.TotalAmmo <= 0)
                {
                    breakdown.Add("현재 무기 탄약 고갈", 98f);
                    AddCandidate(results, context, alternateSlotIndex, breakdown);
                    return;
                }

                Combatant target = context.PriorityTarget;
                if (target == null || !target.IsAlive || target.Team == context.Actor.Team)
                    target = context.Actor.CurrentTarget;
                if (target == null || !target.IsAlive || target.Team == context.Actor.Team)
                    return;

                ShotEvaluation currentShot = context.ShotEvaluator.EvaluateShotWithWeapon(
                    context.Actor,
                    target,
                    currentWeapon);
                ShotEvaluation alternateShot = context.ShotEvaluator.EvaluateShotWithWeapon(
                    context.Actor,
                    target,
                    alternateWeapon);

                if (!currentShot.CanShoot && alternateShot.CanShoot)
                {
                    breakdown.Add("대체 무기만 사격 가능", 92f);
                    AddCandidate(results, context, alternateSlotIndex, breakdown, target);
                    return;
                }

                if (currentAmmo.MagazineAmmo <= 0 && alternateAmmo.MagazineAmmo > 0)
                {
                    breakdown.Add("대체 무기 탄창 즉시 사용 가능", 88f);
                    AddCandidate(results, context, alternateSlotIndex, breakdown, target);
                    return;
                }

                if (!currentShot.CanShoot || !alternateShot.CanShoot || alternateAmmo.MagazineAmmo <= 0)
                    return;

                float currentDps = CalculateExpectedDamagePerSecond(currentWeapon, currentShot);
                float alternateDps = CalculateExpectedDamagePerSecond(alternateWeapon, alternateShot);
                if (alternateDps < currentDps * 1.25f)
                    return;

                float improvementRatio = alternateDps / Mathf.Max(0.01f, currentDps);
                float score = Mathf.Clamp(82f + (improvementRatio - 1.25f) * 52f, 82f, 95f);
                breakdown
                    .Add("기대 화력 우세", score)
                    .Add($"현재 DPS {currentDps:0.0}", 0f)
                    .Add($"대체 DPS {alternateDps:0.0}", 0f);
                AddCandidate(results, context, alternateSlotIndex, breakdown, target);
            }

            public override bool CanStart(
                CombatActionCandidate candidate,
                out string failureReason)
            {
                if (!base.CanStart(candidate, out failureReason))
                    return false;
                return Owner.combatant.CanSwitchToWeaponSlot(
                    candidate.TargetWeaponSlotIndex,
                    out failureReason);
            }

            public override bool Start(CombatActionIntent intent)
            {
                targetSlotIndex = intent.Candidate.TargetWeaponSlotIndex;
                elapsedSeconds = 0f;
                executionDuration = Mathf.Max(0.01f, Definition.WindupSeconds);
                weaponChanged = false;
                Owner.combatant.BeginWeaponSwap();
                return true;
            }

            public override CombatActionExecutionStatus Tick(float deltaTime)
            {
                elapsedSeconds += deltaTime;
                if (!weaponChanged && elapsedSeconds >= executionDuration * 0.5f)
                {
                    if (!Owner.combatant.CommitWeaponSwap(targetSlotIndex, out string failureReason))
                    {
                        Debug.LogWarning($"[무기 교체] {Owner.combatant.name}: {failureReason}");
                        return CombatActionExecutionStatus.Failed;
                    }
                    weaponChanged = true;
                }

                if (elapsedSeconds < executionDuration)
                    return CombatActionExecutionStatus.Running;

                ConsumeChargeAndStartCooldown();
                return CombatActionExecutionStatus.Completed;
            }

            private static float CalculateExpectedDamagePerSecond(
                WeaponDefinition weapon,
                ShotEvaluation shot)
            {
                return weapon.Damage
                    * (shot.HitChancePercent / 100f)
                    / Mathf.Max(0.01f, weapon.AimedShotInterval);
            }

            private void AddCandidate(
                List<CombatActionCandidate> results,
                CombatActionContext context,
                int alternateSlotIndex,
                UtilityScoreBreakdown breakdown,
                Combatant target = null)
            {
                WeaponDefinition alternateWeapon = context.Actor.WeaponLoadout.GetDefinition(alternateSlotIndex);
                breakdown.Add($"교체 대상 {alternateWeapon.DisplayName}", 0f);
                results.Add(new CombatActionCandidate(
                    Kind,
                    target,
                    context.Actor.CurrentCell,
                    false,
                    breakdown,
                    alternateSlotIndex));
            }
        }

        private sealed class GrenadeRuntimeAction : RuntimeCombatAction
        {
            private CombatActionIntent intent;
            private float windupRemaining;

            public GrenadeRuntimeAction(
                CombatActionController owner,
                CombatActionDefinition definition) : base(owner, definition) { }

            public override CombatActionKind Kind => CombatActionKind.Grenade;
            public int DefinitionRadiusCells => Definition.GrenadeRadiusCells;

            public override void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results)
            {
                if (!IsAutomaticUseAllowed(context) || RemainingCharges == 0 || CooldownRemaining > 0f)
                    return;

                CombatActionCandidate? best = null;
                for (int x = 0; x < context.GridMap.Width; x++)
                {
                    for (int z = 0; z < context.GridMap.Height; z++)
                    {
                        GridCoordinate cell = new(x, z);
                        if (context.Actor.CurrentCell.ManhattanDistance(cell) > Definition.GrenadeRangeCells)
                            continue;
                        CountGrenadeTargets(context, cell, out int enemies, out int friendlies, out int lethalEnemies);
                        if (friendlies > 0 || (enemies < 2 && lethalEnemies == 0))
                            continue;

                        UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                            .Add("수류탄", 30f)
                            .Add("적 범위", enemies * 30f)
                            .Add("처치 가능", lethalEnemies * 20f);
                        if (RemainingCharges == 1)
                            breakdown.Add("마지막 수량", -5f);
                        CombatActionCandidate candidate = new(
                            Kind,
                            null,
                            cell,
                            true,
                            breakdown);
                        if (!best.HasValue || candidate.UtilityScore > best.Value.UtilityScore)
                            best = candidate;
                    }
                }
                if (best.HasValue)
                    results.Add(best.Value);
            }

            public override bool CanStart(
                CombatActionCandidate candidate,
                out string failureReason)
            {
                if (!base.CanStart(candidate, out failureReason))
                    return false;
                return ValidateTargetCell(candidate.TargetCell, out failureReason);
            }

            public bool ValidateTargetCell(GridCoordinate targetCell, out string failureReason)
            {
                if (!Owner.gridMap.IsInside(targetCell))
                    return Fail("전장 밖", out failureReason);
                if (Owner.combatant.CurrentCell.ManhattanDistance(targetCell) > Definition.GrenadeRangeCells)
                    return Fail("사거리 밖", out failureReason);
                failureReason = string.Empty;
                return true;
            }

            public override bool Start(CombatActionIntent newIntent)
            {
                intent = newIntent;
                Owner.combatant.StopMovementAtCurrentCell();
                Owner.combatant.PrepareForExclusiveCombatAction();
                float animationDuration = Owner.combatant.PlayThrowAnimation();
                windupRemaining = Mathf.Max(Definition.WindupSeconds, animationDuration * 0.55f);
                return true;
            }

            public override CombatActionExecutionStatus Tick(float deltaTime)
            {
                windupRemaining -= deltaTime;
                if (windupRemaining > 0f)
                    return CombatActionExecutionStatus.Running;

                ConsumeChargeAndStartCooldown();
                LaunchProjectile(intent.Candidate.TargetCell);
                return CombatActionExecutionStatus.Completed;
            }

            private void LaunchProjectile(GridCoordinate targetCell)
            {
                GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = "GrenadeProjectile";
                projectile.transform.localScale = Vector3.one * 0.25f;
                Collider projectileCollider = projectile.GetComponent<Collider>();
                if (projectileCollider != null)
                    UnityEngine.Object.Destroy(projectileCollider);
                Renderer projectileRenderer = projectile.GetComponent<Renderer>();
                if (projectileRenderer != null)
                    projectileRenderer.material.color = new Color(0.18f, 0.32f, 0.12f);
                GrenadeProjectile grenade = projectile.AddComponent<GrenadeProjectile>();
                grenade.Initialize(
                    Owner.director,
                    Owner.gridMap,
                    Owner.combatant.MuzzlePosition,
                    Owner.gridMap.GridToWorld(targetCell),
                    targetCell,
                    Definition.GrenadeTravelSeconds,
                    Definition.GrenadeFuseSeconds,
                    Definition.GrenadeRadiusCells,
                    Definition.GrenadeDamage,
                    Definition.GrenadeCameraShakeDuration,
                    Definition.GrenadeCameraShakeAmplitude,
                    Definition.GrenadeCameraShakeFrequency);
            }

            private void CountGrenadeTargets(
                CombatActionContext context,
                GridCoordinate cell,
                out int enemies,
                out int friendlies,
                out int lethalEnemies)
            {
                enemies = 0;
                friendlies = 0;
                lethalEnemies = 0;
                foreach (Combatant target in context.Director.Combatants)
                {
                    if (target == null || !target.IsAlive)
                        continue;
                    int xDistance = Mathf.Abs(target.CurrentCell.X - cell.X);
                    int zDistance = Mathf.Abs(target.CurrentCell.Z - cell.Z);
                    if (Mathf.Max(xDistance, zDistance) > Definition.GrenadeRadiusCells)
                        continue;
                    if (target.Team == context.Actor.Team)
                        friendlies++;
                    else
                    {
                        enemies++;
                        if (target.CurrentHealth <= Definition.GrenadeDamage)
                            lethalEnemies++;
                    }
                }
            }
        }

        private sealed class StimRuntimeAction : RuntimeCombatAction
        {
            private float windupRemaining;

            public StimRuntimeAction(
                CombatActionController owner,
                CombatActionDefinition definition) : base(owner, definition) { }

            public override CombatActionKind Kind => CombatActionKind.Stim;

            public override void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results)
            {
                if (!IsAutomaticUseAllowed(context)
                    || RemainingCharges == 0
                    || CooldownRemaining > 0f
                    || context.Actor.IsStimActive)
                {
                    return;
                }

                int livingEnemies = 0;
                foreach (Combatant enemy in context.Director.GetLivingEnemies(context.Actor.Team))
                    livingEnemies++;
                if (livingEnemies < 2)
                    return;

                bool engaged = context.Actor.CurrentTarget != null
                    && context.Actor.CurrentTarget.IsAlive;
                bool hasAutomaticMovementIntent = false;
                if (!engaged && context.ControlMode == CombatControlMode.FullAutomatic)
                {
                    List<TacticalCellChoice> movementChoices =
                        context.PositionEvaluator.EvaluateReachableFiringCells(
                            context.Actor,
                            context.AutomaticPeekAllowed,
                            context.PriorityTarget);
                    hasAutomaticMovementIntent = movementChoices.Exists(
                        choice => choice.Cell != context.Actor.CurrentCell);
                }
                if (!engaged && !hasAutomaticMovementIntent)
                    return;

                UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                    .Add("자극제", 45f)
                    .Add("생존 적", Mathf.Min(30f, livingEnemies * 10f));
                if (context.Actor.CurrentTarget != null)
                    breakdown.Add("교전 중", 10f);
                if (RemainingCharges == 1)
                    breakdown.Add("마지막 수량", -10f);
                results.Add(new CombatActionCandidate(
                    Kind,
                    context.Actor,
                    context.Actor.CurrentCell,
                    false,
                    breakdown));
            }

            public override bool CanStart(
                CombatActionCandidate candidate,
                out string failureReason)
            {
                if (!base.CanStart(candidate, out failureReason))
                    return false;
                if (Owner.combatant.IsStimActive)
                    return Fail("이미 자극제 효과 적용 중", out failureReason);
                return true;
            }

            public override bool Start(CombatActionIntent intent)
            {
                Owner.combatant.StopMovementAtCurrentCell();
                Owner.combatant.PrepareForExclusiveCombatAction();
                float animationDuration = Owner.combatant.PlayUseItemAnimation();
                windupRemaining = Mathf.Max(Definition.WindupSeconds, animationDuration * 0.5f);
                return true;
            }

            public override CombatActionExecutionStatus Tick(float deltaTime)
            {
                windupRemaining -= deltaTime;
                if (windupRemaining > 0f)
                    return CombatActionExecutionStatus.Running;

                ConsumeChargeAndStartCooldown();
                Owner.combatant.ApplyStim(
                    Definition.StimDurationSeconds,
                    Definition.StimMovementSpeedMultiplier,
                    Definition.StimFireIntervalMultiplier);
                return CombatActionExecutionStatus.Completed;
            }
        }

        private sealed class DashRuntimeAction : RuntimeCombatAction
        {
            private bool speedApplied;

            public DashRuntimeAction(
                CombatActionController owner,
                CombatActionDefinition definition) : base(owner, definition) { }

            public override CombatActionKind Kind => CombatActionKind.Dash;

            public override void CollectAutomaticCandidates(
                CombatActionContext context,
                List<CombatActionCandidate> results)
            {
                if (context.ControlMode != CombatControlMode.FullAutomatic
                    || !IsAutomaticUseAllowed(context)
                    || CooldownRemaining > 0f)
                {
                    return;
                }

                CombatActionCandidate? best = null;
                List<TacticalCellChoice> positionChoices =
                    context.PositionEvaluator.EvaluateReachableFiringCells(
                        context.Actor,
                        context.AutomaticPeekAllowed,
                        context.PriorityTarget);
                TacticalCellChoice? currentChoice = null;
                foreach (TacticalCellChoice choice in positionChoices)
                {
                    if (choice.Cell == context.Actor.CurrentCell)
                    {
                        currentChoice = choice;
                        break;
                    }
                }
                GridCoordinate[] directions =
                {
                    new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
                };
                ShotEvaluation currentShot = context.Actor.CurrentShotEvaluation;
                foreach (GridCoordinate direction in directions)
                {
                    for (int distance = 1; distance <= Definition.DashMaximumCells; distance++)
                    {
                        GridCoordinate cell = new(
                            context.Actor.CurrentCell.X + direction.X * distance,
                            context.Actor.CurrentCell.Z + direction.Z * distance);
                        if (!ValidateStraightPath(cell))
                            break;

                        TacticalCellChoice? destinationChoice = null;
                        foreach (TacticalCellChoice choice in positionChoices)
                        {
                            if (choice.Cell == cell)
                            {
                                destinationChoice = choice;
                                break;
                            }
                        }
                        if (!destinationChoice.HasValue)
                            continue;

                        float improvement = currentChoice.HasValue
                            ? destinationChoice.Value.Score - currentChoice.Value.Score
                            : Definition.DashMinimumPositionImprovement;
                        bool enablesAttack = !currentShot.CanShoot
                            && destinationChoice.Value.Evaluation.CanShoot;
                        if (!enablesAttack && improvement < Definition.DashMinimumPositionImprovement)
                            continue;

                        UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                            .Add("돌진", 30f)
                            .Add("위치 향상", Mathf.Clamp(improvement, 0f, 35f));
                        if (destinationChoice.Value.MovementExposurePenalty > 0f)
                        {
                            breakdown.Add(
                                "이동 노출",
                                -Mathf.Min(20f, destinationChoice.Value.MovementExposurePenalty));
                        }
                        if (enablesAttack)
                            breakdown.Add("사격 확보", 30f);
                        if (context.GridMap.IsCoverPosition(cell))
                            breakdown.Add("엄폐", 10f);
                        CombatActionCandidate candidate = new(
                            Kind,
                            destinationChoice.Value.Target,
                            cell,
                            true,
                            breakdown);
                        if (!best.HasValue || candidate.UtilityScore > best.Value.UtilityScore)
                            best = candidate;
                    }
                }
                if (best.HasValue)
                    results.Add(best.Value);
            }

            public override bool CanStart(
                CombatActionCandidate candidate,
                out string failureReason)
            {
                if (!base.CanStart(candidate, out failureReason))
                    return false;
                return ValidateTargetCell(candidate.TargetCell, out failureReason);
            }

            public bool ValidateTargetCell(GridCoordinate targetCell, out string failureReason)
            {
                if (!Owner.gridMap.IsInside(targetCell))
                    return Fail("전장 밖", out failureReason);
                GridCoordinate origin = Owner.combatant.CurrentCell;
                int xDistance = Mathf.Abs(targetCell.X - origin.X);
                int zDistance = Mathf.Abs(targetCell.Z - origin.Z);
                if ((xDistance != 0 && zDistance != 0)
                    || xDistance + zDistance < 1
                    || xDistance + zDistance > Definition.DashMaximumCells)
                {
                    return Fail("직선 3셀 이내만 가능", out failureReason);
                }
                if (!ValidateStraightPath(targetCell))
                    return Fail("돌진 경로 차단", out failureReason);
                failureReason = string.Empty;
                return true;
            }

            public override bool Start(CombatActionIntent intent)
            {
                Owner.combatant.PrepareForExclusiveCombatAction();
                Owner.combatant.SetBehaviorTarget(intent.Candidate.Target);
                Owner.combatant.SetTemporaryMovementSpeedMultiplier(
                    Definition.DashMovementSpeedMultiplier);
                speedApplied = true;
                if (!Owner.combatant.SetMoveDestination(intent.Candidate.TargetCell))
                {
                    ClearSpeedMultiplier();
                    return false;
                }
                Owner.combatant.PlayDashAnimation();
                ConsumeChargeAndStartCooldown();
                return true;
            }

            public override CombatActionExecutionStatus Tick(float deltaTime)
            {
                if (Owner.combatant.IsMoving)
                    return CombatActionExecutionStatus.Running;
                ClearSpeedMultiplier();
                return CombatActionExecutionStatus.Completed;
            }

            public override void RequestStop()
            {
                ClearSpeedMultiplier();
            }

            private bool ValidateStraightPath(GridCoordinate targetCell)
            {
                GridCoordinate origin = Owner.combatant.CurrentCell;
                int xStep = Math.Sign(targetCell.X - origin.X);
                int zStep = Math.Sign(targetCell.Z - origin.Z);
                int distance = origin.ManhattanDistance(targetCell);
                for (int step = 1; step <= distance; step++)
                {
                    GridCoordinate cell = new(origin.X + xStep * step, origin.Z + zStep * step);
                    if (!Owner.gridMap.IsWalkable(cell, Owner.combatant))
                        return false;
                }
                return true;
            }

            private void ClearSpeedMultiplier()
            {
                if (!speedApplied)
                    return;
                Owner.combatant.SetTemporaryMovementSpeedMultiplier(1f);
                speedApplied = false;
            }
        }
    }
}
