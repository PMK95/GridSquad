using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatantStatusEffectController : MonoBehaviour
    {
        private Combatant owner;
        private UnitStatModifierHandle stimModifierHandle;
        private float stimRemainingSeconds;
        private float stunRemainingSeconds;
        private int temporaryBarrierCharges;
        private float temporaryBarrierRemainingSeconds;

        public bool IsStimActive => stimRemainingSeconds > 0f;
        public bool IsStunned => stunRemainingSeconds > 0f;
        public float StimRemainingSeconds => Mathf.Max(0f, stimRemainingSeconds);
        public int TemporaryBarrierCharges => Mathf.Max(0, temporaryBarrierCharges);

        public void Initialize(Combatant newOwner)
        {
            owner = newOwner;
        }

        public void ApplyStim(
            string sourceKey,
            string sourceDisplayName,
            Sprite sourceIcon,
            float durationSeconds,
            float movementSpeedMultiplier,
            float fireIntervalMultiplier)
        {
            stimRemainingSeconds = Mathf.Max(0f, durationSeconds);
            if (owner == null || stimRemainingSeconds <= 0f)
                return;

            UnitStatCatalog catalog = owner.StatCatalog;
            System.Collections.Generic.List<UnitStatModifier> modifiers = new();
            if (catalog?.MovementSpeedMultiplier != null)
            {
                modifiers.Add(new UnitStatModifier(
                    catalog.MovementSpeedMultiplier,
                    UnitStatModifierOperation.Add,
                    Mathf.Max(0.1f, movementSpeedMultiplier) - 1f));
            }
            if (catalog?.FireRateMultiplier != null)
            {
                float intervalMultiplier = Mathf.Clamp(fireIntervalMultiplier, 0.1f, 10f);
                modifiers.Add(new UnitStatModifier(
                    catalog.FireRateMultiplier,
                    UnitStatModifierOperation.Add,
                    1f / intervalMultiplier - 1f));
            }
            stimModifierHandle = owner.AddTimedStatModifiers(
                string.IsNullOrWhiteSpace(sourceKey) ? "status:stim" : sourceKey,
                string.IsNullOrWhiteSpace(sourceDisplayName) ? "전투 자극제" : sourceDisplayName,
                sourceIcon,
                UnitStatModifierSourceKind.Action,
                modifiers,
                stimRemainingSeconds);
        }

        public void ApplyStun(float durationSeconds)
        {
            stunRemainingSeconds = Mathf.Max(stunRemainingSeconds, durationSeconds);
            owner?.ResetBehaviorFireCycle();
        }

        public void ApplyTemporaryBarrier(int charges, float durationSeconds)
        {
            temporaryBarrierCharges = Mathf.Max(0, charges);
            temporaryBarrierRemainingSeconds = Mathf.Max(0f, durationSeconds);
        }

        public bool TryConsumeTemporaryBarrier(out int remainingCharges)
        {
            if (temporaryBarrierCharges <= 0 || temporaryBarrierRemainingSeconds <= 0f)
            {
                remainingCharges = 0;
                return false;
            }
            temporaryBarrierCharges--;
            remainingCharges = temporaryBarrierCharges;
            return true;
        }

        public void Tick()
        {
            stunRemainingSeconds = Mathf.Max(0f, stunRemainingSeconds - Time.deltaTime);
            if (temporaryBarrierRemainingSeconds > 0f)
            {
                temporaryBarrierRemainingSeconds = Mathf.Max(
                    0f,
                    temporaryBarrierRemainingSeconds - Time.deltaTime);
                if (temporaryBarrierRemainingSeconds <= 0f)
                    temporaryBarrierCharges = 0;
            }
            if (IsStimActive)
            {
                stimRemainingSeconds = Mathf.Max(0f, stimRemainingSeconds - Time.deltaTime);
                if (stimRemainingSeconds <= 0f)
                {
                    owner?.RemoveStatModifierHandle(stimModifierHandle);
                    stimModifierHandle = default;
                }
            }
        }

        public void ResetState()
        {
            owner?.RemoveStatModifierHandle(stimModifierHandle);
            stimModifierHandle = default;
            stimRemainingSeconds = 0f;
            stunRemainingSeconds = 0f;
            temporaryBarrierCharges = 0;
            temporaryBarrierRemainingSeconds = 0f;
            owner?.ResetBehaviorFireCycle();
        }
    }
}
