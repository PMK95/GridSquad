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
        private readonly HashSet<GridCoordinate> actionTargetCells = new();
        private readonly HashSet<GridCoordinate> actionAffectedCells = new();
        private Combatant selectedCombatant;
        private CombatActionController actionController;
        private CombatActionKind? actionTargetingKind;
        private Mesh viewCellMesh;
        private Mesh shootableCellMesh;
        private Mesh actionTargetMesh;
        private Mesh actionAffectedMesh;
        private MeshFilter actionTargetMeshFilter;
        private MeshFilter actionAffectedMeshFilter;
        private Material actionTargetMaterial;
        private Material actionAffectedMaterial;
        private float nextRefreshTime;
        private GridCoordinate previousShotOriginCell;

        public int ViewCellCount => viewCells.Count;
        public int ShootableCellCount => shootableCells.Count;

        private void Awake()
        {
            viewCellMesh = CreateRuntimeMesh("선택 유닛 시야 셀");
            shootableCellMesh = CreateRuntimeMesh("선택 유닛 사격 가능 셀");
            actionTargetMesh = CreateRuntimeMesh("액션 유효 셀");
            actionAffectedMesh = CreateRuntimeMesh("액션 영향 셀");
            viewCellMeshFilter.sharedMesh = viewCellMesh;
            shootableCellMeshFilter.sharedMesh = shootableCellMesh;
            actionTargetMeshFilter = CreatePreviewMeshObject(
                "ActionTargetCells",
                actionTargetMesh,
                new Color(0.1f, 0.8f, 1f, 0.28f),
                out actionTargetMaterial);
            actionAffectedMeshFilter = CreatePreviewMeshObject(
                "ActionAffectedCells",
                actionAffectedMesh,
                new Color(1f, 0.48f, 0.08f, 0.5f),
                out actionAffectedMaterial);
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
            if (actionTargetMesh != null)
                Destroy(actionTargetMesh);
            if (actionAffectedMesh != null)
                Destroy(actionAffectedMesh);
            if (actionTargetMaterial != null)
                Destroy(actionTargetMaterial);
            if (actionAffectedMaterial != null)
                Destroy(actionAffectedMaterial);
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

        public void SetActionTargeting(
            CombatActionKind? kind,
            CombatActionController newActionController)
        {
            actionTargetingKind = kind;
            actionController = newActionController;
            actionTargetCells.Clear();
            actionAffectedCells.Clear();
            if (!kind.HasValue || actionController == null)
            {
                actionTargetMeshFilter.gameObject.SetActive(false);
                actionAffectedMeshFilter.gameObject.SetActive(false);
                return;
            }

            for (int x = 0; x < gridMap.Width; x++)
            {
                for (int z = 0; z < gridMap.Height; z++)
                {
                    GridCoordinate cell = new(x, z);
                    bool valid = kind == CombatActionKind.Grenade
                        ? actionController.IsValidGrenadeTarget(cell, out _)
                        : actionController.IsValidDashTarget(cell, out _);
                    if (valid)
                        actionTargetCells.Add(cell);
                }
            }
            BuildCellMesh(actionTargetMesh, actionTargetCells, 0.095f, 0.18f);
            actionTargetMeshFilter.gameObject.SetActive(actionTargetCells.Count > 0);
            actionAffectedMeshFilter.gameObject.SetActive(false);
        }

        public void SetActionPreviewCell(GridCoordinate targetCell)
        {
            if (!actionTargetingKind.HasValue || actionController == null)
                return;

            actionAffectedCells.Clear();
            bool valid = actionTargetCells.Contains(targetCell);
            if (actionTargetingKind == CombatActionKind.Grenade)
            {
                int radius = actionController.GetGrenadeRadiusCells();
                for (int x = targetCell.X - radius; x <= targetCell.X + radius; x++)
                {
                    for (int z = targetCell.Z - radius; z <= targetCell.Z + radius; z++)
                    {
                        GridCoordinate cell = new(x, z);
                        if (gridMap.IsInside(cell))
                            actionAffectedCells.Add(cell);
                    }
                }
                bool friendlyFire = actionController.DoesGrenadeAreaContainFriendly(targetCell);
                actionAffectedMaterial.color = friendlyFire
                    ? new Color(1f, 0.08f, 0.08f, 0.58f)
                    : valid
                        ? new Color(1f, 0.48f, 0.08f, 0.5f)
                        : new Color(0.8f, 0.05f, 0.05f, 0.5f);
            }
            else
            {
                GridCoordinate origin = actionController.OwnerCombatant.CurrentCell;
                int xStep = System.Math.Sign(targetCell.X - origin.X);
                int zStep = System.Math.Sign(targetCell.Z - origin.Z);
                int distance = origin.ManhattanDistance(targetCell);
                for (int step = 1; step <= distance; step++)
                {
                    GridCoordinate cell = new(origin.X + xStep * step, origin.Z + zStep * step);
                    if (gridMap.IsInside(cell))
                        actionAffectedCells.Add(cell);
                }
                actionAffectedMaterial.color = valid
                    ? new Color(0.2f, 0.9f, 1f, 0.55f)
                    : new Color(1f, 0.08f, 0.08f, 0.58f);
            }

            BuildCellMesh(actionAffectedMesh, actionAffectedCells, 0.13f, 0.1f);
            actionAffectedMeshFilter.gameObject.SetActive(actionAffectedCells.Count > 0);
        }

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

        private MeshFilter CreatePreviewMeshObject(
            string objectName,
            Mesh mesh,
            Color color,
            out Material material)
        {
            GameObject previewObject = new(objectName);
            previewObject.transform.SetParent(transform, false);
            MeshFilter meshFilter = previewObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = previewObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { color = color };
            meshRenderer.sharedMaterial = material;
            previewObject.SetActive(false);
            return meshFilter;
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
