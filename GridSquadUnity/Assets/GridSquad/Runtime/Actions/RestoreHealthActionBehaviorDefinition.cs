using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Actions/Restore Health Behavior", fileName = "RestoreHealthBehavior")]
    public sealed class RestoreHealthActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        [SerializeField, Min(1)] private int restoreAmount = 20;

        public int RestoreAmount => Mathf.Max(1, restoreAmount);
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.Self;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.Exclusive
            | CombatActionCapabilityFlags.PlayerVisible;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new RestoreHealthCandidateProvider(owner, runtime),
                new RestoreHealthExecutor(owner, runtime));

#if UNITY_EDITOR
        public void SetEditorRestoreAmount(int amount)
            => restoreAmount = Mathf.Max(1, amount);
#endif
    }

    internal sealed class RestoreHealthCandidateProvider : CombatActionCandidateProviderBase
    {
        public RestoreHealthCandidateProvider(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime)
        {
        }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (!Runtime.IsAutomaticUseAllowed(context)
                || !Runtime.CanUse(out _)
                || context.Actor.CurrentHealth >= context.Actor.MaximumHealth)
            {
                return;
            }
            float missingRatio = 1f - context.Actor.CurrentHealth / (float)context.Actor.MaximumHealth;
            UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                .Add("회복 필요", missingRatio * 80f)
                .Add("자가 회복", 15f);
            results.Add(new CombatActionCandidate(
                Definition,
                context.Actor.ShootableTarget,
                context.Actor.CurrentCell,
                false,
                breakdown));
        }
    }

    internal sealed class RestoreHealthExecutor : CombatActionExecutorBase
    {
        private float recoveryDuration;

        public RestoreHealthExecutor(CombatActionController owner, CombatActionRuntime runtime)
            : base(owner, runtime)
        {
        }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            if (Actor.CurrentHealth >= Actor.MaximumHealth)
                return Fail("이미 체력이 가득 찼습니다.", out failureReason);
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
                int restored = Actor.RestoreHealth(
                    Runtime.GetBehavior<RestoreHealthActionBehaviorDefinition>().RestoreAmount);
                if (restored <= 0)
                    return CombatActionExecutionStatus.Failed;
                Runtime.CommitEffect(recoveryDuration);
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
}
