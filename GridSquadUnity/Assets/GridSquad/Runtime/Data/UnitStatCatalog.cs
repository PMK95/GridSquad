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
        [SerializeField] private UnitStatDefinition defense;
        [SerializeField] private UnitStatDefinition fireRateMultiplier;
        [SerializeField] private UnitStatDefinition carryCapacity;
        [SerializeField] private UnitStatDefinition traumaResistance;

        public UnitStatDefinition MaximumHealth => maximumHealth;
        public UnitStatDefinition MovementSpeedMultiplier => movementSpeedMultiplier;
        public UnitStatDefinition HitChanceBonusPercent => hitChanceBonusPercent;
        public UnitStatDefinition DamageMultiplier => damageMultiplier;
        public UnitStatDefinition Defense => defense;
        public UnitStatDefinition FireRateMultiplier => fireRateMultiplier;
        public UnitStatDefinition CarryCapacity => carryCapacity;
        public UnitStatDefinition TraumaResistance => traumaResistance;

        public IEnumerable<UnitStatDefinition> CoreStats
        {
            get
            {
                yield return maximumHealth;
                yield return movementSpeedMultiplier;
                yield return hitChanceBonusPercent;
                yield return damageMultiplier;
                yield return defense;
                yield return fireRateMultiplier;
                yield return carryCapacity;
                yield return traumaResistance;
            }
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            UnitStatDefinition newMaximumHealth,
            UnitStatDefinition newMovementSpeedMultiplier,
            UnitStatDefinition newHitChanceBonusPercent,
            UnitStatDefinition newDamageMultiplier,
            UnitStatDefinition newCarryCapacity = null,
            UnitStatDefinition newDefense = null,
            UnitStatDefinition newFireRateMultiplier = null,
            UnitStatDefinition newTraumaResistance = null)
        {
            maximumHealth = newMaximumHealth;
            movementSpeedMultiplier = newMovementSpeedMultiplier;
            hitChanceBonusPercent = newHitChanceBonusPercent;
            damageMultiplier = newDamageMultiplier;
            defense = newDefense;
            fireRateMultiplier = newFireRateMultiplier;
            carryCapacity = newCarryCapacity;
            traumaResistance = newTraumaResistance;
        }
#endif
    }
}
