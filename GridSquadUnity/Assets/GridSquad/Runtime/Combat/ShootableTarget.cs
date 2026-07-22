using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalEntity), typeof(EntityHealth))]
    public sealed class ShootableTarget : MonoBehaviour
    {
        [SerializeField] private Transform aimCenter;
        [SerializeField] private Collider hitCollider;
        [SerializeField] private bool cover;

        private TacticalEntity entity;
        private EntityHealth health;
        private Combatant combatant;

        public string DisplayName => entity != null ? entity.DisplayName : name;
        public bool IsAlive => health != null && health.IsAlive;
        public int CurrentHealth => health != null ? health.CurrentHealth : 0;
        public int MaximumHealth => health != null ? health.MaximumHealth : 0;
        public GridCoordinate CurrentCell => entity != null ? entity.CurrentCell : default;
        public GridCoordinate CurrentExposureCell => combatant != null
            ? combatant.CurrentExposureCell
            : CurrentCell;
        public Vector3 CurrentExposureCenter => combatant != null
            ? combatant.CurrentExposureCenter
            : hitCollider != null
                ? hitCollider.bounds.center
                : transform.position;
        public Transform AimCenterTransform => aimCenter != null ? aimCenter : transform;
        public float AccidentalHitRadiusWorld => CalculateHorizontalColliderRadius(
            hitCollider,
            combatant != null && combatant.GridMap != null
                ? combatant.GridMap.CellSize * 0.25f
                : 0.5f);
        public Team? TargetTeam => entity != null ? entity.TargetTeam : null;
        public bool IsCover => cover;
        public TacticalEntity Entity => entity;

        private void Awake()
        {
            entity = GetComponent<TacticalEntity>();
            health = GetComponent<EntityHealth>();
            combatant = GetComponent<Combatant>();
            if (hitCollider == null)
                hitCollider = GetComponentInChildren<Collider>();
        }

        public void ConfigureRuntime(
            TacticalEntity newEntity,
            EntityHealth newHealth,
            Combatant newCombatant,
            Transform newAimCenter,
            Collider newHitCollider,
            bool isCover)
        {
            entity = newEntity != null ? newEntity : GetComponent<TacticalEntity>();
            health = newHealth != null ? newHealth : GetComponent<EntityHealth>();
            combatant = newCombatant;
            aimCenter = newAimCenter;
            hitCollider = newHitCollider;
            cover = isCover;
        }

        public void ApplyDamage(int damage)
        {
            if (combatant != null)
                combatant.ApplyDamage(new CombatDamageRequest(null, null, damage));
            else
                health?.ApplyDamage(damage);
        }

        public CombatDamageResult ApplyDamage(CombatDamageRequest request)
        {
            if (combatant != null)
                return combatant.ApplyDamage(request);
            int applied = health != null ? health.ApplyDamage(request.Damage) : 0;
            return new CombatDamageResult(request.Damage, applied, false);
        }

        public void PlayMissFeedback()
        {
            combatant?.PlayMissFeedback();
        }

        private static float CalculateHorizontalColliderRadius(
            Collider targetCollider,
            float fallbackRadius)
        {
            if (targetCollider == null)
                return Mathf.Max(0.05f, fallbackRadius);

            Vector3 extents = targetCollider.bounds.extents;
            return Mathf.Max(0.05f, Mathf.Max(extents.x, extents.z));
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            Transform newAimCenter,
            Collider newHitCollider,
            bool isCover)
        {
            aimCenter = newAimCenter;
            hitCollider = newHitCollider;
            cover = isCover;
        }
#endif
    }
}
