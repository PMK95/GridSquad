using Animancer;
using RootMotion.FinalIK;
using UnityEngine;

namespace GridSquad
{
    public sealed class UnitAnimationController : MonoBehaviour
    {
        [Header("컴포넌트")]
        [SerializeField] private AnimancerComponent animancer;
        [SerializeField] private Animator animator;
        [SerializeField] private AimIK aimIk;

        [Header("애니메이션")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkingClip;
        [SerializeField] private AnimationClip aimingClip;
        [SerializeField] private AnimationClip shootClip;
        [SerializeField] private AnimationClip reloadClip;
        [SerializeField] private AnimationClip hitClip;
        [SerializeField] private AnimationClip deathClip;
        [SerializeField] private AnimationClip throwClip;
        [SerializeField] private AnimationClip useItemClip;
        [SerializeField] private AnimationClip dashClip;

        [Header("전환")]
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float aimReleaseDuration = 0.15f;

        private AnimationClip currentClip;
        private Transform aimTarget;
        private float aimEnterDuration = 0.6f;
        private float shotAnimationEndTime;
        private float hitAnimationEndTime;
        private float actionAnimationEndTime;
        private bool moving;
        private bool aiming;
        private bool reloading;
        private bool dead;

        public float ShotDuration => shootClip != null ? shootClip.length : 0f;
        public bool IsAimReady => aiming
            && aimIk != null
            && aimIk.solver.target == aimTarget
            && aimIk.solver.IKPositionWeight >= 0.999f;

        public void Initialize()
        {
            if (animancer == null)
                animancer = GetComponent<AnimancerComponent>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (aimIk == null)
                aimIk = GetComponent<AimIK>();

            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
            }

            if (aimIk != null)
            {
                aimIk.solver.target = null;
                aimIk.solver.IKPositionWeight = 0f;
            }

            PlayLoop(idleClip);
        }

        private void Update()
        {
            UpdateAimIkWeight();
            if (dead
                || reloading
                || Time.time < hitAnimationEndTime
                || Time.time < shotAnimationEndTime
                || Time.time < actionAnimationEndTime)
                return;

            PlayLoop(aiming ? aimingClip : moving ? walkingClip : idleClip);
        }

        public void SetMovementState(bool isMoving)
        {
            moving = isMoving;
        }

        public void BeginAimingAt(Transform newAimTarget, float enterDuration)
        {
            if (dead || newAimTarget == null)
                return;

            aimTarget = newAimTarget;
            aimEnterDuration = Mathf.Max(0.01f, enterDuration);
            aiming = true;
            reloading = false;
            if (aimIk != null)
                aimIk.solver.target = aimTarget;
            if (Time.time >= hitAnimationEndTime && Time.time >= shotAnimationEndTime)
                PlayLoop(aimingClip);
        }

        public void StopAiming()
        {
            aiming = false;
        }

        public void PlayShot()
        {
            if (dead || shootClip == null || animancer == null)
                return;

            reloading = false;
            AnimancerState state = animancer.Play(shootClip, crossFadeDuration, FadeMode.FromStart);
            state.Speed = 1f;
            shotAnimationEndTime = Time.time + shootClip.length;
            currentClip = shootClip;
        }

        public void BeginReload(float duration, float normalizedProgress = 0f)
        {
            if (dead)
                return;

            aiming = false;
            reloading = true;
            if (reloadClip == null || animancer == null)
                return;

            AnimancerState state = animancer.Play(reloadClip, crossFadeDuration, FadeMode.FromStart);
            state.NormalizedTime = Mathf.Clamp01(normalizedProgress);
            state.Speed = reloadClip.length / Mathf.Max(0.01f, duration);
            currentClip = reloadClip;
        }

        public void CompleteReload()
        {
            reloading = false;
            if (!dead && Time.time >= hitAnimationEndTime)
                PlayLoop(moving ? walkingClip : idleClip);
        }

        public float PlayHitReaction()
        {
            if (dead || hitClip == null || animancer == null)
                return 0f;

            AnimancerState state = animancer.Play(hitClip, crossFadeDuration, FadeMode.FromStart);
            state.Speed = 1f;
            hitAnimationEndTime = Time.time + hitClip.length;
            currentClip = hitClip;
            return hitClip.length;
        }

        public float PlayDeath()
        {
            dead = true;
            aiming = false;
            reloading = false;
            if (aimIk != null)
            {
                aimIk.solver.IKPositionWeight = 0f;
                aimIk.solver.target = null;
            }

            if (deathClip == null || animancer == null)
                return 0f;
            AnimancerState state = animancer.Play(deathClip, crossFadeDuration, FadeMode.FromStart);
            state.Speed = 1f;
            currentClip = deathClip;
            return deathClip.length;
        }

        public float PlayThrowAction() => PlayActionClip(throwClip);

        public float PlayUseItemAction() => PlayActionClip(useItemClip);

        public float PlayDashAction() => PlayActionClip(dashClip);

        private float PlayActionClip(AnimationClip clip)
        {
            if (dead || clip == null || animancer == null)
                return 0f;

            aiming = false;
            reloading = false;
            AnimancerState state = animancer.Play(clip, crossFadeDuration, FadeMode.FromStart);
            state.Speed = 1f;
            actionAnimationEndTime = Time.time + clip.length;
            currentClip = clip;
            return clip.length;
        }

        private void UpdateAimIkWeight()
        {
            if (aimIk == null)
                return;

            bool suppressAim = dead || reloading || Time.time < hitAnimationEndTime;
            float targetWeight = aiming && !suppressAim && aimTarget != null ? 1f : 0f;
            float duration = targetWeight > aimIk.solver.IKPositionWeight
                ? aimEnterDuration
                : aimReleaseDuration;
            aimIk.solver.IKPositionWeight = Mathf.MoveTowards(
                aimIk.solver.IKPositionWeight,
                targetWeight,
                Time.deltaTime / Mathf.Max(0.01f, duration));

            if (targetWeight <= 0f && aimIk.solver.IKPositionWeight <= 0f)
            {
                aimIk.solver.target = null;
                if (!aiming)
                    aimTarget = null;
            }
            else if (aimTarget != null)
            {
                aimIk.solver.target = aimTarget;
            }
        }

        private void PlayLoop(AnimationClip clip)
        {
            if (clip == null || animancer == null || currentClip == clip)
                return;

            AnimancerState state = animancer.Play(clip, crossFadeDuration);
            state.Speed = 1f;
            currentClip = clip;
        }
    }
}
