using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum SupportEquipmentPassiveKind
    {
        None,
        RegeneratingBallisticPlate
    }

    [CreateAssetMenu(menuName = "GridSquad/Equipment/Additional Definition", fileName = "AdditionalEquipmentDefinition")]
    public sealed class AdditionalEquipmentDefinition : EquippableDefinition
    {
        [SerializeField] private CombatActionDefinition[] grantedActions = Array.Empty<CombatActionDefinition>();
        [SerializeField] private SupportEquipmentPassiveKind passiveKind;
        [SerializeField, Min(1)] private int maximumPassiveCharges = 3;
        [SerializeField, Min(0.1f)] private float passiveRechargeSeconds = 10f;
        public override EquipmentCategory Category => EquipmentCategory.Support;
        public IReadOnlyList<CombatActionDefinition> GrantedActions => grantedActions;
        public SupportEquipmentPassiveKind PassiveKind => passiveKind;
        public int MaximumPassiveCharges => Mathf.Max(1, maximumPassiveCharges);
        public float PassiveRechargeSeconds => Mathf.Max(0.1f, passiveRechargeSeconds);

#if UNITY_EDITOR
        public void SetEditorGrantedActions(CombatActionDefinition[] actions)
        {
            grantedActions = actions ?? Array.Empty<CombatActionDefinition>();
            ItemActionGrant[] grants = new ItemActionGrant[grantedActions.Length];
            for (int index = 0; index < grantedActions.Length; index++)
                grants[index] = new ItemActionGrant(grantedActions[index], ItemActionAvailability.Equipped);
            SetEditorActionGrants(grants);
        }

        public void SetEditorPassiveConfiguration(
            SupportEquipmentPassiveKind newPassiveKind,
            int newMaximumCharges,
            float newRechargeSeconds)
        {
            passiveKind = newPassiveKind;
            maximumPassiveCharges = Mathf.Max(1, newMaximumCharges);
            passiveRechargeSeconds = Mathf.Max(0.1f, newRechargeSeconds);
        }
#endif
    }
}
