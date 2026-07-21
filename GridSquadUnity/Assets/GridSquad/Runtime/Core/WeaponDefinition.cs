using UnityEngine;
using UnityEngine.Serialization;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("표시 및 외형")]
        public string DisplayName = "무기";
        public WeaponPresentation PresentationPrefab;

        [Header("전투 수치")]
        [Min(1)] public int Damage = 25;
        [FormerlySerializedAs("AimDuration")]
        [Min(0.05f)] public float AimEnterDuration = 0.6f;
        [FormerlySerializedAs("FireInterval")]
        [Min(0.1f)] public float AimedShotInterval = 1f;
        [Min(1)] public int MagazineCapacity = 5;
        [Min(0)] public int StartingReserveAmmo = 20;
        [Min(0.05f)] public float ReloadDuration = 1.6f;
        [Min(1f)] public float RangeInCells = 10f;
        [Range(0f, 100f)] public float BaseHitChancePercent = 75f;
    }
}
