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
        private const int SlotCount = 2;

        [SerializeField] private WeaponDefinition[] weaponSlots = new WeaponDefinition[SlotCount];
        [SerializeField, Range(0, SlotCount - 1)] private int activeSlotIndex;
        [SerializeField] private WeaponMount weaponMount;

        private readonly WeaponAmmoState[] ammoStates = new WeaponAmmoState[SlotCount];
        private bool battleInitialized;

        public int ActiveSlotIndex => activeSlotIndex;
        public WeaponDefinition ActiveDefinition => GetDefinition(activeSlotIndex);
        public WeaponMount Mount => weaponMount;
        public bool IsBattleInitialized => battleInitialized;

        public WeaponDefinition GetDefinition(int slotIndex)
        {
            return IsValidSlot(slotIndex) && weaponSlots != null && weaponSlots.Length == SlotCount
                ? weaponSlots[slotIndex]
                : null;
        }

        public WeaponAmmoState GetAmmoState(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? ammoStates[slotIndex] : default;
        }

        public int GetNextSlotIndex() => (activeSlotIndex + 1) % SlotCount;

        public void ApplyUnitDefinitionDefaults(UnitDefinition unitDefinition)
        {
            if (battleInitialized || unitDefinition == null)
                return;

            EnsureSlotArray();
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
                weaponSlots[slotIndex] = unitDefinition.GetDefaultWeapon(slotIndex);
            activeSlotIndex = 0;
        }

        public bool SetDefinitionBeforeBattle(int slotIndex, WeaponDefinition definition)
        {
            if (battleInitialized || !IsValidSlot(slotIndex) || definition == null)
                return false;

            EnsureSlotArray();
            int otherSlotIndex = (slotIndex + 1) % SlotCount;
            if (weaponSlots[otherSlotIndex] == definition)
            {
                WeaponDefinition previous = weaponSlots[slotIndex];
                weaponSlots[slotIndex] = definition;
                weaponSlots[otherSlotIndex] = previous;
                return true;
            }

            weaponSlots[slotIndex] = definition;
            return true;
        }

        public bool InitializeForBattle(out string failureReason)
        {
            failureReason = string.Empty;
            EnsureSlotArray();
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                WeaponDefinition definition = weaponSlots[slotIndex];
                if (definition == null)
                {
                    failureReason = $"{name}의 무기 슬롯 {slotIndex + 1}이 비어 있습니다.";
                    return false;
                }

                ammoStates[slotIndex] = new WeaponAmmoState
                {
                    MagazineAmmo = Mathf.Max(1, definition.MagazineCapacity),
                    ReserveAmmo = Mathf.Max(0, definition.StartingReserveAmmo)
                };
            }

            activeSlotIndex = 0;
            battleInitialized = true;
            return EquipPresentation(activeSlotIndex, out failureReason);
        }

        public bool EquipSlot(int slotIndex, out string failureReason)
        {
            failureReason = string.Empty;
            if (!battleInitialized)
            {
                failureReason = "전투가 시작되지 않았습니다.";
                return false;
            }
            if (!IsValidSlot(slotIndex) || GetDefinition(slotIndex) == null)
            {
                failureReason = "교체할 무기 슬롯이 올바르지 않습니다.";
                return false;
            }
            if (slotIndex == activeSlotIndex)
            {
                failureReason = "이미 장착한 무기입니다.";
                return false;
            }

            int previousSlotIndex = activeSlotIndex;
            activeSlotIndex = slotIndex;
            if (EquipPresentation(slotIndex, out failureReason))
                return true;

            activeSlotIndex = previousSlotIndex;
            return false;
        }

        public void StoreActiveAmmo(int magazineAmmo, int reserveAmmo)
        {
            ammoStates[activeSlotIndex] = new WeaponAmmoState
            {
                MagazineAmmo = Mathf.Max(0, magazineAmmo),
                ReserveAmmo = Mathf.Max(0, reserveAmmo)
            };
        }

        private bool EquipPresentation(int slotIndex, out string failureReason)
        {
            if (weaponMount == null)
            {
                failureReason = "WeaponMount가 지정되지 않았습니다.";
                return false;
            }
            return weaponMount.Equip(GetDefinition(slotIndex), out failureReason);
        }

        private void EnsureSlotArray()
        {
            if (weaponSlots != null && weaponSlots.Length == SlotCount)
                return;

            WeaponDefinition[] previous = weaponSlots;
            weaponSlots = new WeaponDefinition[SlotCount];
            if (previous == null)
                return;
            for (int index = 0; index < Mathf.Min(previous.Length, SlotCount); index++)
                weaponSlots[index] = previous[index];
        }

        private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            WeaponDefinition firstWeapon,
            WeaponDefinition secondWeapon,
            WeaponMount newWeaponMount)
        {
            weaponSlots = new[] { firstWeapon, secondWeapon };
            activeSlotIndex = 0;
            weaponMount = newWeaponMount;
        }
#endif
    }
}
