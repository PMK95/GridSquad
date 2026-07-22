using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatTargetingController : MonoBehaviour
    {
        private Combatant owner;
        private ShootableTarget selfTarget;
        private ShotEvaluator shotEvaluator;
        private Transform muzzle;
        private ShootableTarget currentTarget;
        private ShootableTarget manualTargetHoverPreview;
        private ShotEvaluation currentShotEvaluation;
        private bool peekEnabled;

        public ShootableTarget CurrentTarget => currentTarget;
        public ShotEvaluation CurrentShotEvaluation => currentShotEvaluation;
        public bool PeekEnabled => peekEnabled;
        public Vector3 MuzzlePosition => muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 1.25f;
        public float MuzzleHeight => MuzzlePosition.y - transform.position.y;
        public GridCoordinate CurrentExposureCell => peekEnabled && currentShotEvaluation.UsesPeekPosition
            ? currentShotEvaluation.ShotOriginCell
            : owner.CurrentCell;
        public Vector3 CurrentExposureCenter
        {
            get
            {
                Vector3 center = owner.GridMap.GridToWorld(CurrentExposureCell);
                center.y = owner.CurrentAimCenter.y;
                return center;
            }
        }
        public GridCoordinate CurrentIndicatorShotOriginCell => CurrentExposureCell;
        public Vector3 CurrentIndicatorShotOrigin => peekEnabled && currentShotEvaluation.UsesPeekPosition
            ? currentShotEvaluation.ShotOrigin
            : MuzzlePosition;

        public void Initialize(
            Combatant newOwner,
            ShootableTarget newSelfTarget,
            ShotEvaluator newShotEvaluator,
            Transform newMuzzle)
        {
            owner = newOwner;
            selfTarget = newSelfTarget;
            shotEvaluator = newShotEvaluator;
            muzzle = newMuzzle;
        }

        public bool SetTarget(ShootableTarget target)
        {
            ShootableTarget validTarget = target != null
                && target != selfTarget
                && target.TargetTeam != owner.Team
                && target.IsAlive
                    ? target
                    : null;
            if (currentTarget == validTarget)
                return false;
            currentTarget = validTarget;
            return true;
        }

        public void SetManualHoverTarget(ShootableTarget target)
        {
            manualTargetHoverPreview = target != null
                && target != selfTarget
                && target.IsAlive
                && target.TargetTeam != owner.Team
                    ? target
                    : null;
        }

        public void ClearManualHoverTarget()
        {
            manualTargetHoverPreview = null;
        }

        public void GetPresentationTarget(
            out ShootableTarget presentationTarget,
            out ShotEvaluation presentationEvaluation)
        {
            presentationTarget = manualTargetHoverPreview;
            if (presentationTarget != null
                && (!presentationTarget.IsAlive
                    || presentationTarget == selfTarget
                    || presentationTarget.TargetTeam == owner.Team))
            {
                manualTargetHoverPreview = null;
                presentationTarget = null;
            }
            presentationEvaluation = currentShotEvaluation;
            if (presentationTarget != null)
                presentationEvaluation = shotEvaluator.EvaluateShot(owner, presentationTarget);
            else
                presentationTarget = currentTarget;
        }

        public void RefreshShotEvaluation()
        {
            if (currentTarget != null && !currentTarget.IsAlive)
                currentTarget = null;
            currentShotEvaluation = shotEvaluator.EvaluateShot(owner, currentTarget);
            Vector3 unshiftedMuzzle = transform.position + Vector3.up * MuzzleHeight;
            Vector3 desiredOffset = currentShotEvaluation.UsesPeekPosition
                ? currentShotEvaluation.ShotOrigin - unshiftedMuzzle
                : Vector3.zero;
            owner.SetActivePeekOffset(desiredOffset);
        }

        public bool SetPeekEnabled(bool enabled)
        {
            if (peekEnabled == enabled)
                return false;
            peekEnabled = enabled;
            if (!enabled)
                owner.SetActivePeekOffset(Vector3.zero);
            return true;
        }

        public bool UpdateAutomaticPeek(bool allowed)
        {
            if (!allowed || owner.IsMoving || currentTarget == null || !currentTarget.IsAlive)
                return SetPeekEnabled(false);
            ShotEvaluation direct = shotEvaluator.EvaluateShotFromCell(
                owner,
                currentTarget,
                owner.CurrentCell,
                false);
            ShotEvaluation bestWithPeek = shotEvaluator.EvaluateShotFromCell(
                owner,
                currentTarget,
                owner.CurrentCell,
                true);
            bool shouldPeek = bestWithPeek.CanShoot
                && bestWithPeek.UsesPeekPosition
                && (!direct.CanShoot || bestWithPeek.HitChancePercent > direct.HitChancePercent);
            return SetPeekEnabled(shouldPeek);
        }

        public void ClearForEntityRemoval()
        {
            manualTargetHoverPreview = null;
            currentTarget = null;
            peekEnabled = false;
            owner.SetActivePeekOffset(Vector3.zero);
        }

        public void SetMuzzle(Transform newMuzzle)
        {
            muzzle = newMuzzle;
        }
    }
}
