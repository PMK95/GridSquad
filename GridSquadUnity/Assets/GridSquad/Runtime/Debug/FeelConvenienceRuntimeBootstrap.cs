using MoreMountains.Tools;
using UnityEngine;

namespace GridSquad
{
    [DefaultExecutionOrder(-1000)]
    public sealed class FeelConvenienceRuntimeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            MMSaveLoadManager.SaveLoadMethod = new MMSaveLoadManagerMethodJson();
        }
    }
}
