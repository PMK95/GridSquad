using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Unit Trait Definition", fileName = "UnitTraitDefinition")]
    public sealed class UnitTraitDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "특성";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private int maximumHealthDelta;
        [SerializeField] private float movementSpeedMultiplierDelta;
        [SerializeField] private float hitChanceBonusPercent;
        [SerializeField] private float damageMultiplierDelta;

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaximumHealthDelta => maximumHealthDelta;
        public float MovementSpeedMultiplierDelta => movementSpeedMultiplierDelta;
        public float HitChanceBonusPercent => hitChanceBonusPercent;
        public float DamageMultiplierDelta => damageMultiplierDelta;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newDisplayName,
            string newDescription,
            Sprite newIcon,
            int newMaximumHealthDelta,
            float newMovementSpeedMultiplierDelta,
            float newHitChanceBonusPercent,
            float newDamageMultiplierDelta)
        {
            displayName = newDisplayName;
            description = newDescription;
            icon = newIcon;
            maximumHealthDelta = newMaximumHealthDelta;
            movementSpeedMultiplierDelta = newMovementSpeedMultiplierDelta;
            hitChanceBonusPercent = newHitChanceBonusPercent;
            damageMultiplierDelta = newDamageMultiplierDelta;
        }
#endif
    }
}
