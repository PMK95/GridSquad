using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatantFacingController : MonoBehaviour
    {
        private Combatant owner;
        private GridMovementController movementController;
        private CombatTuning tuning;
        private Transform visualRoot;
        private Vector3 activePeekOffset;

        public void Initialize(
            Combatant newOwner,
            GridMovementController newMovementController,
            CombatTuning newTuning,
            Transform newVisualRoot)
        {
            owner = newOwner;
            movementController = newMovementController;
            tuning = newTuning;
            visualRoot = newVisualRoot;
        }

        public void Tick(bool hitReacting)
        {
            if (hitReacting || movementController == null)
                return;
            Vector3 lookDirection = movementController.GetHorizontalLookDirection(owner.CurrentTarget);
            if (lookDirection.sqrMagnitude < 0.0001f)
                return;
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float rotationSpeed = tuning != null ? tuning.CharacterRotationSpeed : 360f;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                rotationSpeed * Time.deltaTime);
            ApplyPeekVisualLean();
        }

        public void SetPeekOffset(Vector3 worldOffset)
        {
            activePeekOffset = worldOffset;
            ApplyPeekVisualLean();
        }

        private void ApplyPeekVisualLean()
        {
            if (visualRoot == null)
                return;
            visualRoot.localPosition = Vector3.zero;
            Vector3 localOffset = transform.InverseTransformVector(activePeekOffset);
            localOffset.y = 0f;
            if (localOffset.sqrMagnitude < 0.0001f)
            {
                visualRoot.localRotation = Quaternion.identity;
                return;
            }
            Vector3 leanAxis = Vector3.Cross(Vector3.up, localOffset.normalized);
            float leanAngle = tuning != null ? tuning.PeekVisualLeanAngle : 8f;
            visualRoot.localRotation = Quaternion.AngleAxis(leanAngle, leanAxis);
        }
    }
}
