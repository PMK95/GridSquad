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
        [SerializeField] private GameObject selectedInfoPanel;
        [SerializeField] private Text selectedInfoTitleText;
        [SerializeField] private Text selectedInfoBodyText;

        private Combatant selectedCombatant;

        private void Update()
        {
            RefreshSelectedCombatantInfo();
        }

        public void SetTimeScaleDisplay(float scale, bool paused)
        {
            if (stateText != null)
                stateText.text = paused ? "PAUSED" : $"SPEED x{scale:0}";
        }

        public void SetTargetingState(bool value)
        {
            if (modeText != null)
                modeText.text = value ? "TARGET MODE: ON" : "TARGET MODE: OFF";
        }

        public void SetDebugState(bool value)
        {
            if (debugText != null)
                debugText.text = value ? "DEBUG: ALL" : "DEBUG: SELECTED";
        }

        public void SetSelectedCombatant(Combatant combatant)
        {
            selectedCombatant = combatant;
            RefreshSelectedCombatantInfo();
        }

        public void ShowResult(string result)
        {
            if (resultPanel != null)
                resultPanel.SetActive(true);
            if (resultText != null)
                resultText.text = $"{result}\nPress R to Restart";
        }

        private void RefreshSelectedCombatantInfo()
        {
            if (selectedInfoPanel != null)
                selectedInfoPanel.SetActive(true);
            if (selectedInfoTitleText != null)
                selectedInfoTitleText.text = selectedCombatant != null
                    ? $"SELECTED: {selectedCombatant.name}"
                    : "SELECTED UNIT";
            if (selectedInfoBodyText == null)
                return;

            if (selectedCombatant == null)
            {
                selectedInfoBodyText.text = "NO UNIT SELECTED\n\nLMB: SELECT ALLY";
                return;
            }

            ShotEvaluation shot = selectedCombatant.CurrentShotEvaluation;
            Combatant target = selectedCombatant.CurrentTarget;
            WeaponDefinition weapon = selectedCombatant.Weapon;
            string team = selectedCombatant.Team == Team.Ally ? "ALLY" : "ENEMY";
            string movement = !selectedCombatant.IsAlive ? "DEAD" : selectedCombatant.IsMoving ? "MOVING" : "IDLE";
            string targetName = target != null && target.IsAlive ? target.name : "-";
            string shotState = shot.CanShoot ? "READY" : $"BLOCKED ({shot.FailureReason})";
            string weaponInfo = weapon != null
                ? $"DMG {weapon.Damage}  RANGE {weapon.RangeInCells:0}\nAIM {weapon.AimDuration:0.0}s  COOLDOWN {weapon.FireInterval:0.0}s"
                : "WEAPON -";
            string fireState = selectedCombatant.FireStateRemainingSeconds > 0.01f
                ? $"{selectedCombatant.FireState}  {selectedCombatant.FireStateRemainingSeconds:0.0}s"
                : selectedCombatant.FireState.ToString();
            string coverAngle = shot.CoverAngleDegrees >= 0f ? $"{shot.CoverAngleDegrees:0} deg" : "-";

            selectedInfoBodyText.text =
                $"TEAM  {team}\n" +
                $"HP    {selectedCombatant.CurrentHealth} / {selectedCombatant.MaximumHealth}\n" +
                $"CELL  {selectedCombatant.CurrentCell}\n" +
                $"STATE {movement}\n\n" +
                $"TARGET  {targetName}\n" +
                $"SHOT    {shotState}\n" +
                $"FIRE    {fireState}\n" +
                $"HIT     {shot.HitChancePercent:0}%\n" +
                $"COVER   {shot.CoverEvasionPercent:0}%\n" +
                $"COVER ANG {coverAngle}\n" +
                $"PEEK    {(selectedCombatant.PeekEnabled ? "ON" : "OFF")}\n\n" +
                weaponInfo;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Text newStateText,
            Text newModeText,
            Text newDebugText,
            GameObject newResultPanel,
            Text newResultText,
            GameObject newSelectedInfoPanel,
            Text newSelectedInfoTitleText,
            Text newSelectedInfoBodyText)
        {
            stateText = newStateText;
            modeText = newModeText;
            debugText = newDebugText;
            resultPanel = newResultPanel;
            resultText = newResultText;
            selectedInfoPanel = newSelectedInfoPanel;
            selectedInfoTitleText = newSelectedInfoTitleText;
            selectedInfoBodyText = newSelectedInfoBodyText;
        }
#endif
    }
}
