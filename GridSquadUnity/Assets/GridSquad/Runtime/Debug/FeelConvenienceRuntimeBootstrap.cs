using MoreMountains.Tools;
using UnityEngine;

namespace GridSquad
{
    public sealed class FeelConvenienceRuntimeBootstrap : MonoBehaviour
    {
        public static void InitializeRuntime()
        {
            MMSaveLoadManager.SaveLoadMethod = new MMSaveLoadManagerMethodJson();
        }
    }
}
