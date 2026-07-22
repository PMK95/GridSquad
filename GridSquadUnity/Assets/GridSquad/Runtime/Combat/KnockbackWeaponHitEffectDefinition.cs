using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapons/Effects/Knockback", fileName = "KnockbackHitEffect")]
    public sealed class KnockbackWeaponHitEffectDefinition : WeaponHitEffectDefinition
    {
        [SerializeField, Min(1)] private int distanceCells = 1;

        public override void Apply(WeaponHitContext context)
        {
            Combatant target = context.Target != null ? context.Target.Entity?.Combatant : null;
            if (target != null && context.Attacker != null)
                target.TryApplyKnockback(context.Attacker.CurrentCell, distanceCells);
        }
    }
}
