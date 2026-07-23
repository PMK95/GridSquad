using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public static class SelectionUiRuntimeBootstrap
    {
        private const string SelectionUiResourcePath = "UI/SelectionUiRoot";

        public static SelectionInspectController CreateOrGetSelectionUi(
            TacticalInputController inputController,
            CombatHudController combatHud)
        {
            if (inputController == null || combatHud == null)
                return null;
            SelectionInspectController existing = FindSelectionUiInScene(
                combatHud.gameObject.scene);
            if (existing != null)
            {
                ConnectSelectionUi(existing, inputController, combatHud);
                return existing;
            }
            GameObject prefab = Resources.Load<GameObject>(SelectionUiResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[선택 UI] Resources/UI/SelectionUiRoot 프리팹을 찾을 수 없습니다.");
                return null;
            }
            Canvas canvas = combatHud.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[선택 UI] 선택 UI를 배치할 Canvas를 찾을 수 없습니다.");
                return null;
            }
            GameObject root = Object.Instantiate(prefab, canvas.transform, false);
            root.name = prefab.name;
            SelectionInspectController created =
                root.GetComponentInChildren<SelectionInspectController>(true);
            ConnectSelectionUi(created, inputController, combatHud);
            return created;
        }

        private static void ConnectSelectionUi(
            SelectionInspectController inspect,
            TacticalInputController inputController,
            CombatHudController combatHud)
        {
            if (inspect == null)
                return;
            Canvas canvas = inspect.GetComponentInParent<Canvas>();
            ContextFloatingMenuController menu =
                canvas?.GetComponentInChildren<ContextFloatingMenuController>(true);
            SelectionDetailWindowController detail =
                canvas?.GetComponentInChildren<SelectionDetailWindowController>(true);
            inputController.SetRuntimeContextUi(menu, detail);
            inspect.InitializeRuntime(inputController);
            DeveloperInventoryPanelController developerPanel =
                canvas?.GetComponentInChildren<DeveloperInventoryPanelController>(true);
            developerPanel?.InitializeRuntime(inputController);
            combatHud?.SetRuntimeCommandMessageText(
                FindTextByName(canvas, "CommandMessage"));
        }

        private static Text FindTextByName(Canvas canvas, string objectName)
        {
            if (canvas == null)
                return null;
            foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
                if (text != null && text.name == objectName)
                    return text;
            return null;
        }

        private static SelectionInspectController FindSelectionUiInScene(
            UnityEngine.SceneManagement.Scene scene)
        {
            SelectionInspectController[] controllers =
                Object.FindObjectsByType<SelectionInspectController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (SelectionInspectController controller in controllers)
            {
                if (controller != null && controller.gameObject.scene == scene)
                    return controller;
            }
            return null;
        }
    }
}
