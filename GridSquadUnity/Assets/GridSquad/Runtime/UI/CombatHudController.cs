using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class CombatHudController : MonoBehaviour
    {
        [SerializeField] private Text stateText;
        [SerializeField] private Text modeText;
        [SerializeField] private Text debugText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private Button allyFullAutoButton;
        [SerializeField] private Text allyFullAutoButtonText;
        [SerializeField] private CombatDirector director;
        [SerializeField] private MMF_Player selectionChangedFeedbacks;
        [SerializeField] private MMF_Player automaticModeChangedFeedbacks;
        [SerializeField] private MMF_Player resultPanelFeedbacks;

        private TacticalEntity selectedEntity;
        private bool automaticModeInitialized;
        private CombatControlMode previousControlMode;
        private float actionMessageEndTime;
        private Text commandMessageText;

        private static readonly Color CommandButtonColor =
            new(0.24f, 0.28f, 0.34f, 0.96f);
        private static readonly Color FullAutoButtonColor =
            new(0.12f, 0.55f, 0.3f, 0.96f);

        private void Awake()
        {
            if (allyFullAutoButton != null)
                allyFullAutoButton.onClick.AddListener(HandleAllyFullAutoClicked);
        }

        private void OnDestroy()
        {
            if (allyFullAutoButton != null)
                allyFullAutoButton.onClick.RemoveListener(HandleAllyFullAutoClicked);
        }

        private void Update()
        {
            if (commandMessageText != null
                && commandMessageText.gameObject.activeSelf
                && Time.unscaledTime >= actionMessageEndTime)
            {
                commandMessageText.text = string.Empty;
                commandMessageText.gameObject.SetActive(false);
            }
        }

        public void SetTimeScaleDisplay(float scale, bool paused)
        {
            if (stateText != null)
                stateText.text = paused ? "PAUSED" : $"SPEED x{scale:0.#}";
        }

        public void SetTargetingState(bool value)
        {
            if (modeText != null)
                modeText.text = value ? "TARGET MODE: ON" : "TARGET MODE: OFF";
        }

        public void SetActionTargetingState(CombatActionDefinition definition)
        {
            if (modeText != null)
                modeText.text = definition != null
                    ? $"{definition.DisplayName}: 대상 선택"
                    : "TARGET MODE: OFF";
        }

        public void SetActionMessage(string message)
        {
            actionMessageEndTime = Time.unscaledTime + 2f;
            if (commandMessageText == null)
                return;
            commandMessageText.text = message ?? string.Empty;
            commandMessageText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(message));
        }

        public void SetRuntimeCommandMessageText(Text text)
        {
            commandMessageText = text;
            if (commandMessageText == null)
                return;
            commandMessageText.text = string.Empty;
            commandMessageText.gameObject.SetActive(false);
        }

        public void SetDebugState(bool value)
        {
            if (debugText != null)
                debugText.text = value ? "DEBUG: ALL" : "DEBUG: SELECTED";
        }

        public void SetSelectedEntity(TacticalEntity entity)
        {
            bool selectionChanged = selectedEntity != entity;
            selectedEntity = entity;
            if (selectionChanged && entity != null)
                selectionChangedFeedbacks?.PlayFeedbacks();
        }

        public void SetSelectedCombatant(Combatant combatant)
        {
            SetSelectedEntity(combatant != null ? combatant.Entity : null);
        }

        public void SetAllyFullAutoState(bool enabled)
        {
            SetAllyControlMode(enabled
                ? CombatControlMode.FullAutomatic
                : CombatControlMode.PlayerMovementAutomaticActions);
        }

        public void SetAllyControlMode(CombatControlMode mode)
        {
            if (allyFullAutoButtonText != null)
                allyFullAutoButtonText.text = $"ALLY AI: {GetControlModeLabel(mode)}";
            if (allyFullAutoButton != null
                && allyFullAutoButton.targetGraphic is Image buttonImage)
            {
                buttonImage.color =
                    mode == CombatControlMode.FullAutomatic
                        ? FullAutoButtonColor
                        : CommandButtonColor;
            }

            if (automaticModeInitialized && previousControlMode != mode)
                automaticModeChangedFeedbacks?.PlayFeedbacks();
            previousControlMode = mode;
            automaticModeInitialized = true;
        }

        public void SetAllyFullAutoInteractable(bool interactable)
        {
            if (allyFullAutoButton != null)
                allyFullAutoButton.interactable = interactable;
        }

        public void ShowResult(string result)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                CanvasGroup canvasGroup = resultPanel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                resultPanel.transform.localScale = Vector3.one * 0.85f;
            }
            if (resultText != null)
                resultText.text = $"{result}\nPress R to Restart";
            resultPanelFeedbacks?.PlayFeedbacks();
        }

        private void HandleAllyFullAutoClicked()
        {
            director?.CycleAllyControlMode();
        }

        private static string GetControlModeLabel(CombatControlMode mode)
            => mode switch
            {
                CombatControlMode.FullAutomatic => "FULL AUTO",
                CombatControlMode.PlayerMovementAutomaticActions => "MOVE MANUAL / ACTION AUTO",
                _ => "FULL MANUAL"
            };

#if UNITY_EDITOR
        public void SetEditorReferences(
            Text newStateText,
            Text newModeText,
            Text newDebugText,
            GameObject newResultPanel,
            Text newResultText,
            Button newAllyFullAutoButton,
            Text newAllyFullAutoButtonText,
            CombatDirector newDirector,
            MMF_Player newSelectionChangedFeedbacks,
            MMF_Player newAutomaticModeChangedFeedbacks,
            MMF_Player newResultPanelFeedbacks)
        {
            stateText = newStateText;
            modeText = newModeText;
            debugText = newDebugText;
            resultPanel = newResultPanel;
            resultText = newResultText;
            allyFullAutoButton = newAllyFullAutoButton;
            allyFullAutoButtonText = newAllyFullAutoButtonText;
            director = newDirector;
            selectionChangedFeedbacks = newSelectionChangedFeedbacks;
            automaticModeChangedFeedbacks = newAutomaticModeChangedFeedbacks;
            resultPanelFeedbacks = newResultPanelFeedbacks;
        }
#endif
    }
}
