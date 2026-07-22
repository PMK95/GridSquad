using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapons/Effects/Stun", fileName = "StunHitEffect")]
    public sealed class StunWeaponHitEffectDefinition : WeaponHitEffectDefinition
    {
        [SerializeField, Range(0f, 100f)] private float chancePercent = 50f;
        [SerializeField, Min(0.1f)] private float durationSeconds = 1.5f;

        public override void Apply(WeaponHitContext context)
        {
            Combatant target = context.Target != null ? context.Target.Entity?.Combatant : null;
            if (target != null && Random.value < chancePercent / 100f)
                target.ApplyStun(durationSeconds);
        }
    }
}
