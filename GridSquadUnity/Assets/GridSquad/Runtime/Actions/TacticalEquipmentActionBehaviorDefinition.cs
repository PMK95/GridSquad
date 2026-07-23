using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum TacticalEquipmentActionEffect
    {
        RestoreHealth,
        CombatStim,
        TemporaryBarrier,
        ReplenishAmmunition,
        DirectDamage
    }

    public enum TacticalEquipmentActionAnimation
    {
        UseItem,
        Throw,
        WeaponAttack,
        MeleeAttack,
        ShieldBlock
    }

    [CreateAssetMenu(
        menuName = "GridSquad/Actions/Tactical Equipment Behavior",
        fileName = "TacticalEquipmentBehavior")]
    public sealed class TacticalEquipmentActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        [SerializeField] private TacticalEquipmentActionEffect effect;
        [SerializeField] private TacticalEquipmentActionAnimation animationKind;
        [SerializeField, Min(0)] private int effectAmount = 20;
        [SerializeField, Min(0f)] private float durationSeconds = 6f;
        [SerializeField, Min(0.1f)] private float movementSpeedMultiplier = 1f;
        [SerializeField, Range(0.1f, 1f)] private float fireIntervalMultiplier = 1f;
        [SerializeField, Min(1)] private int maximumRangeCells = 5;
        [SerializeField, Min(0)] private int knockbackCells;
        [SerializeField, Min(0f)] private float stunSeconds;

        public TacticalEquipmentActionEffect Effect => effect;
        public TacticalEquipmentActionAnimation AnimationKind => animationKind;
        public int EffectAmount => Mathf.Max(0, effectAmount);
        public float DurationSeconds => Mathf.Max(0f, durationSeconds);
        public float MovementSpeedMultiplier => Mathf.Max(0.1f, movementSpeedMultiplier);
        public float FireIntervalMultiplier => Mathf.Clamp(fireIntervalMultiplier, 0.1f, 1f);
        public int MaximumRangeCells => Mathf.Max(1, maximumRangeCells);
        public int KnockbackCells => Mathf.Max(0, knockbackCells);
        public float StunSeconds => Mathf.Max(0f, stunSeconds);
        public override CombatActionTargetingMode TargetingMode =>
            effect == TacticalEquipmentActionEffect.DirectDamage
                ? CombatActionTargetingMode.ShootableTarget
                : CombatActionTargetingMode.Self;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.Exclusive
            | CombatActionCapabilityFlags.PlayerVisible;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new TacticalEquipmentActionCandidateProvider(owner, runtime),
                new TacticalEquipmentActionExecutor(owner, runtime));

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            TacticalEquipmentActionEffect newEffect,
            TacticalEquipmentActionAnimation newAnimationKind,
            int newEffectAmount,
            float newDurationSeconds,
            float newMovementSpeedMultiplier,
            float newFireIntervalMultiplier,
            int newMaximumRangeCells,
            int newKnockbackCells,
            float newStunSeconds)
        {
            effect = newEffect;
            animationKind = newAnimationKind;
            effectAmount = Mathf.Max(0, newEffectAmount);
            durationSeconds = Mathf.Max(0f, newDurationSeconds);
            movementSpeedMultiplier = Mathf.Max(0.1f, newMovementSpeedMultiplier);
            fireIntervalMultiplier = Mathf.Clamp(newFireIntervalMultiplier, 0.1f, 1f);
            maximumRangeCells = Mathf.Max(1, newMaximumRangeCells);
            knockbackCells = Mathf.Max(0, newKnockbackCells);
            stunSeconds = Mathf.Max(0f, newStunSeconds);
        }
#endif
    }

    internal sealed class TacticalEquipmentActionCandidateProvider : CombatActionCandidateProviderBase
    {
        public TacticalEquipmentActionCandidateProvider(
            CombatActionController owner,
            CombatActionRuntime runtime)
            : base(owner, runtime)
        {
        }

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

            TacticalEquipmentActionBehaviorDefinition behavior =
                Runtime.GetBehavior<TacticalEquipmentActionBehaviorDefinition>();
            if (behavior == null)
                return;

            switch (behavior.Effect)
            {
                case TacticalEquipmentActionEffect.RestoreHealth:
                    AddHealthRecoveryCandidate(context, results);
                    return;
                case TacticalEquipmentActionEffect.CombatStim:
                    if (!context.Actor.IsStimActive && HasLivingEnemy(context))
                        AddSelfCandidate(context, results, "전투 가속", 58f);
                    return;
                case TacticalEquipmentActionEffect.TemporaryBarrier:
                    if (context.Actor.TemporaryBarrierCharges <= 0 && HasLivingEnemy(context))
                        AddSelfCandidate(context, results, "임시 방벽", 62f);
                    return;
                case TacticalEquipmentActionEffect.ReplenishAmmunition:
                    if (context.Actor.Weapon != null
                        && context.Actor.Weapon.UsesAmmo
                        && context.Actor.CurrentMagazineAmmo < context.Actor.MagazineCapacity)
                    {
                        AddSelfCandidate(context, results, "탄약 보급", 70f);
                    }
                    return;
                case TacticalEquipmentActionEffect.DirectDamage:
                    AddDirectDamageCandidate(context, results, behavior);
                    return;
            }
        }

        private void AddHealthRecoveryCandidate(
            CombatActionContext context,
            List<CombatActionCandidate> results)
        {
            int missingHealth = context.Actor.MaximumHealth - context.Actor.CurrentHealth;
            if (missingHealth <= 0)
                return;
            float missingRatio = missingHealth / (float)Mathf.Max(1, context.Actor.MaximumHealth);
            UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                .Add("체력 손실", missingRatio * 80f)
                .Add("회복 장비", 18f);
            results.Add(new CombatActionCandidate(
                Definition,
                context.Actor.ShootableTarget,
                context.Actor.CurrentCell,
                false,
                breakdown));
        }

        private void AddDirectDamageCandidate(
            CombatActionContext context,
            List<CombatActionCandidate> results,
            TacticalEquipmentActionBehaviorDefinition behavior)
        {
            ShootableTarget target = FindTargetInsideRange(context, behavior.MaximumRangeCells);
            if (target == null)
                return;
            int distance = context.Actor.CurrentCell.ManhattanDistance(target.CurrentCell);
            UtilityScoreBreakdown breakdown = new UtilityScoreBreakdown()
                .Add("고유 공격", 64f)
                .Add("근접 보정", Mathf.Max(0f, behavior.MaximumRangeCells - distance) * 4f)
                .Add("피해", Mathf.Min(20f, behavior.EffectAmount * 0.25f));
            results.Add(new CombatActionCandidate(
                Definition,
                target,
                target.CurrentCell,
                false,
                breakdown));
        }

        private ShootableTarget FindTargetInsideRange(
            CombatActionContext context,
            int maximumRangeCells)
        {
            ShootableTarget priority = context.PriorityTarget;
            if (IsValidEnemyInsideRange(context.Actor, priority, maximumRangeCells))
                return priority;

            ShootableTarget closest = null;
            int closestDistance = int.MaxValue;
            foreach (Combatant enemy in context.Director.GetLivingEnemies(context.Actor.Team))
            {
                ShootableTarget candidate = enemy != null ? enemy.ShootableTarget : null;
                if (!IsValidEnemyInsideRange(context.Actor, candidate, maximumRangeCells))
                    continue;
                int distance = context.Actor.CurrentCell.ManhattanDistance(candidate.CurrentCell);
                if (distance >= closestDistance)
                    continue;
                closest = candidate;
                closestDistance = distance;
            }
            return closest;
        }

        private static bool IsValidEnemyInsideRange(
            Combatant actor,
            ShootableTarget target,
            int maximumRangeCells)
        {
            return actor != null
                && target != null
                && target.IsAlive
                && target.TargetTeam != actor.Team
                && actor.CurrentCell.ManhattanDistance(target.CurrentCell) <= maximumRangeCells;
        }

        private static bool HasLivingEnemy(CombatActionContext context)
        {
            foreach (Combatant ignored in context.Director.GetLivingEnemies(context.Actor.Team))
                return true;
            return false;
        }

        private void AddSelfCandidate(
            CombatActionContext context,
            List<CombatActionCandidate> results,
            string label,
            float score)
        {
            results.Add(new CombatActionCandidate(
                Definition,
                context.Actor.ShootableTarget,
                context.Actor.CurrentCell,
                false,
                new UtilityScoreBreakdown().Add(label, score)));
        }
    }

    internal sealed class TacticalEquipmentActionExecutor : CombatActionExecutorBase
    {
        private CombatActionCandidate activeCandidate;
        private float windupRemaining;

        public TacticalEquipmentActionExecutor(
            CombatActionController owner,
            CombatActionRuntime runtime)
            : base(owner, runtime)
        {
        }

        public override bool CanBegin(CombatActionCandidate candidate, out string failureReason)
        {
            if (!base.CanBegin(candidate, out failureReason))
                return false;
            TacticalEquipmentActionBehaviorDefinition behavior =
                Runtime.GetBehavior<TacticalEquipmentActionBehaviorDefinition>();
            if (behavior == null)
                return Fail("장비 행동 설정이 없습니다.", out failureReason);
            if (behavior.Effect == TacticalEquipmentActionEffect.DirectDamage)
            {
                if (candidate.Target == null || !candidate.Target.IsAlive)
                    return Fail("공격할 대상이 없습니다.", out failureReason);
                if (candidate.Target.TargetTeam == Actor.Team)
                    return Fail("아군에게 사용할 수 없습니다.", out failureReason);
                if (Actor.CurrentCell.ManhattanDistance(candidate.Target.CurrentCell)
                    > behavior.MaximumRangeCells)
                {
                    return Fail("대상이 행동 사거리 밖에 있습니다.", out failureReason);
                }
            }
            failureReason = string.Empty;
            return true;
        }

        public override bool Begin(CombatActionIntent intent, out string failureReason)
        {
            if (!CanBegin(intent.Candidate, out failureReason))
                return false;
            activeCandidate = intent.Candidate;
            Actor.StopMovementAtCurrentCell();
            Actor.PrepareForExclusiveCombatAction();
            float animationDuration = PlayConfiguredAnimation(
                Runtime.GetBehavior<TacticalEquipmentActionBehaviorDefinition>().AnimationKind);
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

            TacticalEquipmentActionBehaviorDefinition behavior =
                Runtime.GetBehavior<TacticalEquipmentActionBehaviorDefinition>();
            if (behavior == null)
                return CombatActionExecutionStatus.Failed;

            Runtime.ConsumeChargeAndStartCooldown();
            ApplyConfiguredEffect(behavior);
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

        private void ApplyConfiguredEffect(TacticalEquipmentActionBehaviorDefinition behavior)
        {
            switch (behavior.Effect)
            {
                case TacticalEquipmentActionEffect.RestoreHealth:
                    Actor.RestoreHealth(behavior.EffectAmount);
                    break;
                case TacticalEquipmentActionEffect.CombatStim:
                    Actor.ApplyStim(
                        $"action:{Runtime.RuntimeKey}:stim",
                        Runtime.Definition.DisplayName,
                        Runtime.Definition.Icon,
                        behavior.DurationSeconds,
                        behavior.MovementSpeedMultiplier,
                        behavior.FireIntervalMultiplier);
                    break;
                case TacticalEquipmentActionEffect.TemporaryBarrier:
                    Actor.ApplyTemporaryBarrier(
                        behavior.EffectAmount,
                        behavior.DurationSeconds);
                    break;
                case TacticalEquipmentActionEffect.ReplenishAmmunition:
                    Actor.ReplenishWeaponAmmunition(behavior.EffectAmount);
                    break;
                case TacticalEquipmentActionEffect.DirectDamage:
                    ApplyDirectDamage(behavior);
                    break;
            }
        }

        private void ApplyDirectDamage(TacticalEquipmentActionBehaviorDefinition behavior)
        {
            ShootableTarget target = activeCandidate.Target;
            if (target == null || !target.IsAlive)
                return;
            target.ApplyDamage(new CombatDamageRequest(Actor, Actor.Weapon, behavior.EffectAmount));
            Combatant targetCombatant = target.Combatant;
            if (targetCombatant == null)
                return;
            if (behavior.StunSeconds > 0f)
                targetCombatant.ApplyStun(behavior.StunSeconds);
            if (behavior.KnockbackCells > 0)
                targetCombatant.TryApplyKnockback(Actor.CurrentCell, behavior.KnockbackCells);
        }
    }
}
