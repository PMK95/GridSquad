using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(TacticalEntity),
        typeof(EntityHealth),
        typeof(ShootableTarget))]
    public sealed class DestructibleCover : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumHealth = 120;
        [SerializeField] private Collider coverCollider;
        [SerializeField] private Renderer coverRenderer;
        [SerializeField] private TacticalEntity entity;
        [SerializeField] private EntityHealth health;
        [SerializeField] private ShootableTarget shootableTarget;

        private GridMap gridMap;
        private GridCoordinate currentCell;
        private bool registered;
        private MaterialPropertyBlock damageFlashProperties;
        private float damageFlashEndTime;
        private bool runtimeInitialized;

        public TacticalEntity Entity => entity;
        public EntityHealth Health => health;
        public ShootableTarget ShootableTarget => shootableTarget;
        public GridCoordinate CurrentCell => currentCell;
        public bool IsAlive => health != null && health.IsAlive;

        private void Awake()
        {
            EnsureAbilityComponents();
            if (coverCollider == null)
                coverCollider = GetComponent<Collider>();
            if (coverRenderer == null)
                coverRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (damageFlashEndTime <= 0f || Time.time < damageFlashEndTime)
                return;

            damageFlashEndTime = 0f;
            coverRenderer?.SetPropertyBlock(null);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.DamageApplied -= HandleDamageApplied;
                health.HealthDepleted -= HandleHealthDepleted;
            }
            if (registered && gridMap != null)
                gridMap.UnregisterDestructibleCover(this, currentCell, true);
        }

        public void ConfigureRuntime(GridMap newGridMap)
        {
            EnsureAbilityComponents();
            if (coverCollider == null)
                coverCollider = GetComponent<Collider>();
            if (coverRenderer == null)
                coverRenderer = GetComponent<Renderer>();
            gridMap = newGridMap;
            currentCell = gridMap != null ? gridMap.WorldToGrid(transform.position) : default;
            entity.ConfigureRuntime("엄폐물", null, currentCell);
            health.Initialize(maximumHealth, true);
            shootableTarget.ConfigureRuntime(
                entity,
                health,
                null,
                transform,
                coverCollider != null ? coverCollider : GetComponent<Collider>(),
                true);
            if (!runtimeInitialized)
            {
                health.DamageApplied += HandleDamageApplied;
                health.HealthDepleted += HandleHealthDepleted;
                runtimeInitialized = true;
            }
            if (Application.isPlaying)
                RegisterWithGridMap();
        }

        private void EnsureAbilityComponents()
        {
            entity = entity != null ? entity : GetComponent<TacticalEntity>();
            if (entity == null)
                entity = gameObject.AddComponent<TacticalEntity>();
            health = health != null ? health : GetComponent<EntityHealth>();
            if (health == null)
                health = gameObject.AddComponent<EntityHealth>();
            shootableTarget = shootableTarget != null
                ? shootableTarget
                : GetComponent<ShootableTarget>();
            if (shootableTarget == null)
                shootableTarget = gameObject.AddComponent<ShootableTarget>();
        }

        private void RegisterWithGridMap()
        {
            if (registered || gridMap == null)
                return;

            currentCell = gridMap.WorldToGrid(transform.position);
            entity.SetCurrentCell(currentCell);
            gridMap.RegisterDestructibleCover(this, currentCell);
            registered = true;
        }

        private void HandleDamageApplied(EntityHealth source, int appliedDamage)
        {
            PlayDamageFlash();
        }

        private void HandleHealthDepleted(EntityHealth source)
        {
            if (gridMap != null)
                gridMap.UnregisterDestructibleCover(this, currentCell, true);
            registered = false;
            entity.MarkUnavailable();
            if (coverCollider != null)
                coverCollider.enabled = false;
            if (coverRenderer != null)
                coverRenderer.enabled = false;
            Destroy(gameObject);
        }

        private void PlayDamageFlash()
        {
            if (coverRenderer == null)
                return;

            damageFlashProperties ??= new MaterialPropertyBlock();
            damageFlashProperties.Clear();
            Material material = coverRenderer.sharedMaterial;
            if (material != null && material.HasProperty("_BaseColor"))
                damageFlashProperties.SetColor("_BaseColor", Color.white);
            if (material != null && material.HasProperty("_Color"))
                damageFlashProperties.SetColor("_Color", Color.white);
            coverRenderer.SetPropertyBlock(damageFlashProperties);
            damageFlashEndTime = Time.time + 0.08f;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            int newMaximumHealth,
            Collider newCoverCollider,
            Renderer newCoverRenderer)
        {
            maximumHealth = Mathf.Max(1, newMaximumHealth);
            coverCollider = newCoverCollider;
            coverRenderer = newCoverRenderer;
            EnsureAbilityComponents();
            entity.SetEditorConfiguration("엄폐물", true, null);
            health.SetEditorMaximumHealth(maximumHealth);
            shootableTarget.SetEditorConfiguration(transform, coverCollider, true);
        }
#endif
    }
}
