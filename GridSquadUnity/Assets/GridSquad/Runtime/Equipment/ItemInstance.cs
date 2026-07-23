using System;
using UnityEngine;

namespace GridSquad
{
    [Serializable]
    public sealed class ItemInstance
    {
        [SerializeField] private string instanceId;
        [SerializeField] private ItemDefinition definition;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Min(0)] private int durability;
        [SerializeField, Min(0)] private int magazineAmmo;
        [SerializeField, Min(0)] private int reserveAmmo;

        public ItemInstance(ItemDefinition definition, int quantity = 1)
        {
            instanceId = Guid.NewGuid().ToString("N");
            this.definition = definition;
            this.quantity = definition != null
                ? Mathf.Clamp(quantity, 1, definition.MaximumStack)
                : Mathf.Max(1, quantity);
            if (definition is WeaponDefinition weapon)
            {
                magazineAmmo = Mathf.Max(1, weapon.MagazineCapacity);
                reserveAmmo = Mathf.Max(0, weapon.StartingReserveAmmo);
            }
            if (definition is EquippableDefinition equipment)
                durability = equipment.MaximumDurability;
        }

        public ItemInstance(
            string instanceId,
            ItemDefinition definition,
            int quantity,
            int durability,
            int magazineAmmo,
            int reserveAmmo)
        {
            this.instanceId = string.IsNullOrWhiteSpace(instanceId)
                ? Guid.NewGuid().ToString("N")
                : instanceId;
            this.definition = definition;
            this.quantity = Mathf.Max(0, quantity);
            this.durability = Mathf.Max(0, durability);
            this.magazineAmmo = Mathf.Max(0, magazineAmmo);
            this.reserveAmmo = Mathf.Max(0, reserveAmmo);
        }

        public string InstanceId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(instanceId))
                    instanceId = Guid.NewGuid().ToString("N");
                return instanceId;
            }
        }

        public ItemDefinition Definition => definition;
        public int Quantity => Mathf.Max(0, quantity);
        public int Durability => Mathf.Max(0, durability);
        public int MagazineAmmo => Mathf.Max(0, magazineAmmo);
        public int ReserveAmmo => Mathf.Max(0, reserveAmmo);
        public float TotalWeight => definition != null ? definition.Weight * Quantity : 0f;

        public ItemInstance CreateSplitInstance(int splitQuantity)
        {
            int moved = Mathf.Clamp(splitQuantity, 1, Quantity);
            quantity -= moved;
            return new ItemInstance(definition, moved)
            {
                durability = durability,
                magazineAmmo = magazineAmmo,
                reserveAmmo = reserveAmmo
            };
        }

        public int AddQuantity(int amount)
        {
            if (definition == null || !definition.IsStackable || amount <= 0)
                return amount;
            int accepted = Mathf.Min(amount, definition.MaximumStack - Quantity);
            quantity += accepted;
            return amount - accepted;
        }

        public bool ConsumeQuantity(int amount)
        {
            if (amount <= 0 || quantity < amount)
                return false;
            quantity -= amount;
            return true;
        }

        public void SetDurability(int value) => durability = Mathf.Max(0, value);

        public int ApplyWear(int amount)
        {
            int previous = Durability;
            durability = Mathf.Max(0, durability - Mathf.Max(0, amount));
            return previous - durability;
        }

        public void Repair()
        {
            if (definition is EquippableDefinition equipment)
                durability = equipment.MaximumDurability;
        }

        public void SetWeaponAmmo(int newMagazineAmmo, int newReserveAmmo)
        {
            magazineAmmo = Mathf.Max(0, newMagazineAmmo);
            reserveAmmo = Mathf.Max(0, newReserveAmmo);
        }
    }
}
