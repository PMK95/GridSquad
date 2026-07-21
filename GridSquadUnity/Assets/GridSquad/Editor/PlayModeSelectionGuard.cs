using System;
using UnityEditor;
using UnityEngine;

namespace GridSquadEditor
{
    [InitializeOnLoad]
    public static class PlayModeSelectionGuard
    {
        static PlayModeSelectionGuard()
        {
            EditorApplication.playModeStateChanged -= ClearSceneSelectionBeforeEnteringPlayMode;
            EditorApplication.playModeStateChanged += ClearSceneSelectionBeforeEnteringPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= ClearSceneSelectionBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += ClearSceneSelectionBeforeAssemblyReload;
        }

        private static void ClearSceneSelectionBeforeEnteringPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                ClearSelectionWhenSceneObjectIsSelected();
        }

        private static void ClearSceneSelectionBeforeAssemblyReload()
        {
            ClearSelectionWhenSceneObjectIsSelected();
        }

        private static void ClearSelectionWhenSceneObjectIsSelected()
        {
            foreach (UnityEngine.Object selectedObject in Selection.objects)
            {
                if (selectedObject is GameObject || selectedObject is Component)
                {
                    Selection.objects = Array.Empty<UnityEngine.Object>();
                    return;
                }
            }
        }
    }
}
