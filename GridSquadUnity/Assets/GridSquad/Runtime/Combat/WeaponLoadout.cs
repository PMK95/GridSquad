using System;
using UnityEngine;

namespace GridSquad
{
    [Serializable]
    public struct WeaponAmmoState
    {
        public int MagazineAmmo;
        public int ReserveAmmo;
        public int TotalAmmo => Mathf.Max(0, MagazineAmmo) + Mathf.Max(0, ReserveAmmo);
    }

    public sealed class WeaponLoadout : MonoBehaviour
    {
        [SerializeField] private WeaponMount weaponMount;
        [SerializeField] private EquipmentLoadout equipmentLoadout;

        private bool battleInitialized;

        public int ActiveSlotIndex => 0;
        public WeaponDefinition ActiveDefinition => GetDefinition(0);
        public ItemInstance ActiveItemInstance => equipmentLoadout?.GetItemInstance(
            equipmentLoadout.GetSlot(EquipmentSlotKind.LeftHand));
        public WeaponMount Mount => weaponMount;
        public bool IsBattleInitialized => battleInitialized;

        private void Awake()
        {
            equipmentLoadout = equipmentLoadout != null
                ? equipmentLoadout
                : GetComponent<EquipmentLoadout>();
        }

        public WeaponDefinition GetDefinition(int slotIndex)
        {
            if (slotIndex != 0)
                return null;
            EquipmentSlotDefinition leftHand = equipmentLoadout?.GetSlot(EquipmentSlotKind.LeftHand);
            WeaponDefinition equipped = equipmentLoadout?.GetItemInstance(leftHand)?.Definition as WeaponDefinition;
            return equipped;
        }

        public WeaponAmmoState GetAmmoState(int slotIndex)
        {
            if (slotIndex != 0 || ActiveItemInstance == null)
                return default;
            return new WeaponAmmoState
            {
                MagazineAmmo = ActiveItemInstance.MagazineAmmo,
                ReserveAmmo = ActiveItemInstance.ReserveAmmo
            };
        }

        public void ApplyUnitDefinitionDefaults(UnitDefinition unitDefinition)
        {
            GetComponent<UnitInventory>()?.ApplyUnitDefinitionDefaults(unitDefinition);
        }

        public bool InitializeForBattle(out string failureReason)
        {
            battleInitialized = true;
            if (ActiveDefinition == null)
            {
                failureReason = $"{name}의 왼손 무기 슬롯이 비어 있습니다.";
                return false;
            }
            return EquipPresentation(out failureReason);
        }

        public void RefreshEquippedWeapon()
        {
            if (!battleInitialized)
                return;
            EquipPresentation(out _);
        }

        public void StoreActiveAmmo(int magazineAmmo, int reserveAmmo)
        {
            ActiveItemInstance?.SetWeaponAmmo(magazineAmmo, reserveAmmo);
        }

        private bool EquipPresentation(out string failureReason)
        {
            if (weaponMount == null)
            {
                failureReason = "WeaponMount가 지정되지 않았습니다.";
                return false;
            }
            return weaponMount.Equip(ActiveDefinition, out failureReason);
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(WeaponMount newWeaponMount)
        {
            weaponMount = newWeaponMount;
        }

        public void SetEditorEquipmentLoadout(EquipmentLoadout newEquipmentLoadout)
            => equipmentLoadout = newEquipmentLoadout;
#endif
    }
}
