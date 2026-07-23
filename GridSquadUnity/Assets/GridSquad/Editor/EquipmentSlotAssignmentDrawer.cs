using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GridSquad.Editor
{
    [CustomPropertyDrawer(typeof(EquipmentSlotAssignment))]
    public sealed class EquipmentSlotAssignmentDrawer : PropertyDrawer
    {
        private const float LineGap = 2f;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + LineGap;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty slotProperty = property.FindPropertyRelative("slot");
            SerializedProperty equipmentProperty = property.FindPropertyRelative("equipment");
            Rect slotRect = new(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            Rect equipmentRect = new(
                position.x,
                slotRect.yMax + LineGap,
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(slotRect, slotProperty, new GUIContent("슬롯"));
            if (EditorGUI.EndChangeCheck())
            {
                EquipmentSlotDefinition changedSlot =
                    slotProperty.objectReferenceValue as EquipmentSlotDefinition;
                EquippableDefinition currentEquipment =
                    equipmentProperty.objectReferenceValue as EquippableDefinition;
                if (currentEquipment != null
                    && !EquipmentSlotCompatibility.CanAssign(changedSlot, currentEquipment))
                {
                    equipmentProperty.objectReferenceValue = null;
                }
            }

            DrawCompatibleEquipmentDropdown(equipmentRect, slotProperty, equipmentProperty);
            EditorGUI.EndProperty();
        }

        private static void DrawCompatibleEquipmentDropdown(
            Rect position,
            SerializedProperty slotProperty,
            SerializedProperty equipmentProperty)
        {
            EquipmentSlotDefinition slot =
                slotProperty.objectReferenceValue as EquipmentSlotDefinition;
            EquippableDefinition equipment =
                equipmentProperty.objectReferenceValue as EquippableDefinition;
            Rect buttonRect = EditorGUI.PrefixLabel(position, new GUIContent("장비"));

            using (new EditorGUI.DisabledScope(slot == null))
            {
                string buttonLabel = slot == null
                    ? "슬롯을 먼저 할당하세요"
                    : equipment != null ? equipment.DisplayName : "없음";
                if (!EditorGUI.DropdownButton(
                        buttonRect,
                        new GUIContent(buttonLabel),
                        FocusType.Keyboard))
                {
                    return;
                }

                ShowCompatibleEquipmentMenu(buttonRect, slot, equipmentProperty);
            }
        }

        private static void ShowCompatibleEquipmentMenu(
            Rect buttonRect,
            EquipmentSlotDefinition slot,
            SerializedProperty equipmentProperty)
        {
            GenericMenu menu = new();
            AddEquipmentMenuItem(menu, "없음", null, equipmentProperty);
            menu.AddSeparator(string.Empty);

            List<EquippableDefinition> compatibleEquipment = FindCompatibleEquipment(slot);
            if (compatibleEquipment.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("호환 장비가 없습니다"));
            }
            else
            {
                foreach (EquippableDefinition equipment in compatibleEquipment)
                {
                    string menuLabel = $"{equipment.DisplayName} ({equipment.name})";
                    AddEquipmentMenuItem(menu, menuLabel, equipment, equipmentProperty);
                }
            }

            menu.DropDown(buttonRect);
        }

        private static List<EquippableDefinition> FindCompatibleEquipment(
            EquipmentSlotDefinition slot)
        {
            List<EquippableDefinition> results = new();
            string[] assetGuids = AssetDatabase.FindAssets("t:EquippableDefinition");
            foreach (string assetGuid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                EquippableDefinition equipment =
                    AssetDatabase.LoadAssetAtPath<EquippableDefinition>(assetPath);
                if (EquipmentSlotCompatibility.CanAssign(slot, equipment))
                    results.Add(equipment);
            }

            results.Sort((left, right) =>
            {
                int displayNameComparison = string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.CurrentCulture);
                return displayNameComparison != 0
                    ? displayNameComparison
                    : string.Compare(left.name, right.name, StringComparison.Ordinal);
            });
            return results;
        }

        private static void AddEquipmentMenuItem(
            GenericMenu menu,
            string menuLabel,
            EquippableDefinition equipment,
            SerializedProperty equipmentProperty)
        {
            bool selected = equipmentProperty.objectReferenceValue == equipment;
            menu.AddItem(new GUIContent(menuLabel), selected, () =>
            {
                equipmentProperty.serializedObject.Update();
                equipmentProperty.objectReferenceValue = equipment;
                equipmentProperty.serializedObject.ApplyModifiedProperties();
            });
        }
    }
}
