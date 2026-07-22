using GridSquad;
using UnityEditor;
using UnityEngine;

namespace GridSquadEditor
{
    [InitializeOnLoad]
    public static class DestructibleCoverPrefabFactory
    {
        public const string PrefabPath = "Assets/GridSquad/Prefabs/Environment/Cover.prefab";
        private const string CoverMaterialPath = "Assets/GridSquad/Materials/Environment/Cover.mat";
        private const string SelectionMaterialPath = "Assets/GridSquad/Materials/Environment/CoverSelection.mat";
        private const int CoverLayer = 10;
        private const int DefaultMaximumHealth = 120;

        static DestructibleCoverPrefabFactory()
        {
            EditorApplication.delayCall += EnsureDefaultCoverPrefab;
        }

        public static GameObject InstantiateCover(
            Transform parent,
            string instanceName,
            Vector3 position,
            Vector3 scale,
            Material coverMaterial,
            string undoName = null)
        {
            GameObject prefab = LoadOrCreateCoverPrefab(coverMaterial);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            if (!string.IsNullOrEmpty(undoName))
                Undo.RegisterCreatedObjectUndo(instance, undoName);
            return instance;
        }

        public static GameObject LoadOrCreateCoverPrefab(Material coverMaterial)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                GameObject coverObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                coverObject.name = "Cover";
                coverObject.layer = CoverLayer;
                Renderer coverRenderer = coverObject.GetComponent<Renderer>();
                if (coverMaterial != null)
                    coverRenderer.sharedMaterial = coverMaterial;
                coverObject.AddComponent<DestructibleCover>();
                PrefabUtility.SaveAsPrefabAsset(coverObject, PrefabPath);
                Object.DestroyImmediate(coverObject);
            }

            EnsureCoverPrefabStructure(coverMaterial);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void EnsureCoverPrefabStructure(Material coverMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.layer = CoverLayer;
                Collider coverCollider = root.GetComponent<Collider>();
                Renderer coverRenderer = root.GetComponent<Renderer>();
                if (coverRenderer != null && coverMaterial != null)
                    coverRenderer.sharedMaterial = coverMaterial;

                TacticalEntity entity = GetOrAddComponent<TacticalEntity>(root);
                EntityHealth health = GetOrAddComponent<EntityHealth>(root);
                ShootableTarget shootableTarget = GetOrAddComponent<ShootableTarget>(root);
                DestructibleCover destructibleCover = GetOrAddComponent<DestructibleCover>(root);
                GameObject selectionVisual = EnsureSelectionVisual(root.transform);

                entity.SetEditorConfiguration("엄폐물", true, selectionVisual);
                health.SetEditorMaximumHealth(DefaultMaximumHealth);
                shootableTarget.SetEditorConfiguration(root.transform, coverCollider, true);
                destructibleCover.SetEditorConfiguration(
                    DefaultMaximumHealth,
                    coverCollider,
                    coverRenderer);
                entity.SetEditorConfiguration("엄폐물", true, selectionVisual);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject EnsureSelectionVisual(Transform parent)
        {
            Transform existing = parent.Find("SelectionIndicator");
            if (existing != null)
                return existing.gameObject;

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "SelectionIndicator";
            indicator.layer = CoverLayer;
            indicator.transform.SetParent(parent, false);
            indicator.transform.localPosition = new Vector3(0f, 0.54f, 0f);
            indicator.transform.localScale = new Vector3(0.62f, 0.025f, 0.62f);
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            Renderer renderer = indicator.GetComponent<Renderer>();
            renderer.sharedMaterial = LoadOrCreateSelectionMaterial();
            indicator.SetActive(false);
            return indicator;
        }

        private static Material LoadOrCreateSelectionMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SelectionMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            material = new Material(shader)
            {
                name = "CoverSelection",
                color = new Color(0.1f, 0.85f, 1f, 1f)
            };
            AssetDatabase.CreateAsset(material, SelectionMaterialPath);
            return material;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void EnsureDefaultCoverPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Material coverMaterial = AssetDatabase.LoadAssetAtPath<Material>(CoverMaterialPath);
            LoadOrCreateCoverPrefab(coverMaterial);
        }
    }
}
