using System;
using System.IO;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class GameSceneTransitionManager : MonoBehaviour
    {
        [SerializeField] private string loadingSceneName = "GridSquadLoading";

        private string requestedScenePath;
        private bool runtimeInitialized;
        private bool transitionPending;

        public event Action<Scene> DestinationSceneLoaded;
        public bool TransitionPending => transitionPending;

        public void InitializeRuntime()
        {
            if (runtimeInitialized)
                return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            runtimeInitialized = true;
        }

        public bool TryLoadScene(
            string scenePath,
            bool preserveActiveScene,
            out string failureReason)
        {
            InitializeRuntime();
            if (transitionPending)
            {
                failureReason = "이미 다른 씬으로 이동하고 있습니다.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                failureReason = "이동할 씬 경로가 비어 있습니다.";
                return false;
            }

            string destinationName = Path.GetFileNameWithoutExtension(scenePath);
            if (string.IsNullOrWhiteSpace(destinationName))
            {
                failureReason = $"씬 이름을 확인할 수 없습니다: {scenePath}";
                return false;
            }

            try
            {
                Time.timeScale = 1f;
                requestedScenePath = scenePath;
                transitionPending = true;
                MMAdditiveSceneLoadingManager.LoadScene(
                    destinationName,
                    loadingSceneName,
                    unloadMethod: preserveActiveScene
                        ? MMAdditiveSceneLoadingManagerSettings.UnloadMethods.None
                        : MMAdditiveSceneLoadingManagerSettings.UnloadMethods.ActiveScene);
                failureReason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ClearPendingTransition();
                failureReason = exception.Message;
                return false;
            }
        }

        public void CancelPendingTransition()
            => ClearPendingTransition();

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (!transitionPending || !MatchesRequestedScene(scene))
                return;

            ClearPendingTransition();
            DestinationSceneLoaded?.Invoke(scene);
        }

        private bool MatchesRequestedScene(Scene scene)
            => string.Equals(scene.path, requestedScenePath, StringComparison.Ordinal)
               || string.Equals(
                   scene.name,
                   Path.GetFileNameWithoutExtension(requestedScenePath),
                   StringComparison.Ordinal);

        private void ClearPendingTransition()
        {
            requestedScenePath = string.Empty;
            transitionPending = false;
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(string newLoadingSceneName)
        {
            loadingSceneName = newLoadingSceneName;
        }
#endif
    }
}
