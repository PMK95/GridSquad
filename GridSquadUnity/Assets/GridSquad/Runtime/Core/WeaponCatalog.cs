using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        [SerializeField] private List<WeaponDefinition> weapons = new();

        public IReadOnlyList<WeaponDefinition> Weapons => weapons;

#if UNITY_EDITOR
        public void SetEditorWeapons(IEnumerable<WeaponDefinition> newWeapons)
        {
            weapons.Clear();
            if (newWeapons != null)
                weapons.AddRange(newWeapons);
        }
#endif
    }
}
