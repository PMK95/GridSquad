using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class GridCombatIndicator : MonoBehaviour
    {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private MeshFilter viewCellMeshFilter;
        [SerializeField] private MeshFilter shootableCellMeshFilter;

        private readonly HashSet<GridCoordinate> viewCells = new();
        private readonly HashSet<GridCoordinate> shootableCells = new();
        private Combatant selectedCombatant;
        private Mesh viewCellMesh;
        private Mesh shootableCellMesh;
        private float nextRefreshTime;
        private GridCoordinate previousShotOriginCell;

        public int ViewCellCount => viewCells.Count;
        public int ShootableCellCount => shootableCells.Count;

        private void Awake()
        {
            viewCellMesh = CreateRuntimeMesh("선택 유닛 시야 셀");
            shootableCellMesh = CreateRuntimeMesh("선택 유닛 사격 가능 셀");
            viewCellMeshFilter.sharedMesh = viewCellMesh;
            shootableCellMeshFilter.sharedMesh = shootableCellMesh;
            HideIndicators();
        }

        private void Update()
        {
            if (selectedCombatant == null || !selectedCombatant.IsAlive)
            {
                HideIndicators();
                return;
            }

            GridCoordinate shotOriginCell = selectedCombatant.CurrentIndicatorShotOriginCell;
            if (shotOriginCell != previousShotOriginCell || Time.unscaledTime >= nextRefreshTime)
                RebuildIndicators(shotOriginCell);
        }

        private void OnDestroy()
        {
            if (viewCellMesh != null)
                Destroy(viewCellMesh);
            if (shootableCellMesh != null)
                Destroy(shootableCellMesh);
        }

        public void SetSelectedCombatant(Combatant combatant)
        {
            selectedCombatant = combatant;
            if (selectedCombatant == null || !selectedCombatant.IsAlive)
            {
                HideIndicators();
                return;
            }

            RebuildIndicators(selectedCombatant.CurrentIndicatorShotOriginCell);
        }

        public bool IsViewCell(GridCoordinate cell) => viewCells.Contains(cell);
        public bool IsShootableCell(GridCoordinate cell) => shootableCells.Contains(cell);

        private void RebuildIndicators(GridCoordinate shotOriginCell)
        {
            viewCells.Clear();
            shootableCells.Clear();

            for (int x = 0; x < gridMap.Width; x++)
            {
                for (int z = 0; z < gridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    if (cell == selectedCombatant.CurrentCell
                        || cell == shotOriginCell
                        || !shotEvaluator.IsCellInsideShootingView(selectedCombatant, cell, shotOriginCell))
                    {
                        continue;
                    }

                    viewCells.Add(cell);
                    if (gridMap.IsBlocked(cell))
                        continue;

                    ShotEvaluation evaluation = shotEvaluator.EvaluateShotAtCell(selectedCombatant, cell, shotOriginCell);
                    if (evaluation.CanShoot)
                        shootableCells.Add(cell);
                }
            }

            BuildCellMesh(viewCellMesh, viewCells, 0.045f, 0.12f);
            BuildCellMesh(shootableCellMesh, shootableCells, 0.075f, 0.24f);
            viewCellMeshFilter.gameObject.SetActive(viewCells.Count > 0);
            shootableCellMeshFilter.gameObject.SetActive(shootableCells.Count > 0);
            previousShotOriginCell = shotOriginCell;
            nextRefreshTime = Time.unscaledTime + tuning.EvaluationRefreshInterval;
        }

        private void HideIndicators()
        {
            viewCells.Clear();
            shootableCells.Clear();
            if (viewCellMeshFilter != null)
                viewCellMeshFilter.gameObject.SetActive(false);
            if (shootableCellMeshFilter != null)
                shootableCellMeshFilter.gameObject.SetActive(false);
        }

        private void BuildCellMesh(Mesh mesh, IReadOnlyCollection<GridCoordinate> cells, float height, float inset)
        {
            int vertexCount = cells.Count * 4;
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[cells.Count * 6];
            float halfSize = gridMap.CellSize * 0.5f - inset;
            int cellIndex = 0;
            foreach (GridCoordinate cell in cells)
            {
                Vector3 center = gridMap.GridToWorld(cell) + Vector3.up * height;
                int vertexIndex = cellIndex * 4;
                vertices[vertexIndex] = center + new Vector3(-halfSize, 0f, -halfSize);
                vertices[vertexIndex + 1] = center + new Vector3(-halfSize, 0f, halfSize);
                vertices[vertexIndex + 2] = center + new Vector3(halfSize, 0f, halfSize);
                vertices[vertexIndex + 3] = center + new Vector3(halfSize, 0f, -halfSize);

                int triangleIndex = cellIndex * 6;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;
                triangles[triangleIndex + 3] = vertexIndex;
                triangles[triangleIndex + 4] = vertexIndex + 2;
                triangles[triangleIndex + 5] = vertexIndex + 3;
                cellIndex++;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private static Mesh CreateRuntimeMesh(string meshName)
        {
            Mesh mesh = new() { name = meshName };
            mesh.MarkDynamic();
            return mesh;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatTuning newTuning,
            MeshFilter newViewCellMeshFilter,
            MeshFilter newShootableCellMeshFilter)
        {
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            tuning = newTuning;
            viewCellMeshFilter = newViewCellMeshFilter;
            shootableCellMeshFilter = newShootableCellMeshFilter;
        }
#endif
    }
}
