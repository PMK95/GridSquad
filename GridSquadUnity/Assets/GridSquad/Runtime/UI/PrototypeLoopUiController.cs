using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class PrototypeLoopUiController : MonoBehaviour
    {
        [Header("기지")]
        [SerializeField] private GameObject basePanel;
        [SerializeField] private Transform rosterContent;
        [SerializeField] private Toggle rosterTogglePrefab;
        [SerializeField] private Button launchButton;
        [SerializeField] private Button repairAllButton;
        [SerializeField] private TMP_Text baseStatusText;

        [Header("스테이지 사이")]
        [SerializeField] private GameObject betweenStagePanel;
        [SerializeField] private TMP_Text stageStatusText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button extractButton;

        private readonly List<string> selectedUnitIds = new();
        private readonly List<Toggle> rosterToggles = new();
        private PrototypeGameApplication application;

        private void Start()
        {
            application = PrototypeGameApplication.Instance;
            if (application == null)
            {
                Debug.LogError("[프로토타입 UI] PrototypeGameApplication을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }
            application.StateChanged += Refresh;
            launchButton.onClick.AddListener(LaunchMission);
            repairAllButton.onClick.AddListener(RepairAll);
            continueButton.onClick.AddListener(application.ContinueToNextStage);
            extractButton.onClick.AddListener(application.ExtractToBase);
            BuildRoster();
            Refresh();
        }

        private void OnDestroy()
        {
            if (application != null)
                application.StateChanged -= Refresh;
        }

        private void BuildRoster()
        {
            foreach (Toggle toggle in rosterToggles)
                if (toggle != null)
                    Destroy(toggle.gameObject);
            rosterToggles.Clear();
            selectedUnitIds.Clear();

            foreach (UnitState unit in application.Session.BaseState.Units)
            {
                UnitState capturedUnit = unit;
                Toggle toggle = Instantiate(rosterTogglePrefab, rosterContent);
                toggle.gameObject.SetActive(true);
                TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    UnitDefinition definition =
                        application.Catalog.GetRequiredUnit(unit.UnitDefinitionId);
                    label.text = BuildUnitLabel(unit, definition);
                }
                toggle.SetIsOnWithoutNotify(false);
                toggle.onValueChanged.AddListener(
                    selected => ChangeUnitSelection(capturedUnit.UnitInstanceId, toggle, selected));
                rosterToggles.Add(toggle);
            }
        }

        private static string BuildUnitLabel(UnitState unit, UnitDefinition definition)
        {
            string aftereffects = unit.Aftereffects.Count == 0
                ? "후유증 없음"
                : string.Join(
                    ", ",
                    unit.Aftereffects.Select(
                        state => $"{state.AftereffectId}({state.RemainingRestMissions}회 휴식)"));
            string equipment = string.Join(
                ", ",
                unit.Equipment.Select(slot =>
                {
                    ItemState item = unit.FindItem(slot.ItemInstanceId);
                    return item == null
                        ? slot.SlotId
                        : $"{item.ItemDefinitionId} 내구도 {item.Durability}";
                }));
            return $"{definition.DisplayName} / {definition.RoleName}\n{aftereffects}\n{equipment}";
        }

        private void ChangeUnitSelection(string unitId, Toggle toggle, bool selected)
        {
            if (selected)
            {
                if (selectedUnitIds.Count >= 3)
                {
                    toggle.SetIsOnWithoutNotify(false);
                    baseStatusText.text = "최대 3명까지 선발할 수 있습니다.";
                    return;
                }
                if (!selectedUnitIds.Contains(unitId))
                    selectedUnitIds.Add(unitId);
            }
            else
            {
                selectedUnitIds.Remove(unitId);
            }
            Refresh();
        }

        private void LaunchMission()
        {
            if (!application.TryStartDefaultMission(selectedUnitIds, out string failureReason))
            {
                baseStatusText.text = failureReason;
                return;
            }
            baseStatusText.text = "임무 지역으로 이동합니다.";
        }

        private void RepairAll()
        {
            int repaired = application.RepairAllEquipment();
            baseStatusText.text = repaired > 0
                ? $"{repaired}개 장비를 무료로 수리했습니다."
                : "수리할 장비가 없습니다.";
            BuildRoster();
            Refresh();
        }

        private void Refresh()
        {
            if (application == null)
                return;
            bool baseReady = application.FlowState == GameFlowState.BaseReady;
            bool betweenStages = application.FlowState == GameFlowState.BetweenStages;
            basePanel.SetActive(baseReady);
            betweenStagePanel.SetActive(betweenStages);
            if (baseReady)
            {
                int minimum = application.DefaultMission != null
                    ? application.DefaultMission.MinimumSquadSize
                    : 1;
                int maximum = application.DefaultMission != null
                    ? application.DefaultMission.MaximumSquadSize
                    : 3;
                launchButton.interactable =
                    selectedUnitIds.Count >= minimum && selectedUnitIds.Count <= maximum;
                if (string.IsNullOrWhiteSpace(baseStatusText.text))
                    baseStatusText.text = $"{minimum}~{maximum}명을 선발하세요.";
            }
            if (betweenStages)
            {
                int completed = application.Session.ActiveMission.NextStageIndex;
                int total = application.DefaultMission.Stages.Count;
                stageStatusText.text =
                    $"스테이지 {completed}/{total} 완료\n현재 체력·탄약·소모품·내구도가 다음 스테이지로 이어집니다.";
                continueButton.interactable = application.CanContinueStage;
                extractButton.interactable = application.CanExtract;
            }
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            GameObject newBasePanel,
            Transform newRosterContent,
            Toggle newRosterTogglePrefab,
            Button newLaunchButton,
            Button newRepairAllButton,
            TMP_Text newBaseStatusText,
            GameObject newBetweenStagePanel,
            TMP_Text newStageStatusText,
            Button newContinueButton,
            Button newExtractButton)
        {
            basePanel = newBasePanel;
            rosterContent = newRosterContent;
            rosterTogglePrefab = newRosterTogglePrefab;
            launchButton = newLaunchButton;
            repairAllButton = newRepairAllButton;
            baseStatusText = newBaseStatusText;
            betweenStagePanel = newBetweenStagePanel;
            stageStatusText = newStageStatusText;
            continueButton = newContinueButton;
            extractButton = newExtractButton;
        }
#endif
    }
}
