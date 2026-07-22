using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Actions/Basic Attack Behavior", fileName = "BasicAttackBehavior")]
    public sealed class BasicAttackActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.None;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.DefaultAttack
            | CombatActionCapabilityFlags.AllowedWithForcedTarget;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new BasicAttackCandidateProvider(owner, runtime),
                new BasicAttackExecutor(owner, runtime));
    }
}
