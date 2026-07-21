using MoreMountains.Feedbacks;
using UnityEngine;

namespace GridSquad
{
    public sealed class CombatantFeedbackPresenter : MonoBehaviour
    {
        [Header("사격 연출")]
        [SerializeField] private MMF_Player shotFeedbacks;
        [SerializeField] private LineRenderer shotTracer;
        [SerializeField, Min(0.001f)] private float shotTracerWidth = 0.075f;

        [Header("피격 연출")]
        [SerializeField] private MMF_Player hitFeedbacks;
        [SerializeField] private MMF_Player damageTextFeedbacks;
        [SerializeField] private MMF_Player missTextFeedbacks;

        [Header("사망 연출")]
        [SerializeField] private MMF_Player deathVisualFeedbacks;
        [SerializeField] private MMF_Player deathShakeFeedbacks;
        [SerializeField] private MMF_Player defeatShakeFeedbacks;

        private void Awake()
        {
            if (shotTracer != null)
                shotTracer.gameObject.SetActive(false);
        }

        public void PlayShotFeedback(Vector3 startPosition, Vector3 endPosition)
        {
            if (shotTracer != null)
            {
                shotTracer.SetPosition(0, startPosition);
                shotTracer.SetPosition(1, endPosition);
                shotTracer.widthCurve = AnimationCurve.Constant(0f, 1f, shotTracerWidth);
            }

            shotFeedbacks?.PlayFeedbacks(startPosition);
        }

        public void PlayDamageFeedback(int appliedDamage, Vector3 worldPosition)
        {
            if (appliedDamage <= 0)
                return;

            hitFeedbacks?.PlayFeedbacks(worldPosition);
            damageTextFeedbacks?.PlayFeedbacks(worldPosition, appliedDamage);
        }

        public void PlayMissFeedback(Vector3 worldPosition)
        {
            missTextFeedbacks?.PlayFeedbacks(worldPosition);
        }

        public void PlayDeathFeedback(BattleResult battleResult, Vector3 worldPosition)
        {
            deathVisualFeedbacks?.PlayFeedbacks(worldPosition);
            if (battleResult == BattleResult.Defeat)
                defeatShakeFeedbacks?.PlayFeedbacks(worldPosition);
            else
                deathShakeFeedbacks?.PlayFeedbacks(worldPosition);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            MMF_Player newShotFeedbacks,
            LineRenderer newShotTracer,
            MMF_Player newHitFeedbacks,
            MMF_Player newDamageTextFeedbacks,
            MMF_Player newMissTextFeedbacks,
            MMF_Player newDeathVisualFeedbacks,
            MMF_Player newDeathShakeFeedbacks,
            MMF_Player newDefeatShakeFeedbacks)
        {
            shotFeedbacks = newShotFeedbacks;
            shotTracer = newShotTracer;
            hitFeedbacks = newHitFeedbacks;
            damageTextFeedbacks = newDamageTextFeedbacks;
            missTextFeedbacks = newMissTextFeedbacks;
            deathVisualFeedbacks = newDeathVisualFeedbacks;
            deathShakeFeedbacks = newDeathShakeFeedbacks;
            defeatShakeFeedbacks = newDefeatShakeFeedbacks;
        }
#endif
    }
}
