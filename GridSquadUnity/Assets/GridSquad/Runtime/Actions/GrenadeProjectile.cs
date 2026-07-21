using UnityEngine;

namespace GridSquad
{
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        private CombatDirector director;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private GridCoordinate targetCell;
        private float travelSeconds;
        private float elapsedSeconds;
        private int radiusCells;
        private int damage;

        public void Initialize(
            CombatDirector newDirector,
            Vector3 start,
            Vector3 target,
            GridCoordinate newTargetCell,
            float newTravelSeconds,
            int newRadiusCells,
            int newDamage)
        {
            director = newDirector;
            startPosition = start;
            targetPosition = target;
            targetCell = newTargetCell;
            travelSeconds = Mathf.Max(0.05f, newTravelSeconds);
            radiusCells = Mathf.Max(0, newRadiusCells);
            damage = Mathf.Max(0, newDamage);
            transform.position = startPosition;
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedSeconds / travelSeconds);
            Vector3 position = Vector3.Lerp(startPosition, targetPosition, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * 2f;
            transform.position = position;
            transform.Rotate(360f * Time.deltaTime, 540f * Time.deltaTime, 0f, Space.Self);

            if (progress >= 1f)
                Explode();
        }

        private void Explode()
        {
            if (director != null)
            {
                foreach (Combatant combatant in director.Combatants)
                {
                    if (combatant == null || !combatant.IsAlive)
                        continue;
                    int xDistance = Mathf.Abs(combatant.CurrentCell.X - targetCell.X);
                    int zDistance = Mathf.Abs(combatant.CurrentCell.Z - targetCell.Z);
                    if (Mathf.Max(xDistance, zDistance) <= radiusCells)
                        combatant.ApplyDamage(damage);
                }
            }

            CreateExplosionVisual();
            Destroy(gameObject);
        }

        private void CreateExplosionVisual()
        {
            GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = "GrenadeExplosion";
            Collider effectCollider = effect.GetComponent<Collider>();
            if (effectCollider != null)
                Destroy(effectCollider);
            effect.transform.position = targetPosition + Vector3.up * 0.35f;
            effect.transform.localScale = Vector3.one * 0.2f;
            Renderer effectRenderer = effect.GetComponent<Renderer>();
            if (effectRenderer != null)
                effectRenderer.material.color = new Color(1f, 0.25f, 0.05f, 0.85f);
            GrenadeExplosionVisual visual = effect.AddComponent<GrenadeExplosionVisual>();
            visual.Initialize(Mathf.Max(1.5f, radiusCells * 2f), 0.3f);

            GameObject particleObject = new("GrenadeExplosionParticles", typeof(ParticleSystem));
            particleObject.transform.position = targetPosition + Vector3.up * 0.25f;
            ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.75f, 0.12f),
                new Color(1f, 0.08f, 0.01f));
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            particles.Play();
        }
    }

    public sealed class GrenadeExplosionVisual : MonoBehaviour
    {
        private float maximumScale;
        private float duration;
        private float elapsed;

        public void Initialize(float newMaximumScale, float newDuration)
        {
            maximumScale = newMaximumScale;
            duration = Mathf.Max(0.05f, newDuration);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.2f, maximumScale, progress);
            Renderer effectRenderer = GetComponent<Renderer>();
            if (effectRenderer != null)
            {
                Color color = effectRenderer.material.color;
                color.a = 1f - progress;
                effectRenderer.material.color = color;
            }
            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}
