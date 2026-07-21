using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Flicker : MonoBehaviour
{
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer[] renderers;
    [SerializeField, ColorUsage(true, true)] private Color emissionColor = Color.white;
    [SerializeField, Min(0.01f)] private float duration = 0.1f;

    private readonly List<MaterialState> materialStates = new();
    private Coroutine activeFlicker;
    private bool initialized;

    private sealed class MaterialState
    {
        public Material Material;
        public Color OriginalEmissionColor;
        public bool EmissionKeywordEnabled;
    }

    private void Awake()
    {
        InitializeRuntimeMaterials();
    }

    public void Play()
    {
        InitializeRuntimeMaterials();
        if (materialStates.Count == 0)
            return;

        if (activeFlicker != null)
            StopCoroutine(activeFlicker);

        SetEmissionColor(emissionColor);
        activeFlicker = StartCoroutine(RestoreEmissionAfterDuration());
    }

    private void OnDisable()
    {
        if (activeFlicker != null)
        {
            StopCoroutine(activeFlicker);
            activeFlicker = null;
        }
        RestoreEmissionColors();
    }

    private void OnDestroy()
    {
        RestoreEmissionColors();
        foreach (MaterialState state in materialStates)
        {
            if (state.Material != null)
                Destroy(state.Material);
        }
        materialStates.Clear();
    }

    private void InitializeRuntimeMaterials()
    {
        if (initialized)
            return;
        initialized = true;

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        HashSet<Material> registeredMaterials = new();
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            foreach (Material material in targetRenderer.materials)
            {
                if (material == null
                    || !material.HasProperty(EmissionColorPropertyId)
                    || !registeredMaterials.Add(material))
                {
                    continue;
                }

                materialStates.Add(new MaterialState
                {
                    Material = material,
                    OriginalEmissionColor = material.GetColor(EmissionColorPropertyId),
                    EmissionKeywordEnabled = material.IsKeywordEnabled("_EMISSION")
                });
            }
        }
    }

    private IEnumerator RestoreEmissionAfterDuration()
    {
        yield return new WaitForSeconds(duration);
        RestoreEmissionColors();
        activeFlicker = null;
    }

    private void SetEmissionColor(Color color)
    {
        foreach (MaterialState state in materialStates)
        {
            if (state.Material == null)
                continue;
            state.Material.EnableKeyword("_EMISSION");
            state.Material.SetColor(EmissionColorPropertyId, color);
        }
    }

    private void RestoreEmissionColors()
    {
        foreach (MaterialState state in materialStates)
        {
            if (state.Material == null)
                continue;

            state.Material.SetColor(EmissionColorPropertyId, state.OriginalEmissionColor);
            if (!state.EmissionKeywordEnabled)
                state.Material.DisableKeyword("_EMISSION");
        }
    }
}
