using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum UnitStatModifierLifetime
    {
        Timed,
        Persistent
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Actions/Stat Modifier Behavior",
        fileName = "StatModifierBehavior")]
    public sealed class StatModifierActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        [SerializeField] private UnitStatModifier[] modifiers = Array.Empty<UnitStatModifier>();
        [SerializeField] private UnitStatModifierLifetime lifetime;
        [SerializeField, Min(0.1f)] private float durationSeconds = 5f;
        [SerializeField] private TacticalEquipmentActionAnimation animationKind;

        public IReadOnlyList<UnitStatModifier> Modifiers => modifiers;
        public UnitStatModifierLifetime Lifetime => lifetime;
        public float DurationSeconds => Mathf.Max(0.1f, durationSeconds);
        public TacticalEquipmentActionAnimation AnimationKind => animationKind;
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.Self;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.Exclusive
            | CombatActionCapabilityFlags.PlayerVisible;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
        {
            return new CombatActionRuntimeParts(
                new StatModifierActionCandidateProvider(owner, runtime),
                new StatModifierActionExecutor(owner, runtime));
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            UnitStatModifier[] newModifiers,
            UnitStatModifierLifetime newLifetime,
            float newDurationSeconds,
            TacticalEquipmentActionAnimation newAnimationKind)
        {
            modifiers = newModifiers ?? Array.Empty<UnitStatModifier>();
            lifetime = newLifetime;
            durationSeconds = Mathf.Max(0.1f, newDurationSeconds);
            animationKind = newAnimationKind;
        }
#endif
    }

    internal sealed class StatModifierActionCandidateProvider : CombatActionCandidateProviderBase
    {
        public StatModifierActionCandidateProvider(
            CombatActionController owner,
            CombatActionRuntime runtime)
            : base(owner, runtime)
        {
        }

        public override void CollectCandidates(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            if (!Runtime.IsAutomaticUseAllowed(context) || !Runtime.CanUse(out _))
                return;
            StatModifierActionBehaviorDefinition behavior =
                Runtime.GetBehavior<StatModifierActionBehaviorDefinition>();
            if (behavior == null || behavior.Lifetime == UnitStatModifierLifetime.Persistent)
                return;
            results.Add(new CombatActionCandidate(
                Definition,
                context.Actor.ShootableTarget,
                context.Actor.CurrentCell,
                false,
                new UtilityScoreBreakdown().Add("능력치 강화", 50f)));
        }
    }

    internal sealed class StatModifierActionExecutor : CombatActionExecutorBase
    {
        private float windupRemaining;

        public StatModifierActionExecutor(
            CombatActionController owner,
            CombatActionRuntime runtime)
            : base(owner, runtime)
        {
        }

        public override bool CanBegin(
            CombatActionCandidate candidate,
            out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            StatModifierActionBehaviorDefinition behavior =
                Runtime.GetBehavior<StatModifierActionBehaviorDefinition>();
            if (behavior == null || behavior.Modifiers.Count == 0)
                return Fail("적용할 능력치 변경이 없습니다.", out failureReason);
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            Actor.StopMovementAtCurrentCell();
            Actor.PrepareForExclusiveCombatAction();
            float animationDuration = PlayConfiguredAnimation(
                Runtime.GetBehavior<StatModifierActionBehaviorDefinition>().AnimationKind);
            windupRemaining = Mathf.Max(
                Runtime.Definition.WindupSeconds,
                animationDuration * 0.45f);
            return true;
        }

        public override CombatActionExecutionStatus Tick(float deltaTime)
        {
            windupRemaining -= deltaTime;
            if (windupRemaining > 0f)
                return CombatActionExecutionStatus.Running;

            StatModifierActionBehaviorDefinition behavior =
                Runtime.GetBehavior<StatModifierActionBehaviorDefinition>();
            if (behavior == null)
                return CombatActionExecutionStatus.Failed;

            Runtime.ConsumeChargeAndStartCooldown();
            string sourceKey = $"action:{Runtime.RuntimeKey}:stat";
            if (behavior.Lifetime == UnitStatModifierLifetime.Timed)
            {
                Actor.AddTimedStatModifiers(
                    sourceKey,
                    Runtime.Definition.DisplayName,
                    Runtime.Definition.Icon,
                    UnitStatModifierSourceKind.Action,
                    behavior.Modifiers,
                    behavior.DurationSeconds);
            }
            else
            {
                Actor.AddPersistentStatModifiers(
                    sourceKey,
                    Runtime.Definition.DisplayName,
                    Runtime.Definition.Icon,
                    UnitStatModifierSourceKind.Action,
                    behavior.Modifiers);
            }
            return CombatActionExecutionStatus.Completed;
        }

        private float PlayConfiguredAnimation(TacticalEquipmentActionAnimation animationKind)
        {
            return animationKind switch
            {
                TacticalEquipmentActionAnimation.Throw => Actor.PlayThrowAnimation(),
                TacticalEquipmentActionAnimation.WeaponAttack => Actor.PlaySpecialWeaponAttackAnimation(),
                TacticalEquipmentActionAnimation.MeleeAttack => Actor.PlayMeleeActionAnimation(),
                TacticalEquipmentActionAnimation.ShieldBlock => Actor.PlayShieldBlockAnimation(),
                _ => Actor.PlayUseItemAnimation()
            };
        }
    }
}
