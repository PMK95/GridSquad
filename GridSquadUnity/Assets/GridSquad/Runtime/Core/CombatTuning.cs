using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Combat Tuning", fileName = "CombatTuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("그리드")]
        [Min(0.1f)] public float MovementSpeed = 5f;
        [Min(1f)] public float CharacterRotationSpeed = 720f;
        [Range(0f, 30f)] public float PeekVisualLeanAngle = 14f;

        [Header("사격")]
        [Range(0f, 100f)] public float MaximumCoverEvasionPercent = 60f;
        [Range(1f, 360f)] public float ShootingViewAngle = 360f;
        [Range(0.1f, 30f)] public float FireAimToleranceDegrees = 3f;
        [Range(0f, 100f)] public float MinimumHitChancePercent = 5f;
        [Range(0f, 100f)] public float MaximumHitChancePercent = 95f;
        [Min(0.02f)] public float EvaluationRefreshInterval = 0.1f;

        [Header("피격")]
        [Tooltip("피격 시 현재 행동을 중단하고 피격 애니메이션을 재생할 확률입니다.")]
        [Range(0f, 100f)] public float HitReactionInterruptChancePercent = 25f;

        [Header("적 AI")]
        [Min(0.05f)] public float AiEvaluationInterval = 0.5f;
        [Min(1)] public int AiCandidatePathDistance = 6;
        public float AiShootableScore = 100f;
        public float AiPathCostWeight = 3f;
        public float AiIdealRangeCells = 6f;
        public float AiRangeDifferenceWeight = 2f;
        public float AiMinimumImprovement = 15f;
        [Min(0f)] public float AiMovementCooldown = 1f;

        [Header("카메라")]
        [Min(0.1f)] public float CameraPanSpeed = 30f;
        [Min(0.01f)] public float CameraOrbitSensitivity = 0.15f;
        [Min(0.01f)] public float CameraZoomSensitivity = 0.06f;
        public float CameraMinimumPitch = 30f;
        public float CameraMaximumPitch = 75f;
        public float CameraMinimumDistance = 12f;
        public float CameraMaximumDistance = 32f;
        public float CameraInitialYaw = 45f;
        public float CameraInitialPitch = 55f;
        public float CameraInitialDistance = 24f;
        [Min(0f)] public float CameraBoundsPadding = 2f;
    }
}
