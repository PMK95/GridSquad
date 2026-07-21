using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GridSquad
{
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Transform rigTarget;
        [SerializeField] private Transform outputCamera;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private CombatTuning tuning;

        private CinemachineOrbitalFollow orbitalFollow;
        private InputActionMap tacticalMap;

        private void Awake()
        {
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            tacticalMap = inputActions.FindActionMap("Tactical", true);
            CinemachineBrain outputBrain = outputCamera.GetComponent<CinemachineBrain>();
            if (outputBrain != null)
                outputBrain.IgnoreTimeScale = true;
            orbitalFollow.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbitalFollow.HorizontalAxis.Wrap = true;
            orbitalFollow.VerticalAxis.Range = new Vector2(tuning.CameraMinimumPitch, tuning.CameraMaximumPitch);
            orbitalFollow.VerticalAxis.Wrap = false;
            orbitalFollow.HorizontalAxis.Value = tuning.CameraInitialYaw;
            orbitalFollow.VerticalAxis.Value = tuning.CameraInitialPitch;
            orbitalFollow.Radius = tuning.CameraInitialDistance;
        }

        private void OnEnable() => tacticalMap?.Enable();
        private void OnDisable()
        {
            tacticalMap?.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            PanCamera();
            OrbitCamera();
            ZoomCamera();
        }

        private void PanCamera()
        {
            Vector2 move = tacticalMap.FindAction("CameraMove", true).ReadValue<Vector2>();
            if (move.sqrMagnitude < 0.001f)
                return;

            Vector3 forward = outputCamera.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = outputCamera.right;
            right.y = 0f;
            right.Normalize();
            Vector3 moveDirection = right * move.x + forward * move.y;
            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();
            rigTarget.position += moveDirection * tuning.CameraPanSpeed * Time.unscaledDeltaTime;
            ClampRigTargetToBattlefield();
        }

        private void OrbitCamera()
        {
            bool rotating = tacticalMap.FindAction("CameraOrbit", true).IsPressed();
            if (!rotating)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Vector2 delta = tacticalMap.FindAction("PointerDelta", true).ReadValue<Vector2>();
            orbitalFollow.HorizontalAxis.Value += delta.x * tuning.CameraOrbitSensitivity;
            orbitalFollow.VerticalAxis.Value = Mathf.Clamp(
                orbitalFollow.VerticalAxis.Value - delta.y * tuning.CameraOrbitSensitivity,
                tuning.CameraMinimumPitch,
                tuning.CameraMaximumPitch);
        }

        private void ZoomCamera()
        {
            Vector2 scroll = tacticalMap.FindAction("CameraZoom", true).ReadValue<Vector2>();
            if (Mathf.Abs(scroll.y) < 0.01f)
                return;
            orbitalFollow.Radius = Mathf.Clamp(
                orbitalFollow.Radius - scroll.y * tuning.CameraZoomSensitivity,
                tuning.CameraMinimumDistance,
                tuning.CameraMaximumDistance);
        }

        private void ClampRigTargetToBattlefield()
        {
            Vector3 position = rigTarget.position;
            float padding = tuning.CameraBoundsPadding;
            position.x = Mathf.Clamp(position.x, gridMap.Origin.x - padding, gridMap.Origin.x + gridMap.Width * gridMap.CellSize + padding);
            position.z = Mathf.Clamp(position.z, gridMap.Origin.z - padding, gridMap.Origin.z + gridMap.Height * gridMap.CellSize + padding);
            rigTarget.position = position;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            InputActionAsset newInputActions,
            Transform newRigTarget,
            Transform newOutputCamera,
            GridMap newGridMap,
            CombatTuning newTuning)
        {
            inputActions = newInputActions;
            rigTarget = newRigTarget;
            outputCamera = newOutputCamera;
            gridMap = newGridMap;
            tuning = newTuning;
        }
#endif
    }
}
