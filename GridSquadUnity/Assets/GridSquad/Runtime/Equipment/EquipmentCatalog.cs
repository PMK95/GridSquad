using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Equipment/Catalog", fileName = "EquipmentCatalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [SerializeField] private EquippableDefinition[] equipment = Array.Empty<EquippableDefinition>();
        public IReadOnlyList<EquippableDefinition> Equipment => equipment;

#if UNITY_EDITOR
        public void SetEditorEquipment(EquippableDefinition[] definitions)
            => equipment = definitions ?? Array.Empty<EquippableDefinition>();
#endif
    }
}
