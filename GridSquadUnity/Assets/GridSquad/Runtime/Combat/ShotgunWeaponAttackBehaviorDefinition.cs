using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapons/Shotgun Attack", fileName = "ShotgunAttack")]
    public sealed class ShotgunWeaponAttackBehaviorDefinition : WeaponAttackBehaviorDefinition
    {
        [SerializeField, Min(1)] private int pelletCount = 5;
        [SerializeField, Min(1)] private int damagePerPellet = 9;
        [SerializeField, Range(0f, 45f)] private float spreadDegrees = 14f;

        public override WeaponAttackMode Mode => WeaponAttackMode.Shotgun;
        public int PelletCount => pelletCount;
        public int DamagePerPellet => damagePerPellet;
        public float SpreadDegrees => spreadDegrees;
        public override int CalculateDamage(WeaponDefinition weapon) => pelletCount * damagePerPellet;
    }
}
