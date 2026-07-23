using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class LoadingScreenPresentationController : MonoBehaviour
    {
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private RectTransform spinner;
        [SerializeField] private Font sourceFont;
        [SerializeField, Min(0.25f)] private float tipChangeInterval = 3f;
        [SerializeField] private float spinnerRotationSpeed = 180f;
        [SerializeField] private string[] tips =
        {
            "엄폐물은 사격선을 막고 피격 확률을 낮춰 줍니다.",
            "전투 중 사용한 소모품은 기지로 돌아와도 복구되지 않습니다.",
            "장비 내구도가 낮아지면 다음 임무 전에 수리하는 것이 좋습니다.",
            "받은 총피해가 많을수록 임무 복귀 후 후유증 위험이 커집니다.",
            "선발한 대원과 장비 상태는 임무의 모든 스테이지에 이어집니다.",
            "위험할 때는 다음 스테이지로 가기보다 철수를 선택할 수 있습니다.",
            "유닛을 선택하면 하단 패널에서 스탯, 장비와 특성을 확인할 수 있습니다.",
            "행동 아이콘에 마우스를 올리면 사거리와 사용 조건을 확인할 수 있습니다."
        };

        private readonly List<int> shuffledTipIndexes = new();
        private bool runtimeInitialized;
        private float nextTipChangeTime;
        private int shuffledTipCursor;
        private TMP_FontAsset runtimeFontAsset;

        public void InitializeRuntime()
        {
            if (runtimeInitialized)
                return;
            runtimeInitialized = true;
            if (tipText != null && sourceFont != null)
            {
                runtimeFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
                tipText.font = runtimeFontAsset;
            }
            RebuildShuffledTipIndexes();
            ShowNextTip();
            nextTipChangeTime = Time.unscaledTime + tipChangeInterval;
        }

        private void Update()
        {
            if (!runtimeInitialized)
                return;

            if (spinner != null)
            {
                spinner.Rotate(
                    0f,
                    0f,
                    -spinnerRotationSpeed * Time.unscaledDeltaTime);
            }

            if (Time.unscaledTime < nextTipChangeTime)
                return;
            ShowNextTip();
            nextTipChangeTime = Time.unscaledTime + tipChangeInterval;
        }

        private void ShowNextTip()
        {
            if (tipText == null || tips == null || tips.Length == 0)
                return;
            if (shuffledTipCursor >= shuffledTipIndexes.Count)
                RebuildShuffledTipIndexes();
            tipText.text = tips[shuffledTipIndexes[shuffledTipCursor]];
            shuffledTipCursor++;
        }

        private void RebuildShuffledTipIndexes()
        {
            shuffledTipIndexes.Clear();
            if (tips == null)
                return;
            for (int index = 0; index < tips.Length; index++)
                shuffledTipIndexes.Add(index);
            for (int index = shuffledTipIndexes.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                (shuffledTipIndexes[index], shuffledTipIndexes[swapIndex]) =
                    (shuffledTipIndexes[swapIndex], shuffledTipIndexes[index]);
            }
            shuffledTipCursor = 0;
        }

        private void OnDestroy()
        {
            if (runtimeFontAsset != null)
                Destroy(runtimeFontAsset);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            TMP_Text newTipText,
            RectTransform newSpinner,
            Font newSourceFont = null)
        {
            tipText = newTipText;
            spinner = newSpinner;
            sourceFont = newSourceFont;
        }
#endif
    }
}
