using MoreMountains.Tools;

namespace GridSquad
{
    public sealed class GridSquadSceneLoadingManager : MMAdditiveSceneLoadingManager
    {
        private bool runtimeInitialized;

        protected override void Awake()
        {
            // 프로젝트 초기화 루트가 InitializeRuntime을 명시적으로 호출한다.
        }

        public void InitializeRuntime()
        {
            if (runtimeInitialized)
                return;
            runtimeInitialized = true;
            FaderID = 0;
            SetRealtimeProgressValue ??= new ProgressEvent();
            SetInterpolatedProgressValue ??= new ProgressEvent();
            OnLoadStarted ??= new UnityEngine.Events.UnityEvent();
            OnBeforeEntryFade ??= new UnityEngine.Events.UnityEvent();
            OnEntryFade ??= new UnityEngine.Events.UnityEvent();
            OnAfterEntryFade ??= new UnityEngine.Events.UnityEvent();
            OnUnloadOriginScene ??= new UnityEngine.Events.UnityEvent();
            OnLoadDestinationScene ??= new UnityEngine.Events.UnityEvent();
            OnLoadProgressComplete ??= new UnityEngine.Events.UnityEvent();
            OnInterpolatedLoadProgressComplete ??= new UnityEngine.Events.UnityEvent();
            OnBeforeSceneActivation ??= new UnityEngine.Events.UnityEvent();
            OnAfterSceneActivation ??= new UnityEngine.Events.UnityEvent();
            OnExitFade ??= new UnityEngine.Events.UnityEvent();
            OnDestinationSceneActivation ??= new UnityEngine.Events.UnityEvent();
            OnUnloadSceneLoader ??= new UnityEngine.Events.UnityEvent();
            base.Initialization();
        }
    }
}
