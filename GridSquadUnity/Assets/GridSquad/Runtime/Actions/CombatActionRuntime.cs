using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    internal sealed class CombatActionRuntime
    {
        public CombatActionRuntime(CombatActionGrant grant)
        {
            Grant = grant;
            Definition = grant.Definition;
            RemainingCharges = Definition != null ? Definition.StartingCharges : -1;
        }

        public CombatActionGrant Grant { get; }
        public CombatActionDefinition Definition { get; }
        public string RuntimeKey => Grant.RuntimeKey;
        public string SourceDisplayName => Grant.SourceDisplayName;
        public ICombatActionCandidateProvider CandidateProvider { get; private set; }
        public ICombatActionExecutor Executor { get; private set; }
        public CombatActionCapabilityFlags Capabilities => Definition != null
            ? Definition.Capabilities
            : CombatActionCapabilityFlags.None;
        public int RemainingCharges { get; private set; }
        public float CooldownRemaining { get; private set; }
        public CombatActionExecutionPhase Phase { get; private set; }
        public float PhaseDuration { get; private set; }
        public float PhaseRemaining { get; private set; }
        public bool EffectCommitted { get; private set; }

        private bool completeRecoveryAfterInterruption;
        private float committedRecoveryDuration;

        public void Attach(
            ICombatActionCandidateProvider candidateProvider,
            ICombatActionExecutor executor)
        {
            CandidateProvider = candidateProvider;
            Executor = executor;
        }

        public TBehavior GetBehavior<TBehavior>() where TBehavior : CombatActionBehaviorDefinition
            => Definition != null ? Definition.Behavior as TBehavior : null;

        public void TickTimers(float deltaTime)
        {
            float elapsed = Mathf.Max(0f, deltaTime);
            switch (Phase)
            {
                case CombatActionExecutionPhase.Windup:
                case CombatActionExecutionPhase.Recovery:
                    PhaseRemaining = Mathf.Max(0f, PhaseRemaining - elapsed);
                    if (Phase == CombatActionExecutionPhase.Recovery
                        && completeRecoveryAfterInterruption
                        && PhaseRemaining <= 0f)
                    {
                        CompleteActionAndStartCooldown();
                    }
                    break;
                case CombatActionExecutionPhase.Cooldown:
                    CooldownRemaining = Mathf.Max(0f, CooldownRemaining - elapsed);
                    PhaseRemaining = CooldownRemaining;
                    if (CooldownRemaining <= 0f)
                        EnterIdle();
                    break;
            }
        }

        public bool CanUse(out string failureReason)
        {
            if (RemainingCharges == 0)
                return Fail("수량 없음", out failureReason);
            if (Phase == CombatActionExecutionPhase.Recovery)
                return Fail("후딜레이 중", out failureReason);
            if (Phase == CombatActionExecutionPhase.Cooldown || CooldownRemaining > 0f)
                return Fail("재사용 대기 중", out failureReason);
            if (Phase != CombatActionExecutionPhase.Idle)
                return Fail("행동 실행 중", out failureReason);
            failureReason = string.Empty;
            return true;
        }

        public bool IsAutomaticUseAllowed(CombatActionContext context)
        {
            if (Definition == null)
                return true;
            return context.ControlMode == CombatControlMode.FullAutomatic
                ? Definition.AutomaticInFullAuto
                : context.ControlMode == CombatControlMode.PlayerMovementAutomaticActions
                    && Definition.AutomaticInSemiAuto;
        }

        public void BeginWindup(float durationSeconds)
        {
            EffectCommitted = false;
            completeRecoveryAfterInterruption = false;
            committedRecoveryDuration = 0f;
            EnterPhase(CombatActionExecutionPhase.Windup, durationSeconds);
        }

        public void CommitEffect(float recoveryDurationSeconds = 0f)
        {
            if (EffectCommitted)
                return;
            if (RemainingCharges > 0)
                RemainingCharges--;
            Grant.ConsumeOnSuccessfulUse?.Invoke();
            EffectCommitted = true;
            committedRecoveryDuration = Mathf.Max(
                Definition != null ? Definition.RecoverySeconds : 0f,
                recoveryDurationSeconds);
            EnterPhase(CombatActionExecutionPhase.Active, 0f);
        }

        public void BeginRecovery(float durationSeconds)
        {
            if (!EffectCommitted)
                throw new InvalidOperationException("효과 발생 전에는 후딜레이를 시작할 수 없습니다.");
            committedRecoveryDuration = Mathf.Max(
                committedRecoveryDuration,
                durationSeconds);
            EnterPhase(CombatActionExecutionPhase.Recovery, durationSeconds);
        }

        public void CompleteActionAndStartCooldown()
        {
            completeRecoveryAfterInterruption = false;
            EffectCommitted = false;
            committedRecoveryDuration = 0f;
            float duration = Definition != null ? Definition.CooldownSeconds : 0f;
            CooldownRemaining = Mathf.Max(0f, duration);
            if (CooldownRemaining > 0f)
                EnterPhase(CombatActionExecutionPhase.Cooldown, CooldownRemaining);
            else
                EnterIdle();
        }

        public void CancelBeforeEffect()
        {
            if (EffectCommitted)
                return;
            completeRecoveryAfterInterruption = false;
            committedRecoveryDuration = 0f;
            EnterIdle();
        }

        public void PreserveRecoveryAfterInterruption()
        {
            if (!EffectCommitted)
            {
                CancelBeforeEffect();
                return;
            }
            if (Phase != CombatActionExecutionPhase.Recovery)
                BeginRecovery(committedRecoveryDuration);
            completeRecoveryAfterInterruption = true;
            if (PhaseRemaining <= 0f)
                CompleteActionAndStartCooldown();
        }

        public void SynchronizePhase(
            CombatActionExecutionPhase phase,
            float remainingSeconds,
            float durationSeconds)
        {
            Phase = phase;
            PhaseDuration = Mathf.Max(0f, durationSeconds);
            PhaseRemaining = Mathf.Clamp(remainingSeconds, 0f, PhaseDuration);
        }

        private void EnterPhase(CombatActionExecutionPhase phase, float durationSeconds)
        {
            Phase = phase;
            PhaseDuration = Mathf.Max(0f, durationSeconds);
            PhaseRemaining = PhaseDuration;
        }

        private void EnterIdle()
        {
            Phase = CombatActionExecutionPhase.Idle;
            PhaseDuration = 0f;
            PhaseRemaining = 0f;
            CooldownRemaining = 0f;
        }

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }

    internal abstract class CombatActionCandidateProviderBase : ICombatActionCandidateProvider
    {
        protected CombatActionCandidateProviderBase(
            CombatActionController owner,
            CombatActionRuntime runtime)
        {
            Owner = owner;
            Runtime = runtime;
        }

        protected CombatActionController Owner { get; }
        protected CombatActionRuntime Runtime { get; }
        public CombatActionDefinition Definition => Runtime.Definition;
        public abstract void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results);
    }

    internal abstract class CombatActionExecutorBase : ICombatActionExecutor
    {
        protected CombatActionExecutorBase(
            CombatActionController owner,
            CombatActionRuntime runtime)
        {
            Owner = owner;
            Runtime = runtime;
        }

        protected CombatActionController Owner { get; }
        protected CombatActionRuntime Runtime { get; }
        protected Combatant Actor => Owner.Actor;
        public CombatActionDefinition Definition => Runtime.Definition;

        public virtual bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (Actor == null || !Actor.IsAlive)
                return Fail("행동 불가 상태", out failureReason);
            return Runtime.CanUse(out failureReason);
        }

        public abstract bool Begin(CombatActionIntent intent, out string failureReason);
        public abstract CombatActionExecutionStatus Tick(float deltaTime);
        public virtual void Interrupt(CombatActionInterruptReason reason) { }

        protected static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }

    internal sealed class BasicAttackCandidateProvider : CombatActionCandidateProviderBase
    {
        public BasicAttackCandidateProvider(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (!Runtime.CanUse(out _))
                return;

            ShootableTarget target = context.PriorityTarget;
            if (target == null || !target.IsAlive || target.TargetTeam == context.Actor.Team)
            {
                target = context.Director.FindClosestShootableEnemy(
                    context.Actor,
                    context.AutomaticPeekAllowed)?.ShootableTarget;
            }
            if (target == null)
                return;

            ShotEvaluation evaluation = context.ShotEvaluator.EvaluateShot(context.Actor, target);
            if (!evaluation.CanShoot)
            {
                evaluation = context.ShotEvaluator.EvaluateShotFromCell(
                    context.Actor,
                    target,
                    context.Actor.CurrentCell,
                    context.AutomaticPeekAllowed);
            }
            if (!evaluation.CanShoot)
                return;

            UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                .Add("기본", 25f)
                .Add("명중", evaluation.HitChancePercent * 0.6f)
                .Add("아군 오사 위험", -evaluation.FriendlyFireRiskPercent * 0.6f);
            if (context.Actor.Weapon != null
                && target.CurrentHealth <= context.Actor.EffectiveWeaponDamage)
            {
                breakdown.Add("처치 가능", 20f);
            }
            if (target == context.PriorityTarget)
                breakdown.Add("지정 표적", 10f);
            results.Add(new CombatActionCandidate(
                Definition,
                target,
                target.CurrentCell,
                false,
                breakdown));
        }
    }

    internal sealed class BasicAttackExecutor : CombatActionExecutorBase
    {
        private ShootableTarget target;
        private float recoveryDuration;

        public BasicAttackExecutor(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            if (candidate.Target == null
                || !candidate.Target.IsAlive
                || candidate.Target.TargetTeam == Actor.Team)
            {
                return Fail("유효한 사격 대상이 없습니다.", out failureReason);
            }
            ShotEvaluation evaluation = Owner.ShotEvaluator.EvaluateShot(
                Actor,
                candidate.Target);
            if (!evaluation.CanShoot)
            {
                evaluation = Owner.ShotEvaluator.EvaluateShotFromCell(
                    Actor,
                    candidate.Target,
                    Actor.CurrentCell,
                    Owner.AutomaticPeekAllowed);
            }
            if (!evaluation.CanShoot)
            {
                return Fail(
                    $"현재 사격 불가 · {GetShotFailureLabel(evaluation.FailureReason)}",
                    out failureReason);
            }
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            target = intent.Candidate.Target;
            Actor.SetBehaviorTarget(target);
            Actor.UpdateAutomaticPeekForCurrentTarget(Owner.AutomaticPeekAllowed);
            Actor.RefreshShotEvaluationForCurrentTarget();
            if (!Actor.TryBeginPreparedShot(
                    target,
                    out float windupDuration,
                    out failureReason))
            {
                return false;
            }
            Runtime.BeginWindup(windupDuration);
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (target == null || !target.IsAlive || target.TargetTeam == Actor.Team)
                return CombatActionExecutionStatus.Failed;

            if (!Runtime.EffectCommitted)
            {
                PreparedShotStatus shotStatus = Actor.TickPreparedShot(
                    deltaTime,
                    out _);
                Runtime.SynchronizePhase(
                    CombatActionExecutionPhase.Windup,
                    Actor.FireStateRemainingSeconds,
                    Runtime.PhaseDuration);
                if (shotStatus == PreparedShotStatus.Waiting)
                    return CombatActionExecutionStatus.Running;
                if (shotStatus == PreparedShotStatus.Failed)
                    return CombatActionExecutionStatus.Failed;

                recoveryDuration = Actor.GetPreparedShotRecoveryDuration();
                Runtime.CommitEffect(recoveryDuration);
                return CombatActionExecutionStatus.Running;
            }
            if (Runtime.Phase == CombatActionExecutionPhase.Active)
                Runtime.BeginRecovery(recoveryDuration);
            if (Actor.IsReloading)
                Actor.TickAutomaticFireCycleFromBehavior();
            if (Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Actor.CompletePreparedShotRecovery();
            Runtime.CompleteActionAndStartCooldown();
            target = null;
            return CombatActionExecutionStatus.Completed;
        }

        public override void Interrupt(CombatActionInterruptReason reason)
        {
            if (reason == CombatActionInterruptReason.TargetInvalid
                || reason == CombatActionInterruptReason.CombatEnded
                || reason == CombatActionInterruptReason.OwnerDied
                || reason == CombatActionInterruptReason.Stunned
                || reason == CombatActionInterruptReason.HitReaction)
            {
                Actor.ResetBehaviorFireCycle();
            }
            target = null;
        }

        private static string GetShotFailureLabel(ShotFailureReason reason)
        {
            return reason switch
            {
                ShotFailureReason.NoTarget => "대상 없음",
                ShotFailureReason.TargetDead => "대상 전투 불능",
                ShotFailureReason.OutOfRange => "사거리 밖",
                ShotFailureReason.FullyBlocked => "완전 엄폐",
                ShotFailureReason.NoPeekPosition => "사격 위치 없음",
                _ => "사격 조건 미충족"
            };
        }
    }

    internal sealed class RepositionCandidateProvider : CombatActionCandidateProviderBase
    {
        public RepositionCandidateProvider(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (context.ControlMode != CombatControlMode.FullAutomatic
                || !Runtime.CanUse(out _))
            {
                return;
            }
            List<TacticalCellChoice> choices = context.PositionEvaluator.EvaluateReachableFiringCells(
                context.Actor,
                context.AutomaticPeekAllowed,
                context.PriorityTarget);
            if (choices.Count == 0 || choices[0].Cell == context.Actor.CurrentCell)
                return;

            TacticalCellChoice best = choices[0];
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
                ? Owner.Tuning.AiMinimumImprovement + 20f
                : best.Score - currentScore;
            if (improvement < Owner.Tuning.AiMinimumImprovement
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
                Definition,
                best.Target,
                best.Cell,
                true,
                breakdown));
        }
    }

    internal sealed class RepositionExecutor : CombatActionExecutorBase
    {
        private bool stopRequested;
        private bool recoveryStarted;

        public RepositionExecutor(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            if (!candidate.HasTargetCell
                || !Owner.GridMap.IsWalkable(candidate.TargetCell, Actor.Entity))
            {
                return Fail("이동할 수 없는 셀입니다.", out failureReason);
            }
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            stopRequested = false;
            recoveryStarted = false;
            Actor.SetBehaviorTarget(intent.Candidate.Target);
            Actor.SetPeekEnabled(false);
            if (Actor.SetMoveDestination(intent.Candidate.TargetCell))
            {
                Runtime.BeginWindup(0f);
                Runtime.CommitEffect();
                return true;
            }
            return Fail("이동 경로를 시작하지 못했습니다.", out failureReason);
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (stopRequested)
                Actor.RequestStopMovementAfterCurrentCell();
            if (Actor.IsMoving)
                return CombatActionExecutionStatus.Running;
            if (!recoveryStarted)
            {
                recoveryStarted = true;
                Runtime.BeginRecovery(Runtime.Definition.RecoverySeconds);
            }
            if (Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Runtime.CompleteActionAndStartCooldown();
            return CombatActionExecutionStatus.Completed;
        }

        public override void Interrupt(CombatActionInterruptReason reason)
        {
            stopRequested = true;
            Actor?.RequestStopMovementAfterCurrentCell();
        }
    }

    internal sealed class GrenadeCandidateProvider : CombatActionCandidateProviderBase
    {
        public GrenadeCandidateProvider(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (!Runtime.IsAutomaticUseAllowed(context)
                || !Runtime.CanUse(out _))
            {
                return;
            }

            CombatActionCandidate? best = null;
            for (int x = 0; x < context.GridMap.Width; x++)
            {
                for (int z = 0; z < context.GridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (context.Actor.CurrentCell.ManhattanDistance(cell)
                        > Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().RangeCells)
                    {
                        continue;
                    }
                    CountTargets(context, cell, out int enemies, out int friendlies, out int lethalEnemies);
                    if (friendlies > 0 || enemies < 2 && lethalEnemies == 0)
                        continue;
                    UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                        .Add("수류탄", 30f)
                        .Add("적 범위", enemies * 30f)
                        .Add("처치 가능", lethalEnemies * 20f);
                    if (Runtime.RemainingCharges == 1)
                        breakdown.Add("마지막 수량", -5f);
                    CombatActionCandidate candidate = new(Definition, null, cell, true, breakdown);
                    if (!best.HasValue || candidate.UtilityScore > best.Value.UtilityScore)
                        best = candidate;
                }
            }
            if (best.HasValue)
                results.Add(best.Value);
        }

        private void CountTargets(
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
                if (Mathf.Max(xDistance, zDistance) > Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().RadiusCells)
                    continue;
                if (target.Team == context.Actor.Team)
                    friendlies++;
                else
                {
                    enemies++;
                    if (target.CurrentHealth <= Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().Damage)
                        lethalEnemies++;
                }
            }
        }
    }

    internal sealed class GrenadeExecutor : CombatActionExecutorBase
    {
        private CombatActionIntent intent;
        private float recoveryDuration;

        public GrenadeExecutor(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            return ValidateTargetCell(candidate.TargetCell, out failureReason);
        }

        public bool ValidateTargetCell(GridCoordinate targetCell, out string failureReason)
        {
            if (!Owner.GridMap.IsInside(targetCell))
                return Fail("전장 밖입니다.", out failureReason);
            GrenadeActionBehaviorDefinition behavior = Runtime.GetBehavior<GrenadeActionBehaviorDefinition>();
            if (behavior == null)
                return Fail("수류탄 행동 설정이 없습니다.", out failureReason);
            if (Actor.CurrentCell.ManhattanDistance(targetCell) > behavior.RangeCells)
                return Fail("사거리 밖입니다.", out failureReason);
            failureReason = string.Empty;
            return true;
        }

        public override bool Begin(CombatActionIntent newIntent, out string failureReason)
        {
            if (!CanBegin(newIntent.Candidate, out failureReason))
                return false;
            intent = newIntent;
            Actor.StopMovementAtCurrentCell();
            Actor.PrepareForExclusiveCombatAction();
            float animationDuration = Actor.PlayThrowAnimation();
            float windupDuration = Mathf.Max(
                Runtime.Definition.WindupSeconds,
                animationDuration * 0.55f);
            recoveryDuration = Mathf.Max(
                Runtime.Definition.RecoverySeconds,
                animationDuration - windupDuration);
            Runtime.BeginWindup(windupDuration);
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (Runtime.Phase == CombatActionExecutionPhase.Windup
                && Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            if (!Runtime.EffectCommitted)
            {
                Runtime.CommitEffect(recoveryDuration);
                LaunchProjectile(intent.Candidate.TargetCell);
                return CombatActionExecutionStatus.Running;
            }
            if (Runtime.Phase == CombatActionExecutionPhase.Active)
                Runtime.BeginRecovery(recoveryDuration);
            if (Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Runtime.CompleteActionAndStartCooldown();
            return CombatActionExecutionStatus.Completed;
        }

        private void LaunchProjectile(GridCoordinate targetCell)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "GrenadeProjectile";
            projectile.transform.localScale = Vector3.one * 0.25f;
            Collider collider = projectile.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.18f, 0.32f, 0.12f);
            GrenadeProjectile grenade = projectile.AddComponent<GrenadeProjectile>();
            grenade.Initialize(
                Owner.Director,
                Owner.GridMap,
                Actor.MuzzlePosition,
                Owner.GridMap.GridToWorld(targetCell),
                targetCell,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().TravelSeconds,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().FuseSeconds,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().RadiusCells,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().Damage,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().CameraShakeDuration,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().CameraShakeAmplitude,
                Runtime.GetBehavior<GrenadeActionBehaviorDefinition>().CameraShakeFrequency);
        }
    }

    internal sealed class StimCandidateProvider : CombatActionCandidateProviderBase
    {
        public StimCandidateProvider(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (!Runtime.IsAutomaticUseAllowed(context)
                || !Runtime.CanUse(out _)
                || context.Actor.IsStimActive)
            {
                return;
            }
            int livingEnemies = 0;
            foreach (Combatant ignored in context.Director.GetLivingEnemies(context.Actor.Team))
                livingEnemies++;
            if (livingEnemies < 2)
                return;

            bool engaged = context.Actor.CurrentTarget != null && context.Actor.CurrentTarget.IsAlive;
            bool hasMovementIntent = false;
            if (!engaged && context.ControlMode == CombatControlMode.FullAutomatic)
            {
                List<TacticalCellChoice> choices = context.PositionEvaluator.EvaluateReachableFiringCells(
                    context.Actor,
                    context.AutomaticPeekAllowed,
                    context.PriorityTarget);
                hasMovementIntent = choices.Exists(choice => choice.Cell != context.Actor.CurrentCell);
            }
            if (!engaged && !hasMovementIntent)
                return;

            UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                .Add("자극제", 45f)
                .Add("생존 적", Mathf.Min(30f, livingEnemies * 10f));
            if (engaged)
                breakdown.Add("교전 중", 10f);
            if (Runtime.RemainingCharges == 1)
                breakdown.Add("마지막 수량", -10f);
            results.Add(new CombatActionCandidate(
                Definition,
                context.Actor.ShootableTarget,
                context.Actor.CurrentCell,
                false,
                breakdown));
        }
    }

    internal sealed class StimExecutor : CombatActionExecutorBase
    {
        private float recoveryDuration;

        public StimExecutor(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            if (Actor.IsStimActive)
                return Fail("이미 자극제 효과가 적용 중입니다.", out failureReason);
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            Actor.StopMovementAtCurrentCell();
            Actor.PrepareForExclusiveCombatAction();
            float animationDuration = Actor.PlayUseItemAnimation();
            float windupDuration = Mathf.Max(
                Runtime.Definition.WindupSeconds,
                animationDuration * 0.5f);
            recoveryDuration = Mathf.Max(
                Runtime.Definition.RecoverySeconds,
                animationDuration - windupDuration);
            Runtime.BeginWindup(windupDuration);
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (Runtime.Phase == CombatActionExecutionPhase.Windup
                && Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            if (!Runtime.EffectCommitted)
            {
                Runtime.CommitEffect(recoveryDuration);
                Actor.ApplyStim(
                    $"action:{Runtime.RuntimeKey}:stim",
                    Runtime.Definition.DisplayName,
                    Runtime.Definition.Icon,
                    Runtime.GetBehavior<StimActionBehaviorDefinition>().DurationSeconds,
                    Runtime.GetBehavior<StimActionBehaviorDefinition>().MovementSpeedMultiplier,
                    Runtime.GetBehavior<StimActionBehaviorDefinition>().FireIntervalMultiplier);
                return CombatActionExecutionStatus.Running;
            }
            if (Runtime.Phase == CombatActionExecutionPhase.Active)
                Runtime.BeginRecovery(recoveryDuration);
            if (Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Runtime.CompleteActionAndStartCooldown();
            return CombatActionExecutionStatus.Completed;
        }
    }

    internal readonly struct DashPathPlan
    {
        public readonly GridCoordinate ApproachCell;
        public readonly GridCoordinate CollisionCell;
        public readonly Combatant CollisionTarget;

        public bool HasCollision => CollisionTarget != null;

        public DashPathPlan(
            GridCoordinate approachCell,
            GridCoordinate collisionCell,
            Combatant collisionTarget)
        {
            ApproachCell = approachCell;
            CollisionCell = collisionCell;
            CollisionTarget = collisionTarget;
        }
    }

    internal sealed class DashCandidateProvider : CombatActionCandidateProviderBase
    {
        public DashCandidateProvider(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (context.ControlMode != CombatControlMode.FullAutomatic
                || !Runtime.IsAutomaticUseAllowed(context)
                || !Runtime.CanUse(out _))
            {
                return;
            }

            List<TacticalCellChoice> choices = context.PositionEvaluator.EvaluateReachableFiringCells(
                context.Actor,
                context.AutomaticPeekAllowed,
                context.PriorityTarget);
            TacticalCellChoice? currentChoice = FindChoice(choices, context.Actor.CurrentCell);
            ShotEvaluation currentShot = context.Actor.CurrentShotEvaluation;
            CombatActionCandidate? best = null;
            GridCoordinate[] directions =
            {
                new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
            };
            foreach (GridCoordinate direction in directions)
            {
                for (int distance = 1; distance <= Runtime.GetBehavior<DashActionBehaviorDefinition>().MaximumCells; distance++)
                {
                    GridCoordinate cell = new(
                        context.Actor.CurrentCell.X + direction.X * distance,
                        context.Actor.CurrentCell.Z + direction.Z * distance);
                    if (!DashExecutor.TryBuildPathPlan(
                            context.Actor,
                            context.GridMap,
                            cell,
                            Runtime.GetBehavior<DashActionBehaviorDefinition>().MaximumCells,
                            out DashPathPlan pathPlan,
                            out _))
                    {
                        break;
                    }
                    if (pathPlan.HasCollision)
                    {
                        if (pathPlan.CollisionTarget.TryCalculateKnockbackDestination(
                                pathPlan.ApproachCell,
                                Runtime.GetBehavior<DashActionBehaviorDefinition>().KnockbackCells,
                                out _))
                        {
                            UtilityScoreBreakdown collisionBreakdown = new UtilityScoreBreakdown()
                                .Add("돌진", 30f)
                                .Add("적 밀치기", 28f);
                            CombatActionCandidate collisionCandidate = new(
                                Definition,
                                pathPlan.CollisionTarget.ShootableTarget,
                                pathPlan.CollisionCell,
                                true,
                                collisionBreakdown);
                            if (!best.HasValue
                                || collisionCandidate.UtilityScore > best.Value.UtilityScore)
                            {
                                best = collisionCandidate;
                            }
                        }
                        break;
                    }
                    TacticalCellChoice? destination = FindChoice(choices, cell);
                    if (!destination.HasValue)
                        continue;
                    float improvement = currentChoice.HasValue
                        ? destination.Value.Score - currentChoice.Value.Score
                        : Runtime.GetBehavior<DashActionBehaviorDefinition>().MinimumPositionImprovement;
                    bool enablesAttack = !currentShot.CanShoot && destination.Value.Evaluation.CanShoot;
                    if (!enablesAttack && improvement < Runtime.GetBehavior<DashActionBehaviorDefinition>().MinimumPositionImprovement)
                        continue;
                    UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                        .Add("돌진", 30f)
                        .Add("위치 향상", Mathf.Clamp(improvement, 0f, 35f));
                    if (destination.Value.MovementExposurePenalty > 0f)
                        breakdown.Add("이동 노출", -Mathf.Min(20f, destination.Value.MovementExposurePenalty));
                    if (enablesAttack)
                        breakdown.Add("사격 확보", 30f);
                    if (context.GridMap.IsCoverPosition(cell))
                        breakdown.Add("엄폐", 10f);
                    CombatActionCandidate candidate = new(
                        Definition,
                        destination.Value.Target,
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

        private static TacticalCellChoice? FindChoice(
            List<TacticalCellChoice> choices,
            GridCoordinate cell)
        {
            foreach (TacticalCellChoice choice in choices)
            {
                if (choice.Cell == cell)
                    return choice;
            }
            return null;
        }
    }

    internal sealed class DashExecutor : CombatActionExecutorBase
    {
        private UnitStatModifierHandle speedModifierHandle;
        private DashPathPlan pathPlan;
        private bool collisionHandled;
        private bool recoveryStarted;

        public DashExecutor(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime) { }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            return ValidateTargetCell(candidate.TargetCell, out failureReason);
        }

        public bool ValidateTargetCell(GridCoordinate targetCell, out string failureReason)
            => TryBuildPathPlan(targetCell, out _, out failureReason);

        public bool TryBuildPathPlan(
            GridCoordinate targetCell,
            out DashPathPlan plan,
            out string failureReason)
            => TryBuildPathPlan(
                Actor,
                Owner.GridMap,
                targetCell,
                Runtime.GetBehavior<DashActionBehaviorDefinition>().MaximumCells,
                out plan,
                out failureReason);

        internal static bool TryBuildPathPlan(
            Combatant actor,
            GridMap gridMap,
            GridCoordinate targetCell,
            int maximumCells,
            out DashPathPlan plan,
            out string failureReason)
        {
            plan = default;
            if (actor == null || gridMap == null || !gridMap.IsInside(targetCell))
                return Fail("전장 밖입니다.", out failureReason);
            GridCoordinate origin = actor.CurrentCell;
            int xDistance = Mathf.Abs(targetCell.X - origin.X);
            int zDistance = Mathf.Abs(targetCell.Z - origin.Z);
            if ((xDistance != 0 && zDistance != 0)
                || xDistance + zDistance < 1
                || xDistance + zDistance > maximumCells)
            {
                return Fail($"직선 {maximumCells}칸 이내만 가능합니다.", out failureReason);
            }

            int xStep = Math.Sign(targetCell.X - origin.X);
            int zStep = Math.Sign(targetCell.Z - origin.Z);
            int distance = origin.ManhattanDistance(targetCell);
            GridCoordinate approachCell = origin;
            for (int step = 1; step <= distance; step++)
            {
                GridCoordinate cell = new(
                    origin.X + xStep * step,
                    origin.Z + zStep * step);
                if (gridMap.TryGetOccupant(cell, out Combatant occupant)
                    && occupant != null
                    && occupant != actor)
                {
                    if (!occupant.IsAlive || occupant.Team == actor.Team)
                        return Fail("아군 또는 전투 불능 유닛이 경로를 막고 있습니다.", out failureReason);
                    plan = new DashPathPlan(approachCell, cell, occupant);
                    failureReason = string.Empty;
                    return true;
                }
                if (!gridMap.IsWalkable(cell, actor.Entity))
                    return Fail("돌진 경로가 차단되었습니다.", out failureReason);
                approachCell = cell;
            }

            plan = new DashPathPlan(targetCell, default, null);
            failureReason = string.Empty;
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            if (!TryBuildPathPlan(intent.Candidate.TargetCell, out pathPlan, out failureReason))
                return false;
            Actor.PrepareForExclusiveCombatAction();
            Actor.SetBehaviorTarget(pathPlan.HasCollision
                ? pathPlan.CollisionTarget.ShootableTarget
                : intent.Candidate.Target);
            ApplyActionRunningSpeedModifier();
            collisionHandled = false;
            recoveryStarted = false;
            if (!Actor.SetMoveDestination(pathPlan.ApproachCell))
            {
                RemoveActionRunningSpeedModifier();
                return Fail("돌진 이동을 시작하지 못했습니다.", out failureReason);
            }
            Actor.PlayDashAnimation();
            Runtime.BeginWindup(0f);
            Runtime.CommitEffect();
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (Actor.IsMoving)
                return CombatActionExecutionStatus.Running;
            if (pathPlan.HasCollision && !collisionHandled)
            {
                collisionHandled = true;
                Combatant target = pathPlan.CollisionTarget;
                bool targetStillAtCollisionCell = target != null
                    && target.IsAlive
                    && target.CurrentCell == pathPlan.CollisionCell
                    && target.Team != Actor.Team;
                bool collisionCellVacated = !targetStillAtCollisionCell;
                if (targetStillAtCollisionCell)
                {
                    collisionCellVacated = target.TryApplyKnockback(
                        Actor.CurrentCell,
                        Runtime.GetBehavior<DashActionBehaviorDefinition>().KnockbackCells);
                }
                if (collisionCellVacated
                    && Owner.GridMap.IsWalkable(pathPlan.CollisionCell, Actor.Entity)
                    && Actor.SetMoveDestination(pathPlan.CollisionCell))
                {
                    return CombatActionExecutionStatus.Running;
                }
            }
            RemoveActionRunningSpeedModifier();
            if (!recoveryStarted)
            {
                recoveryStarted = true;
                Runtime.BeginRecovery(Runtime.Definition.RecoverySeconds);
            }
            if (Runtime.PhaseRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Runtime.CompleteActionAndStartCooldown();
            return CombatActionExecutionStatus.Completed;
        }

        public override void Interrupt(CombatActionInterruptReason reason)
        {
            Actor?.RequestStopMovementAfterCurrentCell();
            pathPlan = default;
            collisionHandled = false;
            RemoveActionRunningSpeedModifier();
        }

        private void ApplyActionRunningSpeedModifier()
        {
            UnitStatDefinition movementSpeed = Actor.StatCatalog?.MovementSpeedMultiplier;
            DashActionBehaviorDefinition behavior =
                Runtime.GetBehavior<DashActionBehaviorDefinition>();
            if (movementSpeed == null || behavior == null)
                return;
            speedModifierHandle = Actor.AddPersistentStatModifiers(
                $"action:{Runtime.RuntimeKey}:running",
                Runtime.Definition.DisplayName,
                Runtime.Definition.Icon,
                UnitStatModifierSourceKind.Action,
                new[]
                {
                    new UnitStatModifier(
                        movementSpeed,
                        UnitStatModifierOperation.Multiply,
                        behavior.MovementSpeedMultiplier)
                });
        }

        private void RemoveActionRunningSpeedModifier()
        {
            if (!speedModifierHandle.IsValid)
                return;
            Actor.RemoveStatModifierHandle(speedModifierHandle);
            speedModifierHandle = default;
        }
    }
}
