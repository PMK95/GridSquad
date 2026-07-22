using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class WeaponRuntimeController : MonoBehaviour
    {
        private Combatant owner;
        private WeaponLoadout weaponLoadout;
        private int currentMagazineAmmo;
        private int reserveAmmo;

        public int CurrentMagazineAmmo => Weapon != null && !Weapon.UsesAmmo ? 1 : currentMagazineAmmo;
        public int ReserveAmmo => Weapon != null && !Weapon.UsesAmmo ? 0 : reserveAmmo;
        public int MagazineCapacity => Weapon != null && !Weapon.UsesAmmo ? 1 : Weapon != null ? Mathf.Max(1, Weapon.MagazineCapacity) : 1;
        public WeaponDefinition Weapon => owner != null ? owner.Weapon : null;
        public WeaponLoadout Loadout => weaponLoadout;

        public void Initialize(Combatant newOwner, WeaponLoadout newWeaponLoadout)
        {
            owner = newOwner;
            weaponLoadout = newWeaponLoadout;
            currentMagazineAmmo = MagazineCapacity;
            reserveAmmo = Weapon != null ? Mathf.Max(0, Weapon.StartingReserveAmmo) : 0;
        }

        public bool InitializeLoadoutForBattle(out string failureReason)
        {
            failureReason = string.Empty;
            if (weaponLoadout == null)
                return true;
            if (!weaponLoadout.InitializeForBattle(out failureReason))
                return false;
            LoadActiveWeaponState();
            return true;
        }

        public void RefreshEquippedWeapon()
        {
            LoadActiveWeaponState();
        }

        public bool TryConsumeRound()
        {
            if (Weapon != null && !Weapon.UsesAmmo)
                return true;
            if (currentMagazineAmmo <= 0)
                return false;
            currentMagazineAmmo--;
            StoreActiveAmmo();
            return true;
        }

        public int CalculateReloadAmount()
        {
            if (Weapon != null && !Weapon.UsesAmmo)
                return 0;
            return Mathf.Min(MagazineCapacity - currentMagazineAmmo, reserveAmmo);
        }

        public void CommitReload(int amount)
        {
            int applied = Mathf.Clamp(amount, 0, CalculateReloadAmount());
            currentMagazineAmmo += applied;
            reserveAmmo -= applied;
            StoreActiveAmmo();
        }

        private void LoadActiveWeaponState()
        {
            owner.SetLegacyWeaponDefinition(weaponLoadout.ActiveDefinition);
            WeaponAmmoState state = weaponLoadout.GetAmmoState(weaponLoadout.ActiveSlotIndex);
            bool hasItemInstance = weaponLoadout.ActiveItemInstance != null;
            currentMagazineAmmo = hasItemInstance
                ? state.MagazineAmmo
                : Weapon != null ? Mathf.Max(1, Weapon.MagazineCapacity) : 0;
            reserveAmmo = hasItemInstance
                ? state.ReserveAmmo
                : Weapon != null ? Mathf.Max(0, Weapon.StartingReserveAmmo) : 0;
            StoreActiveAmmo();
        }

        private void StoreActiveAmmo()
        {
            weaponLoadout?.StoreActiveAmmo(currentMagazineAmmo, reserveAmmo);
        }
    }
}
