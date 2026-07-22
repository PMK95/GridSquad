using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapons/Melee Attack", fileName = "MeleeAttack")]
    public sealed class MeleeWeaponAttackBehaviorDefinition : WeaponAttackBehaviorDefinition
    {
        [SerializeField, Range(0f, 1f)] private float impactNormalizedTime = 0.55f;
        public override WeaponAttackMode Mode => WeaponAttackMode.Melee;
        public override bool UsesAmmo => false;
        public float ImpactNormalizedTime => impactNormalizedTime;
    }
}
