namespace GridSquad
{
    public static class EquipmentSlotCompatibility
    {
        public static bool CanAssign(
            EquipmentSlotDefinition slot,
            EquippableDefinition equipment)
        {
            if (slot == null || equipment == null)
                return false;

            return slot.SlotKind switch
            {
                EquipmentSlotKind.LeftHand => equipment is WeaponDefinition,
                EquipmentSlotKind.RightHand => equipment is OffHandDefinition,
                EquipmentSlotKind.Head => equipment is ArmorDefinition armor
                    && armor.ArmorSlotKind == EquipmentSlotKind.Head,
                EquipmentSlotKind.Torso => equipment is ArmorDefinition armor
                    && armor.ArmorSlotKind == EquipmentSlotKind.Torso,
                EquipmentSlotKind.Legs => equipment is ArmorDefinition armor
                    && armor.ArmorSlotKind == EquipmentSlotKind.Legs,
                EquipmentSlotKind.Hands => equipment is ArmorDefinition armor
                    && armor.ArmorSlotKind == EquipmentSlotKind.Hands,
                EquipmentSlotKind.Feet => equipment is ArmorDefinition armor
                    && armor.ArmorSlotKind == EquipmentSlotKind.Feet,
                EquipmentSlotKind.SupportOne or EquipmentSlotKind.SupportTwo
                    => equipment is AdditionalEquipmentDefinition,
                _ => false
            };
        }
    }
}
