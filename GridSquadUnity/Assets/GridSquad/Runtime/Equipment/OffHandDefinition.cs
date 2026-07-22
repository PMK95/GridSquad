using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Equipment/Off-Hand Definition", fileName = "OffHandDefinition")]
    public sealed class OffHandDefinition : EquippableDefinition
    {
        public override EquipmentCategory Category => EquipmentCategory.OffHand;
    }
}
