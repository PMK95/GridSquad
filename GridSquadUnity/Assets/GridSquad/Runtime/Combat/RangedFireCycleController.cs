using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum PreparedShotStatus
    {
        Waiting,
        Fired,
        Failed
    }

    [DisallowMultipleComponent]
    public sealed class RangedFireCycleController : MonoBehaviour
    {
        private Combatant owner;
        private CombatTargetingController targeting;
        private WeaponRuntimeController weaponRuntime;
        private ShotEvaluator shotEvaluator;
        private CombatTuning tuning;
        private UnitAnimationController animationController;
        private CombatantFeedbackPresenter feedbackPresenter;
        private FireCycleState fireCycleState;
        private ShootableTarget aimingTarget;
        private float fireStateRemainingSeconds;
        private int pendingReloadAmmo;
        private float reloadElapsedSeconds;
        private bool reloadAnimationStarted;
        private float fireIntervalMultiplier = 1f;
        private int attackSequence;
        private ShootableTarget retainedAimTarget;
        private bool aimRetentionValid;

        public FireCycleState FireState => fireCycleState;
        public bool IsReloading => fireCycleState == FireCycleState.Reloading;
        public bool IsOutOfAmmo => fireCycleState == FireCycleState.OutOfAmmo;
        public float ReloadProgress => IsReloading && reloadAnimationStarted && Weapon != null
            ? Mathf.Clamp01(reloadElapsedSeconds / Mathf.Max(0.01f, Weapon.ReloadDuration))
            : 0f;
        public float MagazineFillRatio
        {
            get
            {
                float displayedAmmo = weaponRuntime.CurrentMagazineAmmo;
                if (IsReloading && reloadAnimationStarted)
                    displayedAmmo += pendingReloadAmmo * ReloadProgress;
                return Mathf.Clamp01(displayedAmmo / weaponRuntime.MagazineCapacity);
            }
        }
        public float FireStateRemainingSeconds => IsReloading && reloadAnimationStarted && Weapon != null
            ? Mathf.Max(0f, Weapon.ReloadDuration - reloadElapsedSeconds)
            : Mathf.Max(0f, fireStateRemainingSeconds);
        private WeaponDefinition Weapon => weaponRuntime.Weapon;

        public void Initialize(
            Combatant newOwner,
            CombatTargetingController newTargeting,
            WeaponRuntimeController newWeaponRuntime,
            ShotEvaluator newShotEvaluator,
            CombatTuning newTuning,
            UnitAnimationController newAnimationController,
            CombatantFeedbackPresenter newFeedbackPresenter)
        {
            owner = newOwner;
            targeting = newTargeting;
            weaponRuntime = newWeaponRuntime;
            shotEvaluator = newShotEvaluator;
            tuning = newTuning;
            animationController = newAnimationController;
            feedbackPresenter = newFeedbackPresenter;
            Reset(true);
        }

        public void Tick()
        {
            if (owner.IsHitReacting || Weapon == null)
                return;
            if (IsReloading)
            {
                TickReload();
                return;
            }
            ShootableTarget target = targeting.CurrentTarget;
            if (owner.IsMoving
                || target == null
                || !target.IsAlive
                || !targeting.CurrentShotEvaluation.CanShoot)
            {
                Reset();
                return;
            }

            switch (fireCycleState)
            {
                case FireCycleState.WaitingForAim:
                    if (weaponRuntime.CurrentMagazineAmmo <= 0)
                    {
                        BeginReload(0f);
                        return;
                    }
                    aimingTarget = target;
                    fireCycleState = FireCycleState.Aiming;
                    fireStateRemainingSeconds = Weapon.AimEnterDuration * fireIntervalMultiplier;
                    animationController?.BeginAimingAt(target.AimCenterTransform, fireStateRemainingSeconds);
                    return;
                case FireCycleState.Aiming:
                    if (aimingTarget != target)
                    {
                        Reset();
                        return;
                    }
                    fireStateRemainingSeconds = Mathf.Max(0f, fireStateRemainingSeconds - Time.deltaTime);
                    if (!CanFireNow())
                        return;
                    if (!FireCurrentShot())
                    {
                        Reset();
                        return;
                    }
                    fireCycleState = FireCycleState.AimedFiring;
                    fireStateRemainingSeconds = Weapon.AimedShotInterval * fireIntervalMultiplier;
                    if (weaponRuntime.CurrentMagazineAmmo <= 0)
                        BeginReload(CalculatePostShotReloadDelay());
                    return;
                case FireCycleState.AimedFiring:
                    fireStateRemainingSeconds = Mathf.Max(0f, fireStateRemainingSeconds - Time.deltaTime);
                    if (!CanFireNow())
                        return;
                    if (!FireCurrentShot())
                    {
                        Reset();
                        return;
                    }
                    fireStateRemainingSeconds = Weapon.AimedShotInterval * fireIntervalMultiplier;
                    if (weaponRuntime.CurrentMagazineAmmo <= 0)
                        BeginReload(CalculatePostShotReloadDelay());
                    return;
            }
        }

        public bool TryBeginPreparedShot(
            ShootableTarget target,
            out float windupDuration,
            out string failureReason)
        {
            windupDuration = 0f;
            if (Weapon == null)
            {
                failureReason = "사용할 무기가 없습니다.";
                return false;
            }
            if (IsReloading)
            {
                failureReason = "재장전 중입니다.";
                return false;
            }
            if (target == null || !target.IsAlive || target.TargetTeam == owner.Team)
            {
                failureReason = "유효한 사격 대상이 없습니다.";
                return false;
            }

            bool retainAim = aimRetentionValid
                && retainedAimTarget == target
                && !owner.IsMoving;
            aimingTarget = target;
            fireCycleState = FireCycleState.Aiming;
            windupDuration = retainAim
                ? 0f
                : Weapon.AimEnterDuration * fireIntervalMultiplier;
            fireStateRemainingSeconds = windupDuration;
            animationController?.BeginAimingAt(
                target.AimCenterTransform,
                Mathf.Max(0.01f, windupDuration));
            failureReason = string.Empty;
            return true;
        }

        public PreparedShotStatus TickPreparedShot(
            float deltaTime,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (Weapon == null || aimingTarget == null || !aimingTarget.IsAlive)
            {
                InvalidateRetainedAim();
                failureReason = "사격 대상이 유효하지 않습니다.";
                return PreparedShotStatus.Failed;
            }
            if (owner.IsMoving)
            {
                InvalidateRetainedAim();
                failureReason = "이동 중에는 사격할 수 없습니다.";
                return PreparedShotStatus.Failed;
            }
            if (IsReloading)
            {
                failureReason = "재장전 중입니다.";
                return PreparedShotStatus.Failed;
            }

            fireStateRemainingSeconds = Mathf.Max(
                0f,
                fireStateRemainingSeconds - Mathf.Max(0f, deltaTime));
            targeting.RefreshShotEvaluation();
            if (!targeting.CurrentShotEvaluation.CanShoot)
            {
                failureReason = "현재 사격 조건을 충족하지 못했습니다.";
                return PreparedShotStatus.Failed;
            }
            if (fireStateRemainingSeconds > 0f
                || !IsAimAlignedWithCurrentTarget()
                || (animationController != null && !animationController.IsAimReady))
            {
                return PreparedShotStatus.Waiting;
            }
            if (weaponRuntime.CurrentMagazineAmmo <= 0)
            {
                InvalidateRetainedAim();
                BeginReload(0f);
                failureReason = "탄창이 비어 있습니다.";
                return PreparedShotStatus.Failed;
            }
            if (!FireCurrentShot())
            {
                InvalidateRetainedAim();
                failureReason = "사격 실행에 실패했습니다.";
                return PreparedShotStatus.Failed;
            }

            retainedAimTarget = aimingTarget;
            aimRetentionValid = true;
            fireCycleState = FireCycleState.AimedFiring;
            fireStateRemainingSeconds = 0f;
            if (weaponRuntime.CurrentMagazineAmmo <= 0)
            {
                InvalidateRetainedAim();
                BeginReload(CalculatePostShotReloadDelay());
            }
            return PreparedShotStatus.Fired;
        }

        public float GetPreparedShotRecoveryDuration()
        {
            if (Weapon == null)
                return 0f;
            float interval = Weapon.AimedShotInterval * fireIntervalMultiplier;
            float animationDuration = animationController != null
                ? animationController.ShotDuration
                : 0f;
            return Mathf.Max(interval, animationDuration);
        }

        public void CompletePreparedShotRecovery()
        {
            if (IsReloading)
                return;
            fireCycleState = FireCycleState.AimedFiring;
            fireStateRemainingSeconds = 0f;
        }

        public void Reset(bool cancelReload = false)
        {
            if (IsReloading && !cancelReload)
                return;
            fireCycleState = FireCycleState.WaitingForAim;
            fireStateRemainingSeconds = 0f;
            aimingTarget = null;
            pendingReloadAmmo = 0;
            reloadElapsedSeconds = 0f;
            reloadAnimationStarted = false;
            InvalidateRetainedAim();
            animationController?.StopAiming();
            if (cancelReload)
                animationController?.CompleteReload();
        }

        public void SetFireIntervalMultiplier(float multiplier)
        {
            fireIntervalMultiplier = Mathf.Clamp(multiplier, 0.1f, 10f);
        }

        public void ResumePresentationAfterHitReaction()
        {
            if (IsReloading && reloadAnimationStarted && Weapon != null)
            {
                animationController?.BeginReload(Weapon.ReloadDuration, ReloadProgress);
                return;
            }
            if ((fireCycleState == FireCycleState.Aiming
                    || fireCycleState == FireCycleState.AimedFiring)
                && targeting.CurrentTarget != null
                && targeting.CurrentTarget.IsAlive
                && Weapon != null)
            {
                animationController?.BeginAimingAt(
                    targeting.CurrentTarget.AimCenterTransform,
                    Weapon.AimEnterDuration);
            }
        }

        private bool CanFireNow()
        {
            return fireStateRemainingSeconds <= 0f
                && IsAimAlignedWithCurrentTarget()
                && (animationController == null || animationController.IsAimReady);
        }

        private bool FireCurrentShot()
        {
            ShootableTarget target = targeting.CurrentTarget;
            if (owner.IsMoving || target == null || !target.IsAlive || !IsAimAlignedWithCurrentTarget())
                return false;
            targeting.RefreshShotEvaluation();
            ShotEvaluation evaluation = targeting.CurrentShotEvaluation;
            if (!evaluation.CanShoot || weaponRuntime.CurrentMagazineAmmo <= 0)
                return false;
            if (!weaponRuntime.TryConsumeRound())
                return false;
            attackSequence++;
            WeaponAttackMode attackMode = Weapon != null && Weapon.AttackBehavior != null
                ? Weapon.AttackBehavior.Mode
                : WeaponAttackMode.Hitscan;
            if (attackMode != WeaponAttackMode.Melee)
                weaponRuntime.Loadout?.Mount?.ActivePresentation?.PlayFireFeedback();
            bool fired = attackMode == WeaponAttackMode.Shotgun
                ? FireShotgun(target, evaluation, attackSequence)
                : FireSingleAttack(target, evaluation, attackSequence, attackMode != WeaponAttackMode.Melee);
            if (!fired)
                return false;
            weaponRuntime.ApplyActiveWeaponWear();
            animationController?.PlayWeaponAttack(
                attackMode);
            return true;
        }

        private bool FireSingleAttack(
            ShootableTarget intendedTarget,
            ShotEvaluation evaluation,
            int currentAttackSequence,
            bool showTracer)
        {
            ShotImpactResult impact = shotEvaluator.CalculateShotImpact(owner, intendedTarget, evaluation);
            if (!impact.Fired)
                return false;
            if (showTracer)
                feedbackPresenter?.PlayShotTracer(evaluation.ShotOrigin, impact.ImpactPoint);
            if (!impact.AppliedDamage)
            {
                intendedTarget.PlayMissFeedback();
                return true;
            }
            impact.ImpactTarget.ApplyDamage(new CombatDamageRequest(
                owner,
                Weapon,
                owner.EffectiveWeaponDamage,
                currentAttackSequence));
            Weapon?.ApplyHitEffects(owner, impact.ImpactTarget);
            return true;
        }

        private bool FireShotgun(
            ShootableTarget intendedTarget,
            ShotEvaluation evaluation,
            int currentAttackSequence)
        {
            ShotgunWeaponAttackBehaviorDefinition shotgun =
                Weapon.AttackBehavior as ShotgunWeaponAttackBehaviorDefinition;
            if (shotgun == null)
                return FireSingleAttack(intendedTarget, evaluation, currentAttackSequence, true);

            Dictionary<ShootableTarget, int> accumulatedDamage = new();
            int damagePerPellet = Mathf.Max(
                1,
                Mathf.RoundToInt(owner.EffectiveWeaponDamage / (float)shotgun.PelletCount));
            bool anyPelletFired = false;
            bool intendedTargetWasHit = false;
            for (int pelletIndex = 0; pelletIndex < shotgun.PelletCount; pelletIndex++)
            {
                ShotImpactResult impact = shotEvaluator.CalculateShotImpact(owner, intendedTarget, evaluation);
                if (!impact.Fired)
                    continue;
                anyPelletFired = true;
                feedbackPresenter?.PlayShotTracer(evaluation.ShotOrigin, impact.ImpactPoint);
                if (!impact.AppliedDamage)
                    continue;
                intendedTargetWasHit |= impact.ImpactTarget == intendedTarget;
                accumulatedDamage.TryGetValue(impact.ImpactTarget, out int currentDamage);
                accumulatedDamage[impact.ImpactTarget] = currentDamage + damagePerPellet;
            }

            foreach (KeyValuePair<ShootableTarget, int> hit in accumulatedDamage)
            {
                hit.Key.ApplyDamage(new CombatDamageRequest(
                    owner,
                    Weapon,
                    hit.Value,
                    currentAttackSequence));
                // 한 공격에서 같은 대상은 피해를 합산하고 부가 효과는 한 번만 적용한다.
                Weapon.ApplyHitEffects(owner, hit.Key);
            }
            if (anyPelletFired && !intendedTargetWasHit)
                intendedTarget.PlayMissFeedback();
            return anyPelletFired;
        }

        private bool IsAimAlignedWithCurrentTarget()
        {
            ShootableTarget target = targeting.CurrentTarget;
            if (target == null || !target.IsAlive)
                return false;
            Vector3 direction = target.CurrentExposureCenter - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return true;
            float tolerance = tuning != null ? tuning.FireAimToleranceDegrees : 5f;
            return Vector3.Angle(transform.forward, direction) <= tolerance;
        }

        private float CalculatePostShotReloadDelay()
        {
            float animationDuration = animationController != null ? animationController.ShotDuration : 0f;
            return Mathf.Max(Weapon.AimedShotInterval, animationDuration);
        }

        private void BeginReload(float shotAnimationDelay)
        {
            InvalidateRetainedAim();
            if (weaponRuntime.CurrentMagazineAmmo >= weaponRuntime.MagazineCapacity)
            {
                fireCycleState = FireCycleState.WaitingForAim;
                return;
            }
            pendingReloadAmmo = weaponRuntime.CalculateReloadAmount();
            if (pendingReloadAmmo <= 0)
            {
                fireCycleState = FireCycleState.OutOfAmmo;
                fireStateRemainingSeconds = 0f;
                animationController?.StopAiming();
                return;
            }
            reloadElapsedSeconds = 0f;
            reloadAnimationStarted = false;
            fireCycleState = FireCycleState.Reloading;
            fireStateRemainingSeconds = Mathf.Max(0f, shotAnimationDelay);
            if (fireStateRemainingSeconds <= 0f)
                StartReloadAnimation();
        }

        private void TickReload()
        {
            if (!reloadAnimationStarted)
            {
                fireStateRemainingSeconds = Mathf.Max(0f, fireStateRemainingSeconds - Time.deltaTime);
                if (fireStateRemainingSeconds > 0f)
                    return;
                StartReloadAnimation();
                return;
            }
            reloadElapsedSeconds += Time.deltaTime;
            if (reloadElapsedSeconds < Weapon.ReloadDuration)
                return;
            weaponRuntime.CommitReload(pendingReloadAmmo);
            pendingReloadAmmo = 0;
            reloadElapsedSeconds = 0f;
            reloadAnimationStarted = false;
            fireCycleState = weaponRuntime.CurrentMagazineAmmo > 0
                ? FireCycleState.WaitingForAim
                : FireCycleState.OutOfAmmo;
            animationController?.CompleteReload();
        }

        private void StartReloadAnimation()
        {
            reloadAnimationStarted = true;
            reloadElapsedSeconds = 0f;
            fireStateRemainingSeconds = 0f;
            animationController?.StopAiming();
            animationController?.BeginReload(Weapon.ReloadDuration);
        }

        private void InvalidateRetainedAim()
        {
            retainedAimTarget = null;
            aimRetentionValid = false;
        }
    }
}
