using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatantHitReactionController : MonoBehaviour
    {
        private Combatant owner;
        private CombatTuning tuning;
        private UnitAnimationController animationController;
        private RangedAttackController rangedAttackController;
        private float remainingSeconds;

        public bool IsReacting => remainingSeconds > 0f;

        public void Initialize(
            Combatant newOwner,
            CombatTuning newTuning,
            UnitAnimationController newAnimationController,
            RangedAttackController newRangedAttackController)
        {
            owner = newOwner;
            tuning = newTuning;
            animationController = newAnimationController;
            rangedAttackController = newRangedAttackController;
        }

        public void TryStart(EntityHealth health)
        {
            if (health == null || !health.IsAlive || !ShouldInterruptCurrentAction())
                return;
            float duration = animationController != null
                ? animationController.PlayHitReaction()
                : 0f;
            if (duration > 0f)
                remainingSeconds = duration;
        }

        public void Tick()
        {
            if (!IsReacting)
                return;
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds <= 0f)
                rangedAttackController?.ResumePresentationAfterHitReaction();
        }

        public void ResetState()
        {
            remainingSeconds = 0f;
        }

        private bool ShouldInterruptCurrentAction()
        {
            float chancePercent = tuning != null
                ? tuning.HitReactionInterruptChancePercent
                : 0f;
            return Random.value < chancePercent / 100f;
        }
    }
}
