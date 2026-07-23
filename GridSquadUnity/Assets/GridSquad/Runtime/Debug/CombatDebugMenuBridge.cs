using MoreMountains.Tools;
using UnityEngine;

namespace GridSquad
{
    public sealed class CombatDebugMenuBridge : MonoBehaviour
    {
        public const string DebugVisibleEvent = "Combat.DebugVisible";
        public const string FullAutoEvent = "Combat.FullAuto";
        public const string AutomaticPeekEvent = "Combat.AutomaticPeek";
        public const string PauseEvent = "Combat.Pause";
        public const string GameSpeedEvent = "Combat.GameSpeed";
        public const string RestartEvent = "Combat.Restart";

        [SerializeField] private CombatDirector director;
        [SerializeField] private TacticalInputController inputController;
        private UnitTacticalBehaviorController[] behaviorControllers;
        private bool runtimeInitialized;

        public void InitializeRuntime(
            CombatDirector newDirector,
            TacticalInputController newInputController,
            UnitTacticalBehaviorController[] newBehaviorControllers)
        {
            director = newDirector != null ? newDirector : director;
            inputController = newInputController != null
                ? newInputController
                : inputController;
            behaviorControllers = newBehaviorControllers
                ?? behaviorControllers
                ?? System.Array.Empty<UnitTacticalBehaviorController>();
            runtimeInitialized = true;
            SynchronizeMenuControls();
        }

        private void OnEnable()
        {
            MMDebugMenuCheckboxEvent.Register(HandleCheckboxChanged);
            MMDebugMenuSliderEvent.Register(HandleSliderChanged);
            MMDebugMenuButtonEvent.Register(HandleButtonPressed);
        }

        private void OnDisable()
        {
            MMDebugMenuCheckboxEvent.Unregister(HandleCheckboxChanged);
            MMDebugMenuSliderEvent.Unregister(HandleSliderChanged);
            MMDebugMenuButtonEvent.Unregister(HandleButtonPressed);
        }

        private void SynchronizeMenuControls()
        {
            if (!runtimeInitialized)
                return;
            if (director != null)
            {
                MMDebugMenuCheckboxEvent.Trigger(
                    DebugVisibleEvent,
                    director.DebugVisible,
                    MMDebugMenuCheckboxEvent.EventModes.SetCheckbox);
                MMDebugMenuCheckboxEvent.Trigger(
                    FullAutoEvent,
                    director.AllyFullAutoEnabled,
                    MMDebugMenuCheckboxEvent.EventModes.SetCheckbox);
            }

            bool automaticPeekEnabled = behaviorControllers != null
                && behaviorControllers.Length > 0
                && behaviorControllers[0] != null
                && behaviorControllers[0].AutomaticPeekAllowed;
            MMDebugMenuCheckboxEvent.Trigger(
                AutomaticPeekEvent,
                automaticPeekEnabled,
                MMDebugMenuCheckboxEvent.EventModes.SetCheckbox);

            if (inputController == null)
                return;
            MMDebugMenuCheckboxEvent.Trigger(
                PauseEvent,
                inputController.Paused,
                MMDebugMenuCheckboxEvent.EventModes.SetCheckbox);
            MMDebugMenuSliderEvent.Trigger(
                GameSpeedEvent,
                inputController.ActiveTimeScale,
                MMDebugMenuSliderEvent.EventModes.SetSlider);
        }

        private void HandleCheckboxChanged(
            string eventName,
            bool value,
            MMDebugMenuCheckboxEvent.EventModes eventMode)
        {
            if (eventMode != MMDebugMenuCheckboxEvent.EventModes.FromCheckbox)
                return;

            switch (eventName)
            {
                case DebugVisibleEvent:
                    director?.SetDebugVisible(value);
                    break;
                case FullAutoEvent:
                    director?.SetAllyFullAutoEnabled(value);
                    break;
                case AutomaticPeekEvent:
                    SetAutomaticPeekForAllUnits(value);
                    break;
                case PauseEvent:
                    inputController?.SetPauseFromDebugMenu(value);
                    break;
            }
        }

        private void HandleSliderChanged(
            string eventName,
            float value,
            MMDebugMenuSliderEvent.EventModes eventMode)
        {
            if (eventMode != MMDebugMenuSliderEvent.EventModes.FromSlider
                || eventName != GameSpeedEvent)
            {
                return;
            }

            if (inputController == null)
                return;

            float appliedSpeed = inputController.SetGameSpeedFromDebugMenu(value);
            MMDebugMenuSliderEvent.Trigger(
                GameSpeedEvent,
                appliedSpeed,
                MMDebugMenuSliderEvent.EventModes.SetSlider);
        }

        private void HandleButtonPressed(
            string eventName,
            bool active,
            MMDebugMenuButtonEvent.EventModes eventMode)
        {
            if (eventMode == MMDebugMenuButtonEvent.EventModes.FromButton
                && active
                && eventName == RestartEvent)
            {
                inputController?.RestartCombatFromDebugMenu();
            }
        }

        private void SetAutomaticPeekForAllUnits(bool enabled)
        {
            if (behaviorControllers == null)
                return;
            foreach (UnitTacticalBehaviorController controller in behaviorControllers)
                controller?.SetAutomaticPeekAllowed(enabled);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            CombatDirector newDirector,
            TacticalInputController newInputController)
        {
            director = newDirector;
            inputController = newInputController;
        }
#endif
    }
}
