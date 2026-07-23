using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class EntrySceneController : MonoBehaviour
    {
        private bool runtimeInitialized;

        public void InitializeRuntime(PrototypeGameApplication application)
        {
            if (runtimeInitialized)
                return;
            runtimeInitialized = true;

            if (application == null)
            {
                Debug.LogError("[엔트리] 게임 애플리케이션을 찾지 못했습니다.", this);
                return;
            }
            if (!application.TryLoadInitialBaseScene(out string failureReason))
                Debug.LogError($"[엔트리] 기지 씬 이동에 실패했습니다: {failureReason}", this);
        }
    }
}
