using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(ShootableTarget),
        typeof(CombatTargetingController),
        typeof(WeaponRuntimeController))]
    [RequireComponent(typeof(RangedFireCycleController))]
    public sealed class RangedAttackController : MonoBehaviour
    {
        private Combatant owner;
        private WeaponLoadout weaponLoadout;
        private CombatTargetingController targeting;
        private WeaponRuntimeController weaponRuntime;
        private RangedFireCycleController fireCycle;

        public ShootableTarget CurrentTarget => targeting.CurrentTarget;
        public ShotEvaluation CurrentShotEvaluation => targeting.CurrentShotEvaluation;
        public FireCycleState FireState => fireCycle.FireState;
        public bool PeekEnabled => targeting.PeekEnabled;
        public int CurrentMagazineAmmo => weaponRuntime.CurrentMagazineAmmo;
        public int ReserveAmmo => weaponRuntime.ReserveAmmo;
        public bool IsReloading => fireCycle.IsReloading;
        public bool IsOutOfAmmo => fireCycle.IsOutOfAmmo;
        public int MagazineCapacity => weaponRuntime.MagazineCapacity;
        public float ReloadProgress => fireCycle.ReloadProgress;
        public float MagazineFillRatio => fireCycle.MagazineFillRatio;
        public float FireStateRemainingSeconds => fireCycle.FireStateRemainingSeconds;
        public WeaponDefinition Weapon => weaponRuntime.Weapon;
        public Vector3 MuzzlePosition => targeting.MuzzlePosition;
        public float MuzzleHeight => targeting.MuzzleHeight;
        public GridCoordinate CurrentExposureCell => targeting.CurrentExposureCell;
        public Vector3 CurrentExposureCenter => targeting.CurrentExposureCenter;
        public GridCoordinate CurrentIndicatorShotOriginCell => targeting.CurrentIndicatorShotOriginCell;
        public Vector3 CurrentIndicatorShotOrigin => targeting.CurrentIndicatorShotOrigin;

        public void Initialize(
            Combatant newOwner,
            ShootableTarget newSelfTarget,
            ShotEvaluator newShotEvaluator,
            CombatTuning newTuning,
            UnitAnimationController newAnimationController,
            CombatantFeedbackPresenter newFeedbackPresenter,
            WeaponLoadout newWeaponLoadout,
            Transform newMuzzle)
        {
            owner = newOwner;
            weaponLoadout = newWeaponLoadout;
            EnsureAbilityComponents();
            targeting.Initialize(owner, newSelfTarget, newShotEvaluator, newMuzzle);
            weaponRuntime.Initialize(owner, weaponLoadout);
            fireCycle.Initialize(
                owner,
                targeting,
                weaponRuntime,
                newShotEvaluator,
                newTuning,
                newAnimationController,
                newFeedbackPresenter);
        }

        public void SetBehaviorTarget(ShootableTarget target)
        {
            if (targeting.SetTarget(target))
                fireCycle.Reset();
        }

        public void SetManualTargetHoverPreview(ShootableTarget target)
            => targeting.SetManualHoverTarget(target);

        public void ClearManualTargetHoverPreview()
            => targeting.ClearManualHoverTarget();

        public void GetPresentationTarget(
            out ShootableTarget presentationTarget,
            out ShotEvaluation presentationEvaluation)
            => targeting.GetPresentationTarget(out presentationTarget, out presentationEvaluation);

        public void RefreshShotEvaluationForCurrentTarget()
            => targeting.RefreshShotEvaluation();

        public void SetPeekEnabled(bool enabled)
        {
            if (targeting.SetPeekEnabled(enabled))
                fireCycle.Reset();
        }

        public void UpdateAutomaticPeekForCurrentTarget(bool allowed)
        {
            if (targeting.UpdateAutomaticPeek(allowed))
                fireCycle.Reset();
        }

        public void TickAutomaticFireCycleFromBehavior()
            => fireCycle.Tick();

        public bool InitializeWeaponLoadoutForBattle(out string failureReason)
        {
            bool initialized = weaponRuntime.InitializeLoadoutForBattle(out failureReason);
            if (initialized)
            {
                targeting.SetMuzzle(weaponLoadout?.Mount?.ActiveMuzzle);
                fireCycle.Reset(true);
            }
            return initialized;
        }

        public void RefreshEquippedWeapon()
        {
            weaponRuntime?.RefreshEquippedWeapon();
            targeting?.SetMuzzle(weaponLoadout?.Mount?.ActiveMuzzle);
            fireCycle?.Reset(true);
        }

        public void SetFireIntervalMultiplier(float multiplier)
            => fireCycle.SetFireIntervalMultiplier(multiplier);

        public int ReplenishAmmunition(int amount)
        {
            int replenished = weaponRuntime != null
                ? weaponRuntime.ReplenishAmmunition(amount)
                : 0;
            if (replenished > 0)
                fireCycle?.Reset(true);
            return replenished;
        }

        public void ResumePresentationAfterHitReaction()
            => fireCycle.ResumePresentationAfterHitReaction();

        public void ResetBehaviorFireCycle()
            => fireCycle.Reset(true);

        public void PrepareForExclusiveCombatAction()
            => fireCycle.Reset(true);

        public void PrepareForEntityRemoval()
        {
            targeting.ClearForEntityRemoval();
            fireCycle.Reset(true);
        }

        public void PrepareForBattleResult()
        {
            targeting.SetTarget(null);
            targeting.SetPeekEnabled(false);
            fireCycle.SetFireIntervalMultiplier(1f);
            fireCycle.Reset(true);
        }

        private void EnsureAbilityComponents()
        {
            targeting = targeting != null ? targeting : GetComponent<CombatTargetingController>();
            if (targeting == null)
                targeting = gameObject.AddComponent<CombatTargetingController>();
            weaponRuntime = weaponRuntime != null ? weaponRuntime : GetComponent<WeaponRuntimeController>();
            if (weaponRuntime == null)
                weaponRuntime = gameObject.AddComponent<WeaponRuntimeController>();
            fireCycle = fireCycle != null ? fireCycle : GetComponent<RangedFireCycleController>();
            if (fireCycle == null)
                fireCycle = gameObject.AddComponent<RangedFireCycleController>();
        }
    }
}
