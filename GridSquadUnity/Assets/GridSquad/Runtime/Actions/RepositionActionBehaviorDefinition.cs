using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Actions/Reposition Behavior", fileName = "RepositionBehavior")]
    public sealed class RepositionActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.GridCell;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.ChangesPosition
            | CombatActionCapabilityFlags.AllowedWithForcedTarget
            | CombatActionCapabilityFlags.MovementCommand;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new RepositionCandidateProvider(owner, runtime),
                new RepositionExecutor(owner, runtime));
    }
}
