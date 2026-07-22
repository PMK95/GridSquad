using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Actions/Stim Behavior", fileName = "StimBehavior")]
    public sealed class StimActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        [SerializeField, Min(0.1f)] private float durationSeconds = 8f;
        [SerializeField, Min(1f)] private float movementSpeedMultiplier = 1.35f;
        [SerializeField, Range(0.1f, 1f)] private float fireIntervalMultiplier = 0.75f;

        public float DurationSeconds => durationSeconds;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float FireIntervalMultiplier => fireIntervalMultiplier;
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.Self;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.Exclusive
            | CombatActionCapabilityFlags.PlayerVisible;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new StimCandidateProvider(owner, runtime),
                new StimExecutor(owner, runtime));

        internal override string GetRuntimeStatusText(
            CombatActionController owner,
            CombatActionRuntime runtime)
        {
            return owner.OwnerCombatant != null && owner.OwnerCombatant.IsStimActive
                ? $"효과 {owner.OwnerCombatant.StimRemainingSeconds:0.0}s"
                : string.Empty;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            float newDurationSeconds,
            float newMovementSpeedMultiplier,
            float newFireIntervalMultiplier)
        {
            durationSeconds = newDurationSeconds;
            movementSpeedMultiplier = newMovementSpeedMultiplier;
            fireIntervalMultiplier = newFireIntervalMultiplier;
        }
#endif
    }
}
