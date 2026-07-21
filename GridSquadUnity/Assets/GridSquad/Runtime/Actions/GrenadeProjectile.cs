using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using MoreMountains.Tools;
using UnityEngine;

namespace GridSquad
{
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        private CombatDirector director;
        private GridMap gridMap;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private GridCoordinate targetCell;
        private float travelSeconds;
        private float fuseSeconds;
        private float elapsedSeconds;
        private int radiusCells;
        private int damage;
        private float cameraShakeDuration;
        private float cameraShakeAmplitude;
        private float cameraShakeFrequency;
        private Mesh warningMesh;
        private Material warningMaterial;
        private GameObject warningObject;

        public void Initialize(
            CombatDirector newDirector,
            GridMap newGridMap,
            Vector3 start,
            Vector3 target,
            GridCoordinate newTargetCell,
            float newTravelSeconds,
            float newFuseSeconds,
            int newRadiusCells,
            int newDamage,
            float newCameraShakeDuration,
            float newCameraShakeAmplitude,
            float newCameraShakeFrequency)
        {
            director = newDirector;
            gridMap = newGridMap;
            startPosition = start;
            targetPosition = target;
            targetCell = newTargetCell;
            travelSeconds = Mathf.Max(0.05f, newTravelSeconds);
            fuseSeconds = Mathf.Max(travelSeconds, newFuseSeconds);
            radiusCells = Mathf.Max(0, newRadiusCells);
            damage = Mathf.Max(0, newDamage);
            cameraShakeDuration = Mathf.Max(0.01f, newCameraShakeDuration);
            cameraShakeAmplitude = Mathf.Max(0f, newCameraShakeAmplitude);
            cameraShakeFrequency = Mathf.Max(0f, newCameraShakeFrequency);
            transform.position = startPosition;
            CreateWarningArea();
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedSeconds / travelSeconds);
            if (progress < 1f)
            {
                Vector3 position = Vector3.Lerp(startPosition, targetPosition, progress);
                position.y += Mathf.Sin(progress * Mathf.PI) * 2f;
                transform.position = position;
                transform.Rotate(360f * Time.deltaTime, 540f * Time.deltaTime, 0f, Space.Self);
            }
            else
            {
                transform.position = targetPosition + Vector3.up * 0.12f;
            }

            UpdateWarningPulse();

            if (elapsedSeconds >= fuseSeconds)
                Explode();
        }

        private void CreateWarningArea()
        {
            if (gridMap == null)
                return;

            warningObject = new GameObject("GrenadeWarningArea", typeof(MeshFilter), typeof(MeshRenderer));
            warningMesh = new Mesh { name = "수류탄 폭발 예고 범위" };
            warningMesh.MarkDynamic();
            warningObject.GetComponent<MeshFilter>().sharedMesh = warningMesh;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            warningMaterial = new Material(shader)
            {
                name = "수류탄 폭발 예고 재질",
                color = new Color(1f, 0.05f, 0.02f, 0.42f)
            };
            if (warningMaterial.HasProperty("_BaseColor"))
                warningMaterial.SetColor("_BaseColor", warningMaterial.color);
            if (warningMaterial.HasProperty("_Surface"))
                warningMaterial.SetFloat("_Surface", 1f);
            if (warningMaterial.HasProperty("_ZWrite"))
                warningMaterial.SetFloat("_ZWrite", 0f);
            warningMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            warningMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            warningObject.GetComponent<MeshRenderer>().sharedMaterial = warningMaterial;

            int diameter = radiusCells * 2 + 1;
            int maximumCellCount = diameter * diameter;
            Vector3[] vertices = new Vector3[maximumCellCount * 4];
            int[] triangles = new int[maximumCellCount * 6];
            int cellCount = 0;
            float halfSize = gridMap.CellSize * 0.46f;
            for (int x = targetCell.X - radiusCells; x <= targetCell.X + radiusCells; x++)
            {
                for (int z = targetCell.Z - radiusCells; z <= targetCell.Z + radiusCells; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (!gridMap.IsInside(cell))
                        continue;
                    Vector3 center = gridMap.GridToWorld(cell) + Vector3.up * 0.09f;
                    int vertexIndex = cellCount * 4;
                    vertices[vertexIndex] = center + new Vector3(-halfSize, 0f, -halfSize);
                    vertices[vertexIndex + 1] = center + new Vector3(-halfSize, 0f, halfSize);
                    vertices[vertexIndex + 2] = center + new Vector3(halfSize, 0f, halfSize);
                    vertices[vertexIndex + 3] = center + new Vector3(halfSize, 0f, -halfSize);
                    int triangleIndex = cellCount * 6;
                    triangles[triangleIndex] = vertexIndex;
                    triangles[triangleIndex + 1] = vertexIndex + 1;
                    triangles[triangleIndex + 2] = vertexIndex + 2;
                    triangles[triangleIndex + 3] = vertexIndex;
                    triangles[triangleIndex + 4] = vertexIndex + 2;
                    triangles[triangleIndex + 5] = vertexIndex + 3;
                    cellCount++;
                }
            }
            if (cellCount < maximumCellCount)
            {
                System.Array.Resize(ref vertices, cellCount * 4);
                System.Array.Resize(ref triangles, cellCount * 6);
            }
            warningMesh.vertices = vertices;
            warningMesh.triangles = triangles;
            warningMesh.RecalculateBounds();
        }

        private void UpdateWarningPulse()
        {
            if (warningMaterial == null)
                return;
            float remainingRatio = 1f - Mathf.Clamp01(elapsedSeconds / fuseSeconds);
            float pulse = 0.35f + Mathf.PingPong(elapsedSeconds * 3f, 0.35f);
            Color color = new(1f, 0.04f + remainingRatio * 0.08f, 0.01f, pulse);
            warningMaterial.color = color;
            if (warningMaterial.HasProperty("_BaseColor"))
                warningMaterial.SetColor("_BaseColor", color);
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
            PlayExplosionCameraShake();
            DestroyWarningArea();
            Destroy(gameObject);
        }

        private void PlayExplosionCameraShake()
        {
            MMCinemachineCameraShaker cameraShaker =
                FindFirstObjectByType<MMCinemachineCameraShaker>();
            if (cameraShaker == null || cameraShakeAmplitude <= 0f)
                return;

            MMChannelData channelData = new(
                cameraShaker.ChannelMode,
                cameraShaker.Channel,
                cameraShaker.MMChannelDefinition);
            MMCameraShakeEvent.Trigger(
                cameraShakeDuration,
                cameraShakeAmplitude,
                cameraShakeFrequency,
                0f,
                0f,
                0f,
                false,
                channelData);
        }

        private void OnDestroy()
        {
            DestroyWarningArea();
        }

        private void DestroyWarningArea()
        {
            if (warningObject != null)
                Destroy(warningObject);
            if (warningMesh != null)
                Destroy(warningMesh);
            if (warningMaterial != null)
                Destroy(warningMaterial);
            warningObject = null;
            warningMesh = null;
            warningMaterial = null;
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
