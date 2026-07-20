using System;
using System.Collections.Generic;
using GridSquad;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GridSquadEditor
{
    [CustomEditor(typeof(GridMap))]
    public sealed class GridMapEditor : Editor
    {
        private const int GroundLayer = 8;
        private const int CoverLayer = 10;
        private const string MaterialRoot = "Assets/GridSquad/Materials";

        private SerializedProperty widthProperty;
        private SerializedProperty heightProperty;
        private SerializedProperty cellSizeProperty;
        private SerializedProperty originProperty;
        private SerializedProperty blockedCellsProperty;
        private SerializedProperty randomBlockedCellCountProperty;
        private SerializedProperty randomSeedProperty;

        private void OnEnable()
        {
            widthProperty = serializedObject.FindProperty("width");
            heightProperty = serializedObject.FindProperty("height");
            cellSizeProperty = serializedObject.FindProperty("cellSize");
            originProperty = serializedObject.FindProperty("origin");
            blockedCellsProperty = serializedObject.FindProperty("blockedCells");
            randomBlockedCellCountProperty = serializedObject.FindProperty("editorRandomBlockedCellCount");
            randomSeedProperty = serializedObject.FindProperty("editorRandomSeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("그리드 맵 생성 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(widthProperty, new GUIContent("가로 셀 수"));
            EditorGUILayout.PropertyField(heightProperty, new GUIContent("세로 셀 수"));
            EditorGUILayout.PropertyField(cellSizeProperty, new GUIContent("셀 크기"));
            EditorGUILayout.PropertyField(originProperty, new GUIContent("원점"));
            EditorGUILayout.PropertyField(blockedCellsProperty, new GUIContent("블럭 셀 목록"), true);

            int maximumCellCount = Mathf.Max(0, widthProperty.intValue * heightProperty.intValue);
            randomBlockedCellCountProperty.intValue = EditorGUILayout.IntSlider(
                "랜덤 블럭 개수",
                randomBlockedCellCountProperty.intValue,
                0,
                maximumCellCount);
            EditorGUILayout.PropertyField(randomSeedProperty, new GUIContent("다음 랜덤 시드"));

            serializedObject.ApplyModifiedProperties();

            GridMap gridMap = (GridMap)target;
            bool hasOutOfBoundsCombatant = HasOutOfBoundsCombatant(gridMap);
            if (hasOutOfBoundsCombatant)
                EditorGUILayout.HelpBox("현재 맵 크기 밖에 있는 유닛이 있습니다. 맵 적용 후 유닛 위치를 확인하세요.", MessageType.Warning);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("맵 생성 기능은 Play Mode가 아닐 때만 사용할 수 있습니다.", MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("현재 설정으로 맵과 비주얼 다시 생성", GUILayout.Height(30f)))
                    RebuildMapUsingSerializedBlockedCells(gridMap);
                if (GUILayout.Button("랜덤 블럭 배치 후 다시 생성", GUILayout.Height(30f)))
                    RebuildMapUsingRandomBlockedCells(gridMap);
                if (GUILayout.Button("블럭을 모두 제거하고 다시 생성", GUILayout.Height(26f)))
                    ApplyMapConfigurationAndRebuildVisuals(gridMap, Array.Empty<Vector2Int>(), "그리드 블럭 제거");
            }
        }

        private void RebuildMapUsingSerializedBlockedCells(GridMap gridMap)
        {
            List<Vector2Int> validBlockedCells = new();
            HashSet<Vector2Int> uniqueCells = new();
            for (int index = 0; index < blockedCellsProperty.arraySize; index++)
            {
                Vector2Int cell = blockedCellsProperty.GetArrayElementAtIndex(index).vector2IntValue;
                if (!IsInside(cell) || !uniqueCells.Add(cell))
                    continue;
                validBlockedCells.Add(cell);
            }
            ApplyMapConfigurationAndRebuildVisuals(gridMap, validBlockedCells.ToArray(), "그리드 맵 크기 변경");
        }

        private void RebuildMapUsingRandomBlockedCells(GridMap gridMap)
        {
            serializedObject.Update();
            int seed = randomSeedProperty.intValue;
            int requestedCount = randomBlockedCellCountProperty.intValue;
            HashSet<Vector2Int> protectedCells = CollectCombatantCells(gridMap);
            List<Vector2Int> candidates = new();
            for (int x = 0; x < widthProperty.intValue; x++)
            {
                for (int z = 0; z < heightProperty.intValue; z++)
                {
                    Vector2Int cell = new(x, z);
                    if (!protectedCells.Contains(cell))
                        candidates.Add(cell);
                }
            }

            System.Random random = new(seed);
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (candidates[index], candidates[swapIndex]) = (candidates[swapIndex], candidates[index]);
            }

            int appliedCount = Mathf.Min(requestedCount, candidates.Count);
            Vector2Int[] blockedCells = candidates.GetRange(0, appliedCount).ToArray();
            randomSeedProperty.intValue = unchecked(seed + 1);
            serializedObject.ApplyModifiedProperties();
            ApplyMapConfigurationAndRebuildVisuals(gridMap, blockedCells, "랜덤 그리드 블럭 배치");
            if (appliedCount < requestedCount)
                Debug.LogWarning($"유닛 배치 셀을 제외하고 {appliedCount}개의 블럭만 배치했습니다.", gridMap);
        }

        private void ApplyMapConfigurationAndRebuildVisuals(
            GridMap gridMap,
            Vector2Int[] blockedCells,
            string undoName)
        {
            serializedObject.Update();
            int width = Mathf.Max(1, widthProperty.intValue);
            int height = Mathf.Max(1, heightProperty.intValue);
            float cellSize = Mathf.Max(0.1f, cellSizeProperty.floatValue);
            Vector3 origin = originProperty.vector3Value;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(gridMap, undoName);
            gridMap.SetEditorConfiguration(width, height, cellSize, origin, blockedCells);
            RebuildGridVisualObjects(gridMap, blockedCells, undoName);
            RecenterCameraTarget(gridMap, undoName);
            EditorUtility.SetDirty(gridMap);
            EditorSceneManager.MarkSceneDirty(gridMap.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            serializedObject.Update();
            Debug.Log($"그리드 맵을 {width}x{height}, 셀 크기 {cellSize:0.##}, 블럭 {blockedCells.Length}개로 다시 생성했습니다.", gridMap);
        }

        private static void RebuildGridVisualObjects(GridMap gridMap, Vector2Int[] blockedCells, string undoName)
        {
            Transform existingCells = gridMap.transform.Find("Cells");
            Transform existingCovers = gridMap.transform.Find("Covers");
            if (existingCells != null)
                Undo.DestroyObjectImmediate(existingCells.gameObject);
            if (existingCovers != null)
                Undo.DestroyObjectImmediate(existingCovers.gameObject);

            Material evenMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/GridEven.mat");
            Material oddMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/GridOdd.mat");
            Material coverMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/Cover.mat");

            GameObject cellsRoot = new("Cells");
            Undo.RegisterCreatedObjectUndo(cellsRoot, undoName);
            cellsRoot.transform.SetParent(gridMap.transform, false);
            float visualCellSize = gridMap.CellSize * 0.96f;
            for (int x = 0; x < gridMap.Width; x++)
            {
                for (int z = 0; z < gridMap.Height; z++)
                {
                    GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Undo.RegisterCreatedObjectUndo(cell, undoName);
                    cell.name = $"Cell_{x:00}_{z:00}";
                    cell.layer = GroundLayer;
                    cell.transform.SetParent(cellsRoot.transform, false);
                    cell.transform.position = gridMap.GridToWorld(new GridCoordinate(x, z)) + Vector3.down * 0.08f;
                    cell.transform.localScale = new Vector3(visualCellSize, 0.12f, visualCellSize);
                    cell.GetComponent<Renderer>().sharedMaterial = (x + z) % 2 == 0 ? evenMaterial : oddMaterial;
                }
            }

            GameObject coversRoot = new("Covers");
            Undo.RegisterCreatedObjectUndo(coversRoot, undoName);
            coversRoot.transform.SetParent(gridMap.transform, false);
            float coverWidth = gridMap.CellSize * 0.9f;
            float coverHeight = gridMap.CellSize * 0.7f;
            foreach (Vector2Int blockedCell in blockedCells)
            {
                GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(cover, undoName);
                cover.name = $"Cover_{blockedCell.x:00}_{blockedCell.y:00}";
                cover.layer = CoverLayer;
                cover.transform.SetParent(coversRoot.transform, false);
                cover.transform.position = gridMap.GridToWorld(new GridCoordinate(blockedCell.x, blockedCell.y)) + Vector3.up * (coverHeight * 0.5f);
                cover.transform.localScale = new Vector3(coverWidth, coverHeight, coverWidth);
                cover.GetComponent<Renderer>().sharedMaterial = coverMaterial;
            }
        }

        private HashSet<Vector2Int> CollectCombatantCells(GridMap gridMap)
        {
            HashSet<Vector2Int> protectedCells = new();
            foreach (Combatant combatant in UnityEngine.Object.FindObjectsByType<Combatant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GridCoordinate cell = gridMap.WorldToGrid(combatant.transform.position);
                Vector2Int cellPosition = new(cell.X, cell.Z);
                if (IsInside(cellPosition))
                    protectedCells.Add(cellPosition);
            }
            return protectedCells;
        }

        private bool HasOutOfBoundsCombatant(GridMap gridMap)
        {
            foreach (Combatant combatant in UnityEngine.Object.FindObjectsByType<Combatant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GridCoordinate cell = gridMap.WorldToGrid(combatant.transform.position);
                if (cell.X < 0 || cell.X >= widthProperty.intValue || cell.Z < 0 || cell.Z >= heightProperty.intValue)
                    return true;
            }
            return false;
        }

        private bool IsInside(Vector2Int cell)
            => cell.x >= 0 && cell.x < widthProperty.intValue && cell.y >= 0 && cell.y < heightProperty.intValue;

        private static void RecenterCameraTarget(GridMap gridMap, string undoName)
        {
            GameObject cameraTarget = GameObject.Find("CameraRigTarget");
            if (cameraTarget == null)
                return;
            Undo.RecordObject(cameraTarget.transform, undoName);
            cameraTarget.transform.position = gridMap.Origin + new Vector3(
                gridMap.Width * gridMap.CellSize * 0.5f,
                0f,
                gridMap.Height * gridMap.CellSize * 0.5f);
        }
    }
}
