using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class SelectionUiRuntimeBootstrap : MonoBehaviour
    {
        private const string SelectionUiResourcePath = "UI/SelectionUiRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateSelectionUiForLoadedCombatScene()
        {
            if (FindFirstObjectByType<SelectionInspectController>() != null)
                return;
            TacticalInputController inputController = FindFirstObjectByType<TacticalInputController>();
            if (inputController == null)
                return;
            GameObject prefab = Resources.Load<GameObject>(SelectionUiResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[선택 UI] Resources/UI/SelectionUiRoot 프리팹을 찾을 수 없습니다.");
                return;
            }
            Canvas canvas = FindFirstObjectByType<CombatHudController>()?.GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[선택 UI] 선택 UI를 배치할 Canvas를 찾을 수 없습니다.");
                return;
            }
            GameObject root = Instantiate(prefab, canvas.transform, false);
            root.name = prefab.name;
            ContextFloatingMenuController menu = root.GetComponentInChildren<ContextFloatingMenuController>(true);
            SelectionDetailWindowController detail = root.GetComponentInChildren<SelectionDetailWindowController>(true);
            inputController.SetRuntimeContextUi(menu, detail);
        }
    }
}
