using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Equipment/Layout Definition", fileName = "EquipmentLayout")]
    public sealed class EquipmentLayoutDefinition : ScriptableObject
    {
        [SerializeField] private EquipmentSlotDefinition[] slots = Array.Empty<EquipmentSlotDefinition>();
        public IReadOnlyList<EquipmentSlotDefinition> Slots => slots;

#if UNITY_EDITOR
        public void SetEditorSlots(EquipmentSlotDefinition[] newSlots)
            => slots = newSlots ?? Array.Empty<EquipmentSlotDefinition>();
#endif
    }
}
