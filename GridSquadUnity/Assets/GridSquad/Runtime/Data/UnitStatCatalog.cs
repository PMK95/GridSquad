using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Stats/Unit Stat Catalog", fileName = "UnitStatCatalog")]
    public sealed class UnitStatCatalog : ScriptableObject
    {
        [SerializeField] private UnitStatDefinition maximumHealth;
        [SerializeField] private UnitStatDefinition movementSpeedMultiplier;
        [SerializeField] private UnitStatDefinition hitChanceBonusPercent;
        [SerializeField] private UnitStatDefinition damageMultiplier;
        [SerializeField] private UnitStatDefinition carryCapacity;

        public UnitStatDefinition MaximumHealth => maximumHealth;
        public UnitStatDefinition MovementSpeedMultiplier => movementSpeedMultiplier;
        public UnitStatDefinition HitChanceBonusPercent => hitChanceBonusPercent;
        public UnitStatDefinition DamageMultiplier => damageMultiplier;
        public UnitStatDefinition CarryCapacity => carryCapacity;

        public IEnumerable<UnitStatDefinition> CoreStats
        {
            get
            {
                yield return maximumHealth;
                yield return movementSpeedMultiplier;
                yield return hitChanceBonusPercent;
                yield return damageMultiplier;
                yield return carryCapacity;
            }
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            UnitStatDefinition newMaximumHealth,
            UnitStatDefinition newMovementSpeedMultiplier,
            UnitStatDefinition newHitChanceBonusPercent,
            UnitStatDefinition newDamageMultiplier,
            UnitStatDefinition newCarryCapacity = null)
        {
            maximumHealth = newMaximumHealth;
            movementSpeedMultiplier = newMovementSpeedMultiplier;
            hitChanceBonusPercent = newHitChanceBonusPercent;
            damageMultiplier = newDamageMultiplier;
            carryCapacity = newCarryCapacity;
        }
#endif
    }
}
