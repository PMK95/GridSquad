using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class CharacterWorldUiPresenter : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private MMHealthBar healthBar;
        [SerializeField] private Image reloadFill;
        [SerializeField] private Text outOfAmmoText;
        [SerializeField] private Text detailText;
        [SerializeField] private GameObject selectionIndicator;
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private LineRenderer targetRing;
        [SerializeField] private LineRenderer peekLine;
        [SerializeField] private LineRenderer peekRing;

        private Combatant owner;
        private int displayedHealth = -1;
        private int displayedMaximumHealth = -1;

        public void Initialize(Combatant newOwner)
        {
            owner = newOwner;
            displayedHealth = -1;
            displayedMaximumHealth = -1;
            RefreshHealth();
            RefreshMagazine();
        }

        public void Refresh(
            Combatant target,
            ShotEvaluation evaluation,
            bool selected,
            bool debugVisible)
        {
            bool showDetails = selected || debugVisible;
            RefreshHealth();
            RefreshMagazine();
            if (canvas != null)
                canvas.enabled = owner != null && owner.IsAlive;
            if (detailText != null)
            {
                detailText.gameObject.SetActive(owner != null && owner.IsAlive);
                detailText.text = showDetails
                    ? BuildDetailText(target, evaluation)
                    : $"{owner.DisplayName}  HP {owner.CurrentHealth}/{owner.MaximumHealth}";
            }

            bool showTargetSet = showDetails && target != null && target.IsAlive;
            if (targetLine != null)
                targetLine.enabled = showTargetSet;
            if (targetRing != null)
                targetRing.enabled = showTargetSet;
            Color color = !evaluation.CanShoot
                ? Color.red
                : evaluation.CoverEvasionPercent > 0.01f ? Color.yellow : Color.green;
            if (showTargetSet)
            {
                Vector3 targetDisplayPosition = target.CurrentExposureCenter;
                if (targetLine != null)
                {
                    targetLine.SetPosition(0, evaluation.ShotOrigin);
                    targetLine.SetPosition(1, targetDisplayPosition);
                    SetLineColor(targetLine, color);
                }
                Vector3 targetRingPosition = new(
                    targetDisplayPosition.x,
                    target.transform.position.y + 0.08f,
                    targetDisplayPosition.z);
                SetRingPositions(targetRing, targetRingPosition, 0.78f);
                SetLineColor(targetRing, color);
            }

            bool showPeekSet = showDetails && evaluation.UsesPeekPosition;
            if (peekLine != null)
                peekLine.enabled = showPeekSet;
            if (peekRing != null)
                peekRing.enabled = showPeekSet;
            if (showPeekSet)
            {
                Vector3 peekGroundPosition = new(
                    evaluation.ShotOrigin.x,
                    owner.transform.position.y + 0.08f,
                    evaluation.ShotOrigin.z);
                if (peekLine != null)
                {
                    peekLine.SetPosition(0, owner.transform.position + Vector3.up * 0.08f);
                    peekLine.SetPosition(1, peekGroundPosition);
                    SetLineColor(peekLine, Color.cyan);
                }
                SetRingPositions(peekRing, peekGroundPosition, 0.68f);
                SetLineColor(peekRing, Color.cyan);
            }
        }

        public void RefreshHealth()
        {
            if (owner == null || healthBar == null)
                return;
            if (displayedHealth == owner.CurrentHealth
                && displayedMaximumHealth == owner.MaximumHealth)
            {
                return;
            }
            displayedHealth = owner.CurrentHealth;
            displayedMaximumHealth = owner.MaximumHealth;
            healthBar.UpdateBar(owner.CurrentHealth, 0f, owner.MaximumHealth, true);
        }

        public void RefreshMagazine()
        {
            if (owner == null)
                return;
            if (reloadFill != null)
                reloadFill.fillAmount = owner.MagazineFillRatio;
            if (outOfAmmoText != null)
                outOfAmmoText.gameObject.SetActive(owner.IsAlive && owner.IsOutOfAmmo);
        }

        public void SetSelected(bool value)
        {
            if (selectionIndicator != null)
                selectionIndicator.SetActive(value);
        }

        public void SetDead()
        {
            if (canvas != null)
                canvas.enabled = false;
            if (selectionIndicator != null)
                selectionIndicator.SetActive(false);
            if (targetLine != null)
                targetLine.enabled = false;
            if (targetRing != null)
                targetRing.enabled = false;
            if (peekLine != null)
                peekLine.enabled = false;
            if (peekRing != null)
                peekRing.enabled = false;
            if (reloadFill != null)
                reloadFill.fillAmount = 0f;
            if (outOfAmmoText != null)
                outOfAmmoText.gameObject.SetActive(false);
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            if (line == null)
                return;
            line.startColor = color;
            line.endColor = color;
        }

        private static void SetRingPositions(LineRenderer ring, Vector3 center, float radius)
        {
            if (ring == null)
                return;
            int segmentCount = Mathf.Max(4, ring.positionCount - 1);
            for (int index = 0; index <= segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                ring.SetPosition(index, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private string BuildDetailText(Combatant target, ShotEvaluation evaluation)
        {
            string targetName = target != null ? target.DisplayName : "-";
            string state = evaluation.CanShoot ? $"HIT {evaluation.HitChancePercent:0}%" : "NO SHOT";
            string coverAngle = evaluation.CoverAngleDegrees >= 0f ? $"{evaluation.CoverAngleDegrees:0}deg" : "-";
            return $"{owner.DisplayName}  HP {owner.CurrentHealth}/{owner.MaximumHealth}  TGT {targetName}\n{state}  COV {evaluation.CoverEvasionPercent:0}%  ANG {coverAngle}  PEEK {(owner.PeekEnabled ? "ON" : "OFF")}  FIRE {owner.FireState}  AMMO {owner.CurrentMagazineAmmo}/{owner.ReserveAmmo}";
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Canvas newCanvas,
            MMHealthBar newHealthBar,
            Text newOutOfAmmoText,
            Text newDetailText,
            GameObject newSelectionIndicator,
            LineRenderer newTargetLine,
            LineRenderer newTargetRing,
            LineRenderer newPeekLine,
            LineRenderer newPeekRing)
        {
            canvas = newCanvas;
            healthBar = newHealthBar;
            outOfAmmoText = newOutOfAmmoText;
            detailText = newDetailText;
            selectionIndicator = newSelectionIndicator;
            targetLine = newTargetLine;
            targetRing = newTargetRing;
            peekLine = newPeekLine;
            peekRing = newPeekRing;
        }
#endif
    }
}
