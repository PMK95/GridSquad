using UnityEngine;

namespace GridSquad
{
    public sealed class WeaponMount : MonoBehaviour
    {
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private UnitAnimationController animationController;

        private WeaponPresentation activePresentation;

        public WeaponPresentation ActivePresentation => activePresentation;
        public Transform ActiveMuzzle => activePresentation != null ? activePresentation.Muzzle : null;

        public bool Equip(WeaponDefinition definition, out string failureReason)
        {
            failureReason = string.Empty;
            if (definition == null)
            {
                failureReason = "무기 정의가 없습니다.";
                return false;
            }
            if (definition.PresentationPrefab == null)
            {
                failureReason = $"{definition.DisplayName} 외형 프리팹이 없습니다.";
                return false;
            }
            if (weaponSocket == null)
            {
                failureReason = "WeaponSocket이 지정되지 않았습니다.";
                return false;
            }

            if (activePresentation != null)
            {
                activePresentation.gameObject.SetActive(false);
                Destroy(activePresentation.gameObject);
            }

            activePresentation = Instantiate(definition.PresentationPrefab, weaponSocket, false);
            activePresentation.name = definition.PresentationPrefab.name;
            animationController?.BindWeaponAimTransform(
                activePresentation.GunAim,
                activePresentation.AimAxis);
            return true;
        }

        public void LowerAimForWeaponSwap()
        {
            animationController?.LowerAimForWeaponSwap();
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Transform newWeaponSocket,
            UnitAnimationController newAnimationController)
        {
            weaponSocket = newWeaponSocket;
            animationController = newAnimationController;
        }
#endif
    }
}
