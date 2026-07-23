using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Actions/Dash Behavior", fileName = "DashBehavior")]
    public sealed class DashActionBehaviorDefinition : CombatActionBehaviorDefinition
    {
        [SerializeField, Min(1)] private int maximumCells = 3;
        [SerializeField, Min(1f)] private float movementSpeedMultiplier = 3f;
        [SerializeField, Min(0f)] private float minimumPositionImprovement = 15f;
        [SerializeField, Min(1)] private int knockbackCells = 1;

        public int MaximumCells => maximumCells;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float MinimumPositionImprovement => minimumPositionImprovement;
        public int KnockbackCells => Mathf.Max(1, knockbackCells);
        public override CombatActionTargetingMode TargetingMode => CombatActionTargetingMode.GridCell;
        public override CombatActionCapabilityFlags Capabilities =>
            CombatActionCapabilityFlags.ChangesPosition
            | CombatActionCapabilityFlags.Exclusive
            | CombatActionCapabilityFlags.PlayerVisible;

        internal override CombatActionRuntimeParts CreateRuntimeParts(
            CombatActionController owner,
            CombatActionRuntime runtime)
            => new(
                new DashCandidateProvider(owner, runtime),
                new DashExecutor(owner, runtime));

        internal override void CollectValidTargetCells(
            CombatActionController owner,
            CombatActionRuntime runtime,
            HashSet<GridCoordinate> results)
        {
            if (runtime.Executor is not DashExecutor executor || owner.GridMap == null)
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
            if (!selection.HasTargetCell || owner.OwnerCombatant == null || owner.GridMap == null)
                return;
            DashPathPlan pathPlan = default;
            bool valid = runtime.Executor is DashExecutor executor
                && executor.TryBuildPathPlan(selection.TargetCell, out pathPlan, out _);
            preview.Reset(valid
                ? pathPlan.HasCollision
                    ? new Color(1f, 0.48f, 0.08f, 0.68f)
                    : new Color(0.2f, 0.9f, 1f, 0.55f)
                : new Color(1f, 0.08f, 0.08f, 0.58f));
            GridCoordinate origin = owner.OwnerCombatant.CurrentCell;
            int xStep = Math.Sign(selection.TargetCell.X - origin.X);
            int zStep = Math.Sign(selection.TargetCell.Z - origin.Z);
            GridCoordinate previewEnd = valid && pathPlan.HasCollision
                ? pathPlan.CollisionCell
                : selection.TargetCell;
            int distance = origin.ManhattanDistance(previewEnd);
            for (int step = 1; step <= distance; step++)
            {
                GridCoordinate cell = new(origin.X + xStep * step, origin.Z + zStep * step);
                if (owner.GridMap.IsInside(cell))
                    preview.AddAffectedCell(cell);
            }
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            int newMaximumCells,
            float newMovementSpeedMultiplier,
            float newMinimumPositionImprovement,
            int newKnockbackCells = 1)
        {
            maximumCells = newMaximumCells;
            movementSpeedMultiplier = newMovementSpeedMultiplier;
            minimumPositionImprovement = newMinimumPositionImprovement;
            knockbackCells = Mathf.Max(1, newKnockbackCells);
        }
#endif
    }
}
