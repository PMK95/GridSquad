using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad
{
    public sealed class WeaponLoadoutPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private Button startButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Font font;
        [SerializeField] private WeaponCatalog weaponCatalog;
        [SerializeField] private CombatDirector director;

        private readonly List<UnitRow> rows = new();

        private sealed class UnitRow
        {
            public Combatant Combatant;
            public Text[] SlotTexts;
        }

        private void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(HandleStartClicked);
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartClicked);
        }

        public void Show()
        {
            panelRoot?.SetActive(true);
            RebuildRows();
            SetStatusMessage("각 유닛의 두 무기를 지정한 뒤 START를 누르세요.");
        }

        public void Hide()
        {
            panelRoot?.SetActive(false);
        }

        public void SetStatusMessage(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private void RebuildRows()
        {
            if (rowContainer == null || director == null || weaponCatalog == null)
                return;

            if (rowContainer.TryGetComponent(out VerticalLayoutGroup rowLayout))
            {
                rowLayout.spacing = 14f;
                rowLayout.childControlWidth = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandHeight = false;
            }

            for (int childIndex = rowContainer.childCount - 1; childIndex >= 0; childIndex--)
                Destroy(rowContainer.GetChild(childIndex).gameObject);
            rows.Clear();

            int unitIndex = 0;
            foreach (Combatant combatant in director.Combatants)
            {
                if (combatant == null
                    || combatant.Team != Team.Ally
                    || combatant.WeaponLoadout == null)
                    continue;
                CreateUnitRow(combatant, unitIndex++);
            }
        }

        private void CreateUnitRow(Combatant combatant, int unitIndex)
        {
            GameObject rowObject = CreateUiObject($"UnitRow_{unitIndex + 1}", rowContainer);
            HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 86f;

            Text unitText = CreateText("Unit", rowObject.transform, 16, TextAnchor.MiddleLeft);
            unitText.text = $"{combatant.DisplayName}  [{combatant.RoleName}]";
            LayoutElement unitLayout = unitText.gameObject.AddComponent<LayoutElement>();
            unitLayout.minWidth = 240f;
            unitLayout.preferredWidth = 280f;

            UnitRow row = new()
            {
                Combatant = combatant,
                SlotTexts = new Text[2]
            };
            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                int capturedSlotIndex = slotIndex;
                Button button = CreateButton($"Slot{slotIndex + 1}", rowObject.transform, out Text buttonText);
                button.onClick.AddListener(() => CycleWeapon(row, capturedSlotIndex));
                row.SlotTexts[slotIndex] = buttonText;
            }
            rows.Add(row);
            RefreshRow(row);
        }

        private void CycleWeapon(UnitRow row, int slotIndex)
        {
            IReadOnlyList<WeaponDefinition> weapons = weaponCatalog.Weapons;
            if (weapons == null || weapons.Count == 0)
            {
                SetStatusMessage("선택 가능한 무기가 없습니다.");
                return;
            }

            WeaponDefinition current = row.Combatant.WeaponLoadout.GetDefinition(slotIndex);
            int currentIndex = -1;
            for (int index = 0; index < weapons.Count; index++)
            {
                if (weapons[index] == current)
                {
                    currentIndex = index;
                    break;
                }
            }
            WeaponDefinition next = weapons[(currentIndex + 1 + weapons.Count) % weapons.Count];
            if (!row.Combatant.WeaponLoadout.SetDefinitionBeforeBattle(slotIndex, next))
            {
                SetStatusMessage("전투 시작 후에는 준비 로드아웃을 바꿀 수 없습니다.");
                return;
            }

            RefreshRow(row);
            SetStatusMessage($"{row.Combatant.DisplayName} 무기 구성을 변경했습니다.");
        }

        private static void RefreshRow(UnitRow row)
        {
            for (int slotIndex = 0; slotIndex < row.SlotTexts.Length; slotIndex++)
            {
                WeaponDefinition definition = row.Combatant.WeaponLoadout.GetDefinition(slotIndex);
                row.SlotTexts[slotIndex].text = $"SLOT {slotIndex + 1}\n{(definition != null ? definition.DisplayName : "-")}";
            }
        }

        private void HandleStartClicked()
        {
            director?.StartBattleWithCurrentLoadouts();
        }

        private GameObject CreateUiObject(string objectName, Transform parent)
        {
            GameObject created = new(objectName, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        private Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = CreateUiObject(objectName, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private Button CreateButton(string objectName, Transform parent, out Text buttonText)
        {
            GameObject buttonObject = CreateUiObject(objectName, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.22f, 0.3f, 0.98f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
            buttonLayout.minWidth = 210f;
            buttonLayout.preferredWidth = 260f;
            buttonText = CreateText("Text", buttonObject.transform, 15, TextAnchor.MiddleCenter);
            RectTransform textRect = buttonText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            GameObject newPanelRoot,
            RectTransform newRowContainer,
            Button newStartButton,
            Text newStatusText,
            Font newFont,
            WeaponCatalog newWeaponCatalog,
            CombatDirector newDirector)
        {
            panelRoot = newPanelRoot;
            rowContainer = newRowContainer;
            startButton = newStartButton;
            statusText = newStatusText;
            font = newFont;
            weaponCatalog = newWeaponCatalog;
            director = newDirector;
        }
#endif
    }
}
