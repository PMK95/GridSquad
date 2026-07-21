using MoreMountains.Feedbacks;
using UnityEngine;

namespace GridSquad
{
    public sealed class WeaponPresentation : MonoBehaviour
    {
        [SerializeField] private Transform gunAim;
        [SerializeField] private Transform muzzle;
        [SerializeField] private MMF_Player fireFeedbacks;
        [SerializeField] private Vector3 aimAxis = Vector3.forward;
        [SerializeField, Min(0f)] private float recoilDistance = 0.045f;
        [SerializeField, Min(0.01f)] private float recoilDuration = 0.09f;

        private Vector3 restLocalPosition;
        private float recoilRemainingSeconds;

        public Transform GunAim => gunAim != null ? gunAim : transform;
        public Transform Muzzle => muzzle != null ? muzzle : GunAim;
        public Vector3 AimAxis => aimAxis.sqrMagnitude > 0.0001f ? aimAxis.normalized : Vector3.forward;

        private void Awake()
        {
            restLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            recoilRemainingSeconds = Mathf.Max(0f, recoilRemainingSeconds - Time.deltaTime);
            float ratio = recoilRemainingSeconds / Mathf.Max(0.01f, recoilDuration);
            transform.localPosition = restLocalPosition - Vector3.forward * (recoilDistance * ratio);
        }

        public void PlayFireFeedback()
        {
            fireFeedbacks?.PlayFeedbacks(Muzzle.position);
            recoilRemainingSeconds = recoilDuration;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Transform newGunAim,
            Transform newMuzzle,
            MMF_Player newFireFeedbacks,
            Vector3 newAimAxis)
        {
            gunAim = newGunAim;
            muzzle = newMuzzle;
            fireFeedbacks = newFireFeedbacks;
            aimAxis = newAimAxis;
        }
#endif
    }
}
