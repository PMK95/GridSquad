using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Actions/Grenade Behavior", fileName = "GrenadeBehavior")]
    public sealed class GrenadeActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        [SerializeField, Min(1)] private int rangeCells = 5;
        [SerializeField, Min(0)] private int radiusCells = 1;
        [SerializeField, Min(1)] private int damage = 40;
        [SerializeField, Min(0.05f)] private float travelSeconds = 0.4f;
        [SerializeField, Min(0.1f)] private float fuseSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float cameraShakeDuration = 0.28f;
        [SerializeField, Min(0f)] private float cameraShakeAmplitude = 1.4f;
        [SerializeField, Min(0f)] private float cameraShakeFrequency = 24f;

        public int RangeCells => rangeCells;
        public int RadiusCells => radiusCells;
        public int Damage => damage;
        public float TravelSeconds => travelSeconds;
        public float FuseSeconds => fuseSeconds;
        public float CameraShakeDuration => cameraShakeDuration;
        public float CameraShakeAmplitude => cameraShakeAmplitude;
        public float CameraShakeFrequency => cameraShakeFrequency;
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.GridCell;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.Exclusive
            | CombatActionCapabilityFlags.PlayerVisible;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new GrenadeCandidateProvider(owner, runtime),
                new GrenadeExecutor(owner, runtime));

        internal override void CollectValidTargetCells(
            CombatActionController owner,
            CombatActionRuntime runtime,
            HashSet<GridCoordinate> results)
        {
            if (runtime.Executor is not GrenadeExecutor executor || owner.GridMap == null)
                return;
            for (int x = 0; x < owner.GridMap.Width; x++)
            {
                for (int z = 0; z < owner.GridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (executor.ValidateTargetCell(cell, out _))
                        results.Add(cell);
                }
            }
        }

        internal override void BuildTargetPreview(
            CombatActionController owner,
            CombatActionRuntime runtime,
            CombatActionTargetSelection selection,
            CombatActionPreview preview)
        {
            if (!selection.HasTargetCell || owner.GridMap == null)
                return;
            bool valid = runtime.Executor is GrenadeExecutor executor
                && executor.ValidateTargetCell(selection.TargetCell, out _);
            bool friendlyFire = owner.DoesAreaContainFriendly(selection.TargetCell, radiusCells);
            preview.Reset(friendlyFire
                ? new Color(1f, 0.08f, 0.08f, 0.58f)
                : valid
                    ? new Color(1f, 0.48f, 0.08f, 0.5f)
                    : new Color(0.8f, 0.05f, 0.05f, 0.5f));
            for (int x = selection.TargetCell.X - radiusCells; x <= selection.TargetCell.X + radiusCells; x++)
            {
                for (int z = selection.TargetCell.Z - radiusCells; z <= selection.TargetCell.Z + radiusCells; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (owner.GridMap.IsInside(cell))
                        preview.AddAffectedCell(cell);
                }
            }
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            int newRangeCells,
            int newRadiusCells,
            int newDamage,
            float newTravelSeconds,
            float newFuseSeconds,
            float newCameraShakeDuration,
            float newCameraShakeAmplitude,
            float newCameraShakeFrequency)
        {
            rangeCells = newRangeCells;
            radiusCells = newRadiusCells;
            damage = newDamage;
            travelSeconds = newTravelSeconds;
            fuseSeconds = newFuseSeconds;
            cameraShakeDuration = newCameraShakeDuration;
            cameraShakeAmplitude = newCameraShakeAmplitude;
            cameraShakeFrequency = newCameraShakeFrequency;
        }
#endif
    }
}
