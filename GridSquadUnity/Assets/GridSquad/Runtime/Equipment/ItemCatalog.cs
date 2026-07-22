using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Items/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();

        public IReadOnlyList<ItemDefinition> Items => items;

#if UNITY_EDITOR
        public void SetEditorItems(ItemDefinition[] newItems)
            => items = newItems ?? Array.Empty<ItemDefinition>();
#endif
    }
}
