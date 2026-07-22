using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatantStatusEffectController : MonoBehaviour
    {
        private RangedAttackController rangedAttackController;
        private float temporaryMovementSpeedMultiplier = 1f;
        private float stimMovementSpeedMultiplier = 1f;
        private float stimFireIntervalMultiplier = 1f;
        private float stimRemainingSeconds;
        private float stunRemainingSeconds;

        public bool IsStimActive => stimRemainingSeconds > 0f;
        public bool IsStunned => stunRemainingSeconds > 0f;
        public float StimRemainingSeconds => Mathf.Max(0f, stimRemainingSeconds);
        public float MovementSpeedMultiplier => temporaryMovementSpeedMultiplier
            * stimMovementSpeedMultiplier;

        public void Initialize(RangedAttackController newRangedAttackController)
        {
            rangedAttackController = newRangedAttackController;
        }

        public void ApplyStim(
            float durationSeconds,
            float movementSpeedMultiplier,
            float fireIntervalMultiplier)
        {
            if (IsStimActive)
                return;
            stimRemainingSeconds = Mathf.Max(0f, durationSeconds);
            stimMovementSpeedMultiplier = Mathf.Max(1f, movementSpeedMultiplier);
            stimFireIntervalMultiplier = Mathf.Clamp(fireIntervalMultiplier, 0.1f, 1f);
            rangedAttackController?.SetFireIntervalMultiplier(stimFireIntervalMultiplier);
        }

        public void SetTemporaryMovementSpeedMultiplier(float multiplier)
        {
            temporaryMovementSpeedMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public void ApplyStun(float durationSeconds)
        {
            stunRemainingSeconds = Mathf.Max(stunRemainingSeconds, durationSeconds);
            rangedAttackController?.ResetBehaviorFireCycle();
        }

        public void Tick()
        {
            stunRemainingSeconds = Mathf.Max(0f, stunRemainingSeconds - Time.deltaTime);
            if (IsStimActive)
            {
                stimRemainingSeconds = Mathf.Max(0f, stimRemainingSeconds - Time.deltaTime);
                if (stimRemainingSeconds <= 0f)
                {
                    stimMovementSpeedMultiplier = 1f;
                    stimFireIntervalMultiplier = 1f;
                    rangedAttackController?.SetFireIntervalMultiplier(1f);
                }
            }
        }

        public void ResetState()
        {
            temporaryMovementSpeedMultiplier = 1f;
            stimMovementSpeedMultiplier = 1f;
            stimFireIntervalMultiplier = 1f;
            stimRemainingSeconds = 0f;
            stunRemainingSeconds = 0f;
            rangedAttackController?.SetFireIntervalMultiplier(1f);
        }
    }
}
