using UnityEngine;

namespace GridSquad
{
    public sealed class ShotEvaluator : MonoBehaviour
    {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private CombatTuning tuning;

        private ShotGeometryEvaluator geometryEvaluator;
        private ShotImpactCalculator impactCalculator;

        private void Awake()
        {
            BuildEvaluationServices();
        }

        public ShotEvaluation EvaluateShot(Combatant shooter, ShootableTarget target)
            => EvaluateShotWithWeapon(shooter, target, shooter != null ? shooter.Weapon : null);

        public ShotEvaluation EvaluateShotWithWeapon(
            Combatant shooter,
            ShootableTarget target,
            WeaponDefinition weapon)
        {
            EnsureEvaluationServices();
            return geometryEvaluator.EvaluateShot(shooter, target, weapon);
        }

        public ShotEvaluation EvaluateShotFromCell(
            Combatant shooter,
            ShootableTarget target,
            GridCoordinate shooterCell,
            bool allowPeek)
            => EvaluateShotFromCellWithWeapon(shooter, target, shooterCell, allowPeek, shooter.Weapon);

        public ShotEvaluation EvaluateShotFromCellWithWeapon(
            Combatant shooter,
            ShootableTarget target,
            GridCoordinate shooterCell,
            bool allowPeek,
            WeaponDefinition weapon)
        {
            EnsureEvaluationServices();
            return geometryEvaluator.EvaluateShotFromCell(
                shooter,
                target,
                shooterCell,
                allowPeek,
                weapon);
        }

        public ShotImpactResult CalculateShotImpact(
            Combatant shooter,
            ShootableTarget intendedTarget,
            ShotEvaluation evaluation)
        {
            EnsureEvaluationServices();
            return impactCalculator.Calculate(shooter, intendedTarget, evaluation);
        }

        public CoverEvaluation EvaluateIncomingCover(Combatant attacker, GridCoordinate targetCell)
        {
            EnsureEvaluationServices();
            return geometryEvaluator.EvaluateIncomingCover(attacker, targetCell);
        }

        public float EvaluateIncomingCoverAtCell(Combatant attacker, GridCoordinate targetCell)
            => EvaluateIncomingCover(attacker, targetCell).EvasionPercent;

        public bool IsCellInsideShootingView(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate shotOriginCell)
        {
            EnsureEvaluationServices();
            return geometryEvaluator.IsCellInsideShootingView(shooter, targetCell, shotOriginCell);
        }

        public ShotEvaluation EvaluateShotAtCell(
            Combatant shooter,
            GridCoordinate targetCell,
            GridCoordinate shotOriginCell)
        {
            EnsureEvaluationServices();
            return geometryEvaluator.EvaluateShotAtCell(shooter, targetCell, shotOriginCell);
        }

        private void EnsureEvaluationServices()
        {
            if (geometryEvaluator == null || impactCalculator == null)
                BuildEvaluationServices();
        }

        private void BuildEvaluationServices()
        {
            FriendlyFireEvaluator friendlyFireEvaluator = new(gridMap, tuning);
            geometryEvaluator = new ShotGeometryEvaluator(gridMap, tuning, friendlyFireEvaluator);
            impactCalculator = new ShotImpactCalculator(gridMap, friendlyFireEvaluator);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(GridMap newGridMap, CombatTuning newTuning)
        {
            gridMap = newGridMap;
            tuning = newTuning;
            BuildEvaluationServices();
        }
#endif
    }
}
