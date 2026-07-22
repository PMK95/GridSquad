using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapons/Hitscan Attack", fileName = "HitscanAttack")]
    public sealed class HitscanWeaponAttackBehaviorDefinition : WeaponAttackBehaviorDefinition
    {
        public override WeaponAttackMode Mode => WeaponAttackMode.Hitscan;
    }
}
