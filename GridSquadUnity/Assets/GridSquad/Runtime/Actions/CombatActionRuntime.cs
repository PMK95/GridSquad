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

        public void Attach(
            ICombatActionCandidateProvider candidateProvider,
            ICombatActionExecutor executor)
        {
            CandidateProvider = candidateProvider;
            Executor = executor;
        }

        public TBehavior GetBehavior<TBehavior>() where TBehavior : CombatActionBehaviorDefinition
            => Definition != null ? Definition.Behavior as TBehavior : null;

        public void TickCooldown(float deltaTime)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
        }

        public bool CanUse(out string failureReason)
        {
            if (RemainingCharges == 0)
                return Fail("수량 없음", out failureReason);
            if (CooldownRemaining > 0f)
                return Fail("재사용 대기 중", out failureReason);
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

        public void ConsumeChargeAndStartCooldown()
        {
            if (RemainingCharges > 0)
                RemainingCharges--;
            Grant.ConsumeOnSuccessfulUse?.Invoke();
            CooldownRemaining = Definition != null ? Definition.CooldownSeconds : 0f;
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
        private float remainingDuration;
        private ShootableTarget target;

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
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            target = intent.Candidate.Target;
            remainingDuration = Mathf.Max(
                Owner.Tuning != null ? Owner.Tuning.EvaluationRefreshInterval : 0.2f,
                intent.ExecutionDurationSeconds);
            Actor.SetBehaviorTarget(target);
            Actor.UpdateAutomaticPeekForCurrentTarget(Owner.AutomaticPeekAllowed);
            Actor.RefreshShotEvaluationForCurrentTarget();
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            Actor.TickAutomaticFireCycleFromBehavior();
            if (target == null || !target.IsAlive || target.TargetTeam == Actor.Team)
                return CombatActionExecutionStatus.Failed;
            remainingDuration -= deltaTime;
            return remainingDuration > 0f
                ? CombatActionExecutionStatus.Running
                : CombatActionExecutionStatus.Completed;
        }

        public override void Interrupt(CombatActionInterruptReason reason)
        {
            if (reason == CombatActionInterruptReason.TargetInvalid
                || reason == CombatActionInterruptReason.CombatEnded
                || reason == CombatActionInterruptReason.OwnerDied)
            {
                Actor.ResetBehaviorFireCycle();
            }
            target = null;
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
            if (context.ControlMode != CombatControlMode.FullAutomatic)
                return;
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
            Actor.SetBehaviorTarget(intent.Candidate.Target);
            Actor.SetPeekEnabled(false);
            if (Actor.SetMoveDestination(intent.Candidate.TargetCell))
                return true;
            return Fail("이동 경로를 시작하지 못했습니다.", out failureReason);
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (stopRequested)
                Actor.RequestStopMovementAfterCurrentCell();
            return Actor.IsMoving
                ? CombatActionExecutionStatus.Running
                : CombatActionExecutionStatus.Completed;
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
                || Runtime.RemainingCharges == 0
                || Runtime.CooldownRemaining > 0f)
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
        private float windupRemaining;

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
            windupRemaining = Mathf.Max(Runtime.Definition.WindupSeconds, animationDuration * 0.55f);
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            windupRemaining -= deltaTime;
            if (windupRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Runtime.ConsumeChargeAndStartCooldown();
            LaunchProjectile(intent.Candidate.TargetCell);
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
                || Runtime.RemainingCharges == 0
                || Runtime.CooldownRemaining > 0f
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
        private float windupRemaining;

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
            windupRemaining = Mathf.Max(Runtime.Definition.WindupSeconds, animationDuration * 0.5f);
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            windupRemaining -= deltaTime;
            if (windupRemaining > 0f)
                return CombatActionExecutionStatus.Running;
            Runtime.ConsumeChargeAndStartCooldown();
            Actor.ApplyStim(
                Runtime.GetBehavior<StimActionBehaviorDefinition>().DurationSeconds,
                Runtime.GetBehavior<StimActionBehaviorDefinition>().MovementSpeedMultiplier,
                Runtime.GetBehavior<StimActionBehaviorDefinition>().FireIntervalMultiplier);
            return CombatActionExecutionStatus.Completed;
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
                || Runtime.CooldownRemaining > 0f)
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
                    if (!DashExecutor.IsStraightPathWalkable(
                            context.Actor,
                            context.GridMap,
                            cell))
                    {
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
        private bool speedApplied;

        public DashExecutor(CombatActionController owner, CombatActionRuntime runtime)
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
            GridCoordinate origin = Actor.CurrentCell;
            int xDistance = Mathf.Abs(targetCell.X - origin.X);
            int zDistance = Mathf.Abs(targetCell.Z - origin.Z);
            if ((xDistance != 0 && zDistance != 0)
                || xDistance + zDistance < 1
                || xDistance + zDistance > Runtime.GetBehavior<DashActionBehaviorDefinition>().MaximumCells)
            {
                return Fail($"직선 {Runtime.GetBehavior<DashActionBehaviorDefinition>().MaximumCells}칸 이내만 가능합니다.", out failureReason);
            }
            if (!IsStraightPathWalkable(Actor, Owner.GridMap, targetCell))
                return Fail("돌진 경로가 차단되었습니다.", out failureReason);
            failureReason = string.Empty;
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            Actor.PrepareForExclusiveCombatAction();
            Actor.SetBehaviorTarget(intent.Candidate.Target);
            Actor.SetTemporaryMovementSpeedMultiplier(Runtime.GetBehavior<DashActionBehaviorDefinition>().MovementSpeedMultiplier);
            speedApplied = true;
            if (!Actor.SetMoveDestination(intent.Candidate.TargetCell))
            {
                ClearSpeedMultiplier();
                return Fail("돌진 이동을 시작하지 못했습니다.", out failureReason);
            }
            Actor.PlayDashAnimation();
            Runtime.ConsumeChargeAndStartCooldown();
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            if (Actor.IsMoving)
                return CombatActionExecutionStatus.Running;
            ClearSpeedMultiplier();
            return CombatActionExecutionStatus.Completed;
        }

        public override void Interrupt(CombatActionInterruptReason reason)
        {
            Actor?.RequestStopMovementAfterCurrentCell();
            ClearSpeedMultiplier();
        }

        internal static bool IsStraightPathWalkable(
            Combatant actor,
            GridMap gridMap,
            GridCoordinate targetCell)
        {
            GridCoordinate origin = actor.CurrentCell;
            int xStep = Math.Sign(targetCell.X - origin.X);
            int zStep = Math.Sign(targetCell.Z - origin.Z);
            int distance = origin.ManhattanDistance(targetCell);
            for (int step = 1; step <= distance; step++)
            {
                GridCoordinate cell = new(origin.X + xStep * step, origin.Z + zStep * step);
                if (!gridMap.IsWalkable(cell, actor.Entity))
                    return false;
            }
            return true;
        }

        private void ClearSpeedMultiplier()
        {
            if (!speedApplied)
                return;
            Actor.SetTemporaryMovementSpeedMultiplier(1f);
            speedApplied = false;
        }
    }
}
