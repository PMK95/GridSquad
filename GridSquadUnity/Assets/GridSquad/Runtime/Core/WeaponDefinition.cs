using UnityEngine;
using UnityEngine.Serialization;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : EquippableDefinition
    {
        public override EquipmentCategory Category => EquipmentCategory.Weapon;
        [SerializeField] private WeaponHandedness handedness = WeaponHandedness.TwoHanded;
        public WeaponHandedness Handedness => handedness;

        [Header("표시 및 외형")]
        public WeaponPresentation PresentationPrefab;
        [SerializeField] private WeaponAttackBehaviorDefinition attackBehavior;
        [SerializeField] private WeaponHitEffectDefinition[] hitEffects = System.Array.Empty<WeaponHitEffectDefinition>();

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
        [Header("트라우마")]
        [SerializeField, Min(0f)] private float traumaMultiplier = 1f;
        [SerializeField, Min(0f)] private float fixedTrauma;

        public WeaponAttackBehaviorDefinition AttackBehavior => attackBehavior;
        public System.Collections.Generic.IReadOnlyList<WeaponHitEffectDefinition> HitEffects => hitEffects;
        public bool UsesAmmo => attackBehavior == null || attackBehavior.UsesAmmo;
        public int AttackDamage => attackBehavior != null ? attackBehavior.CalculateDamage(this) : Damage;
        public float TraumaMultiplier => Mathf.Max(0f, traumaMultiplier);
        public float FixedTrauma => Mathf.Max(0f, fixedTrauma);

        public void ApplyHitEffects(Combatant attacker, ShootableTarget target)
        {
            WeaponHitContext context = new(attacker, target, this);
            foreach (WeaponHitEffectDefinition effect in hitEffects)
                effect?.Apply(context);
        }

#if UNITY_EDITOR
        public void SetEditorHandedness(WeaponHandedness newHandedness)
            => handedness = newHandedness;

        public void SetEditorAttackConfiguration(
            WeaponAttackBehaviorDefinition newAttackBehavior,
            WeaponHitEffectDefinition[] newHitEffects)
        {
            attackBehavior = newAttackBehavior;
            hitEffects = newHitEffects ?? System.Array.Empty<WeaponHitEffectDefinition>();
        }

        public void SetEditorTraumaConfiguration(
            float newTraumaMultiplier,
            float newFixedTrauma)
        {
            traumaMultiplier = Mathf.Max(0f, newTraumaMultiplier);
            fixedTrauma = Mathf.Max(0f, newFixedTrauma);
        }
#endif
    }
}
