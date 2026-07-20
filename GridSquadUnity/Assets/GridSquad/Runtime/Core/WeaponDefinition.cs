using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Min(1)] public int Damage = 25;
        [Min(0.05f)] public float AimDuration = 0.6f;
        [Min(0.1f)] public float FireInterval = 1f;
        [Min(1f)] public float RangeInCells = 10f;
        [Range(0f, 100f)] public float BaseHitChancePercent = 75f;
    }
}
