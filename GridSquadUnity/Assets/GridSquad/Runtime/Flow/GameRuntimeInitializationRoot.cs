using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GridSquad
{
    public static class GameRuntimeInitializationRoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterLoadedSceneInitialization()
        {
            FeelConvenienceRuntimeBootstrap.InitializeRuntime();
            SceneManager.sceneLoaded -= InitializeLoadedScene;
            SceneManager.sceneLoaded += InitializeLoadedScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            InitializeLoadedScene(activeScene, LoadSceneMode.Single);
        }

        private static void InitializeLoadedScene(Scene scene, LoadSceneMode loadMode)
        {
            try
            {
                InitializeLoadingScreen(scene);
                PrototypeGameApplication application =
                    PrototypeGameApplication.Instance
                    ?? UnityEngine.Object.FindFirstObjectByType<PrototypeGameApplication>();
                if (application != null)
                {
                    application.InitializeApplication();
                    application.InitializeLoadedBaseScene(scene);
                    InitializeEntryScene(scene, application);
                    if (FindComponentInScene<CombatDirector>(scene) != null)
                    {
                        CombatSceneRuntimeInitializer
                            .SuspendCombatSceneUntilMissionConfiguration(scene);
                    }
                    return;
                }

                if (FindComponentInScene<CombatDirector>(scene) != null)
                    CombatSceneRuntimeInitializer.InitializeStandaloneCombatScene(scene);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[런타임 초기화] {scene.name} 씬 초기화에 실패했습니다.\n{exception}");
            }
        }

        private static void InitializeEntryScene(
            Scene scene,
            PrototypeGameApplication application)
        {
            EntrySceneController entry = FindComponentInScene<EntrySceneController>(scene);
            entry?.InitializeRuntime(application);
        }

        private static void InitializeLoadingScreen(Scene scene)
        {
            GridSquadSceneLoadingManager sceneLoader =
                FindComponentInScene<GridSquadSceneLoadingManager>(scene);
            sceneLoader?.InitializeRuntime();
            LoadingScreenPresentationController loadingScreen =
                FindComponentInScene<LoadingScreenPresentationController>(scene);
            loadingScreen?.InitializeRuntime();
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (T component in components)
            {
                if (component != null && component.gameObject.scene == scene)
                    return component;
            }
            return null;
        }
    }
}
