using UnityEngine;

public class Flicker : MonoBehaviour
{
    [SerializeField] Renderer[] renderers;
    [SerializeField,ColorUsage(true,true)] Color emissionColor = Color.white;
}
