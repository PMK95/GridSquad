using System;
using System.Collections.Generic;
using GridSquad;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GridSquadEditor
{
    public static class CombatFeasibilitySceneBuilder
    {
        private const string RootPath = "Assets/GridSquad";
        private const string ScenePath = RootPath + "/Scenes/CombatFeasibility.unity";
        private const string CharacterUiPath = RootPath + "/Prefabs/CharacterWorldUI.prefab";
        private const string AllyPrefabPath = RootPath + "/Prefabs/AllyUnit.prefab";
        private const string EnemyPrefabPath = RootPath + "/Prefabs/EnemyUnit.prefab";
        private const string TuningPath = RootPath + "/Settings/CombatTuning.asset";
        private const string WeaponPath = RootPath + "/Settings/WeaponDefinition.asset";
        private const string BehaviorGraphPath = RootPath + "/Behavior/UnitCombatBehavior.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private const int GroundLayer = 8;
        private const int UnitLayer = 9;
        private const int CoverLayer = 10;

        private static readonly Vector2Int[] CoverCells =
        {
            new(3, 2), new(3, 3), new(5, 4), new(6, 4), new(8, 2),
            new(2, 6), new(4, 7), new(5, 7), new(7, 6), new(9, 7),
            new(3, 9), new(6, 9), new(8, 9), new(9, 9)
        };

        [MenuItem("GridSquad/전투 피저빌리티 씬 생성")]
        public static void BuildCombatFeasibilityScene()
        {
            EnsureFolders();
            EnsureLayer(GroundLayer, "Ground");
            EnsureLayer(UnitLayer, "Unit");
            EnsureLayer(CoverLayer, "Cover");

            CombatTuning tuning = LoadOrCreateAsset<CombatTuning>(TuningPath);
            WeaponDefinition weapon = LoadOrCreateAsset<WeaponDefinition>(WeaponPath);
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
                throw new InvalidOperationException("전술 입력 에셋을 찾지 못했습니다.");

            Material allyMaterial = CreateOrUpdateMaterial("Ally", new Color(0.15f, 0.55f, 1f));
            Material enemyMaterial = CreateOrUpdateMaterial("Enemy", new Color(1f, 0.22f, 0.18f));
            Material gunMaterial = CreateOrUpdateMaterial("Gun", new Color(0.12f, 0.14f, 0.16f));
            Material coverMaterial = CreateOrUpdateMaterial("Cover", new Color(0.25f, 0.28f, 0.32f));
            Material gridEvenMaterial = CreateOrUpdateMaterial("GridEven", new Color(0.20f, 0.23f, 0.25f));
            Material gridOddMaterial = CreateOrUpdateMaterial("GridOdd", new Color(0.24f, 0.27f, 0.29f));
            Material lineMaterial = CreateOrUpdateMaterial("DebugLine", Color.white, true);
            Material viewRangeMaterial = CreateOrUpdateMaterial("ViewRangeIndicator", new Color(0.15f, 0.65f, 1f, 0.24f), true);
            Material shootableCellMaterial = CreateOrUpdateMaterial("ShootableCellIndicator", new Color(0.15f, 1f, 0.35f, 0.5f), true);
            BehaviorGraph unitCombatBehavior = CreateOrUpdateUnitCombatBehaviorGraph();

            GameObject characterUiPrefab = CreateCharacterWorldUiPrefab(lineMaterial);
            GameObject allyPrefab = CreateUnitPrefab(AllyPrefabPath, Team.Ally, allyMaterial, gunMaterial, lineMaterial, characterUiPrefab, unitCombatBehavior);
            GameObject enemyPrefab = CreateUnitPrefab(EnemyPrefabPath, Team.Enemy, enemyMaterial, gunMaterial, lineMaterial, characterUiPrefab, unitCombatBehavior);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            Camera sceneCamera = CreateSceneCamera();
            GridMap gridMap = CreateGrid(gridEvenMaterial, gridOddMaterial, coverMaterial);
            Transform cameraTarget = CreateCameraTarget(gridMap);
            CreateRtsCinemachineCamera(sceneCamera, cameraTarget, gridMap, inputActions, tuning);

            GameObject systems = new("CombatSystems");
            ShotEvaluator shotEvaluator = systems.AddComponent<ShotEvaluator>();
            CombatDirector director = systems.AddComponent<CombatDirector>();
            TacticalPositionEvaluator positionEvaluator = systems.AddComponent<TacticalPositionEvaluator>();
            TacticalInputController inputController = systems.AddComponent<TacticalInputController>();
            CombatHudController hud = CreateSceneHud(director);
            CreateEventSystem();
            LineRenderer pathLine = CreateLineRenderer("SelectedPath", systems.transform, lineMaterial, 0.08f);
            GridCombatIndicator gridCombatIndicator = CreateGridCombatIndicator(
                systems.transform,
                gridMap,
                shotEvaluator,
                tuning,
                viewRangeMaterial,
                shootableCellMaterial);

            shotEvaluator.SetEditorReferences(gridMap, tuning);
            positionEvaluator.SetEditorReferences(gridMap, shotEvaluator, director, tuning);
            Combatant[] combatants = CreateCombatants(
                allyPrefab,
                enemyPrefab,
                gridMap,
                shotEvaluator,
                director,
                positionEvaluator,
                tuning,
                weapon,
                sceneCamera.transform);
            director.SetEditorReferences(combatants, shotEvaluator, hud);
            inputController.SetEditorReferences(
                inputActions,
                sceneCamera,
                gridMap,
                director,
                hud,
                pathLine,
                gridCombatIndicator,
                1 << GroundLayer,
                1 << UnitLayer);

            Selection.activeGameObject = systems;
            EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("전투 피저빌리티 씬과 프리팹 생성을 완료했습니다.");
        }

        public static void ConfigureRtsCameraController()
        {
            GameObject cameraObject = GameObject.Find("RTSCinemachineCamera");
            GameObject targetObject = GameObject.Find("CameraRigTarget");
            GameObject gridObject = GameObject.Find("GridMap");
            Camera outputCamera = Object.FindFirstObjectByType<Camera>();
            if (cameraObject == null || targetObject == null || gridObject == null || outputCamera == null)
                throw new InvalidOperationException("RTS 카메라 연결에 필요한 씬 오브젝트가 없습니다.");

            CinemachineOrbitalFollow orbitalFollow = cameraObject.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow == null)
                throw new InvalidOperationException("Cinemachine Orbital Follow가 구성되지 않았습니다.");

            RtsCameraController controller = cameraObject.GetComponent<RtsCameraController>();
            if (controller == null)
                controller = cameraObject.AddComponent<RtsCameraController>();
            controller.SetEditorReferences(
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath),
                targetObject.transform,
                outputCamera.transform,
                gridObject.GetComponent<GridMap>(),
                AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("RTS 카메라 입력 참조 연결을 완료했습니다.");
        }

        [MenuItem("GridSquad/선택 유닛 그리드 인디케이터 구성")]
        public static void ConfigureGridCombatIndicator()
        {
            EnsureFolders();
            GridMap gridMap = Object.FindFirstObjectByType<GridMap>();
            ShotEvaluator shotEvaluator = Object.FindFirstObjectByType<ShotEvaluator>();
            TacticalInputController inputController = Object.FindFirstObjectByType<TacticalInputController>();
            CombatTuning tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath);
            if (gridMap == null || shotEvaluator == null || inputController == null || tuning == null)
                throw new InvalidOperationException("그리드 인디케이터 연결에 필요한 전투 오브젝트가 없습니다.");

            Material viewRangeMaterial = CreateOrUpdateMaterial(
                "ViewRangeIndicator",
                new Color(0.15f, 0.65f, 1f, 0.24f),
                true);
            Material shootableCellMaterial = CreateOrUpdateMaterial(
                "ShootableCellIndicator",
                new Color(0.15f, 1f, 0.35f, 0.5f),
                true);
            GridCombatIndicator indicator = CreateGridCombatIndicator(
                inputController.transform,
                gridMap,
                shotEvaluator,
                tuning,
                viewRangeMaterial,
                shootableCellMaterial);
            inputController.SetEditorGridCombatIndicator(indicator);
            EditorUtility.SetDirty(inputController);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("선택 유닛 그리드 시야 및 사격 가능 셀 인디케이터 구성을 완료했습니다.");
        }

        public static void ValidateCombatFeasibilityStructure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("전투 피저빌리티 씬이 열려 있지 않습니다.");

            GridMap gridMap = Object.FindFirstObjectByType<GridMap>();
            CombatDirector director = Object.FindFirstObjectByType<CombatDirector>();
            ShotEvaluator shotEvaluator = Object.FindFirstObjectByType<ShotEvaluator>();
            TacticalPositionEvaluator positionEvaluator = Object.FindFirstObjectByType<TacticalPositionEvaluator>();
            TacticalInputController inputController = Object.FindFirstObjectByType<TacticalInputController>();
            CombatHudController hud = Object.FindFirstObjectByType<CombatHudController>();
            InputSystemUIInputModule uiInputModule = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            Camera mainCamera = Camera.main;
            CinemachineCamera cinemachineCamera = Object.FindFirstObjectByType<CinemachineCamera>();
            RtsCameraController cameraController = Object.FindFirstObjectByType<RtsCameraController>();
            GridCombatIndicator gridCombatIndicator = Object.FindFirstObjectByType<GridCombatIndicator>();
            if (gridMap == null || gridMap.Width != 12 || gridMap.Height != 12 || !Mathf.Approximately(gridMap.CellSize, 2f))
                throw new InvalidOperationException("12×12, 2m 그리드 직렬화가 올바르지 않습니다.");
            if (director == null || shotEvaluator == null || positionEvaluator == null
                || inputController == null || hud == null || gridCombatIndicator == null
                || uiInputModule == null || uiInputModule.GetComponent<EventSystem>() == null)
                throw new InvalidOperationException("전투 시스템 또는 씬 HUD가 누락되었습니다.");
            if (mainCamera == null || mainCamera.GetComponent<CinemachineBrain>() == null
                || cinemachineCamera == null
                || cinemachineCamera.GetComponent<CinemachineOrbitalFollow>() == null
                || cinemachineCamera.GetComponent<CinemachineRotationComposer>() == null
                || cameraController == null)
                throw new InvalidOperationException("Cinemachine RTS 카메라 구조가 올바르지 않습니다.");

            BehaviorGraph expectedBehaviorGraph = AssetDatabase.LoadAssetAtPath<BehaviorGraph>(BehaviorGraphPath);
            if (expectedBehaviorGraph == null)
                throw new InvalidOperationException("UnitCombatBehavior 런타임 그래프가 없습니다.");

            Combatant[] combatants = Object.FindObjectsByType<Combatant>(FindObjectsSortMode.None);
            int allyCount = 0;
            int enemyCount = 0;
            foreach (Combatant combatant in combatants)
            {
                if (combatant.Team == Team.Ally)
                    allyCount++;
                else
                    enemyCount++;
                if (combatant.GetComponentInChildren<CharacterWorldUiPresenter>(true) == null)
                    throw new InvalidOperationException($"{combatant.name}의 캐릭터 UI가 누락되었습니다.");
                RequireReferenceFields(
                    combatant,
                    "weapon",
                    "tuning",
                    "gridMap",
                    "shotEvaluator",
                    "director",
                    "visualRoot",
                    "muzzle",
                    "aimCenter",
                    "selectionCollider",
                    "muzzleFlash",
                    "shotTracer",
                    "worldUi");
                if (combatant.GetComponentInChildren<Flicker>(true) == null)
                    throw new InvalidOperationException($"{combatant.name}의 피격 플리커가 누락되었습니다.");
                BehaviorGraphAgent behaviorAgent = combatant.GetComponent<BehaviorGraphAgent>();
                UnitTacticalBehaviorController behaviorController = combatant.GetComponent<UnitTacticalBehaviorController>();
                if (behaviorController != null)
                {
                    SerializedObject behaviorControllerObject = new(behaviorController);
                    SerializedProperty defaultMovementProperty =
                        behaviorControllerObject.FindProperty("autonomousMovementDefault");
                    bool expectedAutonomousMovement = combatant.Team == Team.Enemy;
                    if (defaultMovementProperty == null
                        || defaultMovementProperty.boolValue != expectedAutonomousMovement)
                    {
                        throw new InvalidOperationException(
                            $"{combatant.name}의 기본 자율 이동 설정이 잘못되었습니다.");
                    }
                    RequireReferenceFields(
                        behaviorController,
                        "combatant",
                        "gridMap",
                        "shotEvaluator",
                        "director",
                        "positionEvaluator",
                        "tuning",
                        "behaviorAgent");
                }
                if (behaviorAgent == null || behaviorController == null
                    || behaviorAgent.Graph != expectedBehaviorGraph)
                    throw new InvalidOperationException($"{combatant.name}의 전술 AI가 누락되었습니다.");
            }
            if (allyCount != 3 || enemyCount != 4)
                throw new InvalidOperationException($"유닛 배치 수가 올바르지 않습니다. 아군 {allyCount}, 적군 {enemyCount}");

            RequireReferenceFields(inputController, "inputActions", "sceneCamera", "gridMap", "director", "hud", "selectedPathLine", "gridCombatIndicator");
            RequireReferenceFields(gridCombatIndicator, "gridMap", "shotEvaluator", "tuning", "viewCellMeshFilter", "shootableCellMeshFilter");
            RequireReferenceFields(cameraController, "inputActions", "rigTarget", "outputCamera", "gridMap", "tuning");
            RequireReferenceFields(director, "shotEvaluator", "hud");
            RequireReferenceFields(shotEvaluator, "gridMap", "tuning");
            RequireReferenceFields(positionEvaluator, "gridMap", "shotEvaluator", "director", "tuning");
            RequireReferenceFields(
                hud,
                "stateText",
                "modeText",
                "debugText",
                "allyFullAutoButton",
                "allyFullAutoButtonText",
                "director",
                "resultPanel",
                "resultText",
                "selectedInfoPanel",
                "selectedInfoTitleText",
                "selectedInfoBodyText");
            if (hud.transform.Find("SelectedInfoPanel") == null)
                throw new InvalidOperationException("선택 캐릭터 정보 패널이 씬 HUD에 없습니다.");
            if (hud.transform.Find("AllyFullAutoButton") == null)
                throw new InvalidOperationException("아군 자동전투 토글 버튼이 HUD에 없습니다.");

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            InputActionMap tacticalMap = inputActions?.FindActionMap("Tactical", false);
            string[] requiredActions =
            {
                "CameraMove", "PointerPosition", "PointerDelta", "CameraOrbit", "CameraZoom",
                "Select", "MoveCommand", "TargetCommand", "TogglePeek", "TogglePause",
                "Speed1", "Speed2", "Speed3", "Cancel", "ToggleDebug", "Restart"
            };
            if (tacticalMap == null)
                throw new InvalidOperationException("Tactical 입력 맵이 누락되었습니다.");
            foreach (string actionName in requiredActions)
            {
                InputAction action = tacticalMap.FindAction(actionName, false);
                if (action == null || action.bindings.Count == 0)
                    throw new InvalidOperationException($"전술 입력 {actionName} 또는 바인딩이 누락되었습니다.");
            }

            ValidateNestedCharacterUiPrefab(AllyPrefabPath);
            ValidateNestedCharacterUiPrefab(EnemyPrefabPath);
            Debug.Log("전투 피저빌리티 씬·프리팹·입력·카메라 직렬화 구조 검사를 완료했습니다.");
        }

        private static void RequireReferenceFields(Component component, params string[] propertyNames)
        {
            SerializedObject serializedObject = new(component);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                    throw new InvalidOperationException($"{component.name}/{component.GetType().Name}.{propertyName} 참조가 비어 있습니다.");
            }
        }

        private static void ValidateNestedCharacterUiPrefab(string unitPrefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(unitPrefabPath);
            Transform ui = prefab != null ? prefab.transform.Find("CharacterWorldUI") : null;
            if (ui == null || ui.GetComponent<CharacterWorldUiPresenter>() == null)
                throw new InvalidOperationException($"{unitPrefabPath}의 중첩 캐릭터 UI가 누락되었습니다.");
            CharacterWorldUiPresenter presenter = ui.GetComponent<CharacterWorldUiPresenter>();
            RequireReferenceFields(
                presenter,
                "canvas",
                "healthFill",
                "detailText",
                "selectionIndicator",
                "targetLine",
                "targetRing",
                "peekLine",
                "peekRing");
            string nestedPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(ui.gameObject);
            if (nestedPath != CharacterUiPath)
                throw new InvalidOperationException($"{unitPrefabPath}의 CharacterWorldUI가 별도 프리팹으로 중첩되지 않았습니다.");
            if (prefab.GetComponentInChildren<ParticleSystem>(true) == null)
                throw new InvalidOperationException($"{unitPrefabPath}의 총구 파티클 시스템이 누락되었습니다.");
            if (prefab.GetComponentInChildren<Flicker>(true) == null)
                throw new InvalidOperationException($"{unitPrefabPath}의 피격 플리커가 누락되었습니다.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "GridSquad");
            EnsureFolder(RootPath, "Scenes");
            EnsureFolder(RootPath, "Prefabs");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Settings");
            EnsureFolder(RootPath, "Behavior");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void EnsureLayer(int index, string layerName)
        {
            SerializedObject tagManager = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == layerName)
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return;
            }
            throw new InvalidOperationException($"레이어 {index}가 이미 {layer.stringValue}(으)로 사용 중입니다.");
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static BehaviorGraph CreateOrUpdateUnitCombatBehaviorGraph()
        {
            if (AssetDatabase.LoadMainAssetAtPath(BehaviorGraphPath) != null)
                AssetDatabase.DeleteAsset(BehaviorGraphPath);

            BehaviorAuthoringGraph authoringGraph =
                ScriptableObject.CreateInstance<BehaviorAuthoringGraph>();
            authoringGraph.name = "UnitCombatBehavior";
            AssetDatabase.CreateAsset(authoringGraph, BehaviorGraphPath);
            authoringGraph.EnsureAssetHasBlackboard();

            BlackboardAsset blackboard = authoringGraph.Blackboard;
            blackboard.Variables.Clear();
            TypedVariableModel<GameObject> self = CreateBlackboardVariable(
                "Self",
                (GameObject)null,
                new SerializableGUID(1, 0));
            TypedVariableModel<bool> autonomousMovementAllowed =
                CreateBlackboardVariable("AutonomousMovementAllowed", false);
            TypedVariableModel<bool> automaticPeekAllowed =
                CreateBlackboardVariable("AutomaticPeekAllowed", true);
            TypedVariableModel<bool> moveCommandPending =
                CreateBlackboardVariable("MoveCommandPending", false);
            TypedVariableModel<Vector3> moveDestination =
                CreateBlackboardVariable("MoveDestination", Vector3.zero);
            TypedVariableModel<GameObject> priorityTarget =
                CreateBlackboardVariable("PriorityTarget", (GameObject)null);
            TypedVariableModel<GameObject> currentTarget =
                CreateBlackboardVariable("CurrentTarget", (GameObject)null);
            blackboard.Variables.Add(self);
            blackboard.Variables.Add(autonomousMovementAllowed);
            blackboard.Variables.Add(automaticPeekAllowed);
            blackboard.Variables.Add(moveCommandPending);
            blackboard.Variables.Add(moveDestination);
            blackboard.Variables.Add(priorityTarget);
            blackboard.Variables.Add(currentTarget);
            blackboard.SetAssetDirty();

            StartNodeModel start = (StartNodeModel)CreateBehaviorNode(
                authoringGraph,
                typeof(Start),
                new Vector2(0f, 0f));
            start.Repeat = true;
            CompositeNodeModel parallel = (CompositeNodeModel)CreateBehaviorNode(
                authoringGraph,
                typeof(ParallelAllComposite),
                new Vector2(0f, 160f));
            BehaviorGraphNodeModel tacticalLoop = CreateBehaviorNode(
                authoringGraph,
                typeof(TacticalDecisionLoopAction),
                new Vector2(-260f, 340f));
            BehaviorGraphNodeModel combatLoop = CreateBehaviorNode(
                authoringGraph,
                typeof(CombatExecutionLoopAction),
                new Vector2(260f, 340f));

            tacticalLoop.SetField("Agent", self, typeof(GameObject));
            tacticalLoop.SetField(
                "AutonomousMovementAllowed",
                autonomousMovementAllowed,
                typeof(bool));
            tacticalLoop.SetField(
                "AutomaticPeekAllowed",
                automaticPeekAllowed,
                typeof(bool));
            tacticalLoop.SetField(
                "MoveCommandPending",
                moveCommandPending,
                typeof(bool));
            tacticalLoop.SetField("MoveDestination", moveDestination, typeof(Vector3));
            tacticalLoop.SetField("PriorityTarget", priorityTarget, typeof(GameObject));
            tacticalLoop.SetField("CurrentTarget", currentTarget, typeof(GameObject));
            combatLoop.SetField("Agent", self, typeof(GameObject));
            combatLoop.SetField("CurrentTarget", currentTarget, typeof(GameObject));

            ConnectBehaviorNodes(authoringGraph, start, parallel);
            ConnectBehaviorNodes(authoringGraph, parallel, tacticalLoop);
            ConnectBehaviorNodes(authoringGraph, parallel, combatLoop);
            authoringGraph.SetAssetDirty();
            authoringGraph.ValidateAsset();
            BehaviorGraph runtimeGraph = authoringGraph.BuildRuntimeGraph(true);
            EditorUtility.SetDirty(authoringGraph);
            EditorUtility.SetDirty(runtimeGraph);
            AssetDatabase.SaveAssets();
            return runtimeGraph;
        }

        private static TypedVariableModel<TValue> CreateBlackboardVariable<TValue>(
            string name,
            TValue value,
            SerializableGUID? fixedId = null)
        {
            TypedVariableModel<TValue> variable = new()
            {
                Name = name,
                IsExposed = true,
                m_Value = value
            };
            if (fixedId.HasValue)
                variable.ID = fixedId.Value;
            return variable;
        }

        private static BehaviorGraphNodeModel CreateBehaviorNode(
            BehaviorAuthoringGraph graph,
            Type runtimeNodeType,
            Vector2 position)
        {
            NodeInfo nodeInfo = Unity.Behavior.NodeRegistry.GetInfo(runtimeNodeType);
            if (nodeInfo == null)
                throw new InvalidOperationException(
                    $"{runtimeNodeType.Name} Unity Behavior 노드 정보를 찾지 못했습니다.");
            return (BehaviorGraphNodeModel)graph.CreateNode(
                nodeInfo.ModelType.Type,
                position,
                null,
                new object[] { nodeInfo });
        }

        private static void ConnectBehaviorNodes(
            BehaviorAuthoringGraph graph,
            NodeModel parent,
            NodeModel child)
        {
            if (!parent.TryDefaultOutputPortModel(out PortModel output)
                || !child.TryDefaultInputPortModel(out PortModel input))
            {
                throw new InvalidOperationException(
                    $"{parent.GetType().Name}와 {child.GetType().Name} 노드를 연결할 수 없습니다.");
            }
            graph.ConnectEdge(output, input);
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, bool transparent = false)
        {
            string path = $"{RootPath}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateParticleMaterial(string name, Color color)
        {
            string path = $"{RootPath}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("URP 파티클용 셰이더를 찾지 못했습니다.");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateCharacterWorldUiPrefab(Material lineMaterial)
        {
            GameObject root = new("CharacterWorldUI");
            root.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            CharacterWorldUiPresenter presenter = root.AddComponent<CharacterWorldUiPresenter>();

            GameObject canvasObject = new("WorldCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(220f, 70f);
            canvasRect.localScale = Vector3.one * 0.008f;

            Image healthBackground = CreateImage("HPBackground", canvasRect, new Color(0.06f, 0.06f, 0.06f, 0.9f));
            SetRect(healthBackground.rectTransform, new Vector2(0f, 20f), new Vector2(200f, 14f));
            Image healthFill = CreateImage("HPFill", healthBackground.rectTransform, new Color(0.18f, 0.9f, 0.25f, 1f));
            SetStretch(healthFill.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            healthFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;

            Text detailText = CreateText("DebugText", canvasRect, 14, TextAnchor.UpperCenter);
            SetRect(detailText.rectTransform, new Vector2(0f, -8f), new Vector2(260f, 44f));
            detailText.color = Color.white;

            GameObject selection = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selection.name = "SelectionIndicator";
            selection.transform.SetParent(root.transform, false);
            selection.transform.localPosition = new Vector3(0f, -2.28f, 0f);
            selection.transform.localScale = new Vector3(0.85f, 0.015f, 0.85f);
            Object.DestroyImmediate(selection.GetComponent<Collider>());
            selection.GetComponent<Renderer>().sharedMaterial = CreateOrUpdateMaterial("Selection", new Color(0.1f, 1f, 0.35f));
            selection.SetActive(false);

            LineRenderer targetLine = CreateLineRenderer("TargetLine", root.transform, lineMaterial, 0.055f);
            LineRenderer targetRing = CreateRingLineRenderer("TargetRing", root.transform, lineMaterial, 0.07f);
            LineRenderer peekLine = CreateLineRenderer("PeekSimulationLine", root.transform, lineMaterial, 0.055f);
            LineRenderer peekRing = CreateRingLineRenderer("PeekSimulationRing", root.transform, lineMaterial, 0.07f);
            targetLine.enabled = false;
            targetRing.enabled = false;
            peekLine.enabled = false;
            peekRing.enabled = false;
            presenter.SetEditorReferences(
                canvas,
                healthFill,
                detailText,
                selection,
                targetLine,
                targetRing,
                peekLine,
                peekRing,
                null);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterUiPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateUnitPrefab(
            string path,
            Team team,
            Material bodyMaterial,
            Material gunMaterial,
            Material lineMaterial,
            GameObject characterUiPrefab,
            BehaviorGraph behaviorGraph)
        {
            GameObject root = new(team == Team.Ally ? "AllyUnit" : "EnemyUnit");
            root.layer = UnitLayer;
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.5f;
            Combatant combatant = root.AddComponent<Combatant>();
            BehaviorGraphAgent behaviorAgent = root.AddComponent<BehaviorGraphAgent>();
            behaviorAgent.Graph = behaviorGraph;
            UnitTacticalBehaviorController behaviorController =
                root.AddComponent<UnitTacticalBehaviorController>();

            GameObject visualRoot = new("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.layer = UnitLayer;
            body.transform.SetParent(visualRoot.transform, false);
            body.transform.localPosition = Vector3.up;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "Gun";
            gun.layer = UnitLayer;
            gun.transform.SetParent(visualRoot.transform, false);
            gun.transform.localPosition = new Vector3(0f, 1.15f, 0.55f);
            gun.transform.localScale = new Vector3(0.18f, 0.18f, 0.9f);
            Object.DestroyImmediate(gun.GetComponent<Collider>());
            gun.GetComponent<Renderer>().sharedMaterial = gunMaterial;
            visualRoot.AddComponent<Flicker>();

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visualRoot.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.15f, 1.05f);
            Transform aimCenter = new GameObject("AimCenter").transform;
            aimCenter.SetParent(visualRoot.transform, false);
            aimCenter.localPosition = new Vector3(0f, 1.1f, 0f);

            ParticleSystem muzzleFlash = CreateParticleSystem("MuzzleFlash", muzzle, new Color(1f, 0.65f, 0.1f));
            LineRenderer shotTracer = CreateLineRenderer("ShotTracer", root.transform, lineMaterial, 0.075f);
            shotTracer.enabled = false;

            GameObject uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(characterUiPrefab, root.transform);
            uiInstance.name = "CharacterWorldUI";
            CharacterWorldUiPresenter worldUi = uiInstance.GetComponent<CharacterWorldUiPresenter>();

            combatant.SetEditorConfiguration(
                team,
                100,
                null,
                null,
                null,
                null,
                null,
                visualRoot.transform,
                muzzle,
                aimCenter,
                collider,
                muzzleFlash,
                shotTracer,
                worldUi);
            behaviorController.SetEditorReferences(
                combatant,
                null,
                null,
                null,
                null,
                null,
                behaviorAgent,
                team == Team.Enemy,
                true);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Color color)
        {
            GameObject particleObject = new(name, typeof(ParticleSystem));
            particleObject.transform.SetParent(parent, false);
            ParticleSystem particleSystem = particleObject.GetComponent<ParticleSystem>();
            ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            string materialName = name == "MuzzleFlash"
                ? "MuzzleFlashParticle"
                : $"{parent.root.name}_{name}Particle";
            particleRenderer.sharedMaterial = CreateOrUpdateParticleMaterial(materialName, color);
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.15f;
            main.startLifetime = 0.12f;
            main.startSpeed = 2.5f;
            main.startSize = 0.18f;
            main.startColor = color;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
            return particleSystem;
        }

        private static Camera CreateSceneCamera()
        {
            GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(12f, 18f, -10f), Quaternion.Euler(55f, 0f, 0f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 50f;
            return camera;
        }

        private static void CreateRtsCinemachineCamera(
            Camera outputCamera,
            Transform cameraTarget,
            GridMap gridMap,
            InputActionAsset inputActions,
            CombatTuning tuning)
        {
            if (outputCamera.GetComponent<CinemachineBrain>() == null)
                outputCamera.gameObject.AddComponent<CinemachineBrain>();

            GameObject cameraObject = new("RTSCinemachineCamera");
            CinemachineCamera cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
            CinemachineOrbitalFollow orbitalFollow = cameraObject.AddComponent<CinemachineOrbitalFollow>();
            cameraObject.AddComponent<CinemachineRotationComposer>();
            RtsCameraController controller = cameraObject.AddComponent<RtsCameraController>();

            cinemachineCamera.Follow = cameraTarget;
            cinemachineCamera.LookAt = cameraTarget;
            LensSettings lens = cinemachineCamera.Lens;
            lens.FieldOfView = 50f;
            cinemachineCamera.Lens = lens;

            orbitalFollow.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbitalFollow.HorizontalAxis.Wrap = true;
            orbitalFollow.HorizontalAxis.Value = tuning.CameraInitialYaw;
            orbitalFollow.VerticalAxis.Range = new Vector2(tuning.CameraMinimumPitch, tuning.CameraMaximumPitch);
            orbitalFollow.VerticalAxis.Value = tuning.CameraInitialPitch;
            orbitalFollow.Radius = tuning.CameraInitialDistance;

            controller.SetEditorReferences(inputActions, cameraTarget, outputCamera.transform, gridMap, tuning);
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new("Directional Light", typeof(Light));
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private static GridMap CreateGrid(Material evenMaterial, Material oddMaterial, Material coverMaterial)
        {
            GameObject root = new("GridMap");
            GridMap gridMap = root.AddComponent<GridMap>();
            gridMap.SetEditorConfiguration(12, 12, 2f, Vector3.zero, CoverCells);

            GameObject cells = new("Cells");
            cells.transform.SetParent(root.transform, false);
            for (int x = 0; x < 12; x++)
            {
                for (int z = 0; z < 12; z++)
                {
                    GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Cell_{x:00}_{z:00}";
                    cell.layer = GroundLayer;
                    cell.transform.SetParent(cells.transform, false);
                    cell.transform.position = gridMap.GridToWorld(new GridCoordinate(x, z)) + Vector3.down * 0.08f;
                    cell.transform.localScale = new Vector3(1.92f, 0.12f, 1.92f);
                    cell.GetComponent<Renderer>().sharedMaterial = (x + z) % 2 == 0 ? evenMaterial : oddMaterial;
                }
            }

            GameObject covers = new("Covers");
            covers.transform.SetParent(root.transform, false);
            foreach (Vector2Int blocked in CoverCells)
            {
                GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = $"Cover_{blocked.x:00}_{blocked.y:00}";
                cover.layer = CoverLayer;
                cover.transform.SetParent(covers.transform, false);
                cover.transform.position = gridMap.GridToWorld(new GridCoordinate(blocked.x, blocked.y)) + Vector3.up * 0.7f;
                cover.transform.localScale = new Vector3(1.8f, 1.4f, 1.8f);
                cover.GetComponent<Renderer>().sharedMaterial = coverMaterial;
            }
            return gridMap;
        }

        private static Transform CreateCameraTarget(GridMap gridMap)
        {
            GameObject target = new("CameraRigTarget");
            target.transform.position = gridMap.GridToWorld(new GridCoordinate(5, 5));
            return target.transform;
        }

        private static Combatant[] CreateCombatants(
            GameObject allyPrefab,
            GameObject enemyPrefab,
            GridMap gridMap,
            ShotEvaluator shotEvaluator,
            CombatDirector director,
            TacticalPositionEvaluator positionEvaluator,
            CombatTuning tuning,
            WeaponDefinition weapon,
            Transform cameraTransform)
        {
            Vector2Int[] allyCells = { new(1, 2), new(1, 5), new(2, 9) };
            Vector2Int[] enemyCells = { new(10, 1), new(10, 4), new(10, 7), new(10, 10) };
            List<Combatant> combatants = new();
            CreateTeamInstances(allyPrefab, Team.Ally, allyCells, combatants, gridMap, shotEvaluator, director, positionEvaluator, tuning, weapon, cameraTransform);
            CreateTeamInstances(enemyPrefab, Team.Enemy, enemyCells, combatants, gridMap, shotEvaluator, director, positionEvaluator, tuning, weapon, cameraTransform);
            return combatants.ToArray();
        }

        private static void CreateTeamInstances(
            GameObject prefab,
            Team team,
            Vector2Int[] cells,
            List<Combatant> combatants,
            GridMap gridMap,
            ShotEvaluator shotEvaluator,
            CombatDirector director,
            TacticalPositionEvaluator positionEvaluator,
            CombatTuning tuning,
            WeaponDefinition weapon,
            Transform cameraTransform)
        {
            for (int index = 0; index < cells.Length; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = $"{(team == Team.Ally ? "Ally" : "Enemy")}_{index + 1}";
                instance.transform.position = gridMap.GridToWorld(new GridCoordinate(cells[index].x, cells[index].y));
                instance.transform.rotation = team == Team.Ally ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

                Combatant combatant = instance.GetComponent<Combatant>();
                Transform visualRoot = instance.transform.Find("VisualRoot");
                Transform muzzle = visualRoot.Find("Muzzle");
                Transform aimCenter = visualRoot.Find("AimCenter");
                ParticleSystem muzzleFlash = muzzle.GetComponentInChildren<ParticleSystem>(true);
                LineRenderer shotTracer = instance.transform.Find("ShotTracer").GetComponent<LineRenderer>();
                CharacterWorldUiPresenter worldUi = instance.GetComponentInChildren<CharacterWorldUiPresenter>(true);
                worldUi.SetEditorCameraTransform(cameraTransform);
                combatant.SetEditorConfiguration(
                    team,
                    100,
                    weapon,
                    tuning,
                    gridMap,
                    shotEvaluator,
                    director,
                    visualRoot,
                    muzzle,
                    aimCenter,
                    instance.GetComponent<Collider>(),
                    muzzleFlash,
                    shotTracer,
                    worldUi);

                BehaviorGraphAgent behaviorAgent = instance.GetComponent<BehaviorGraphAgent>();
                UnitTacticalBehaviorController behaviorController =
                    instance.GetComponent<UnitTacticalBehaviorController>();
                behaviorController.SetEditorReferences(
                    combatant,
                    gridMap,
                    shotEvaluator,
                    director,
                    positionEvaluator,
                    tuning,
                    behaviorAgent,
                    team == Team.Enemy,
                    true);
                combatants.Add(combatant);
            }
        }

        private static CombatHudController CreateSceneHud(CombatDirector director)
        {
            GameObject canvasObject = new("CombatHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            CombatHudController hud = canvasObject.AddComponent<CombatHudController>();

            Text stateText = CreateText("StateText", canvasObject.transform, 24, TextAnchor.UpperLeft);
            SetHudRect(stateText.rectTransform, new Vector2(20f, -20f), new Vector2(320f, 40f), new Vector2(0f, 1f));
            Text modeText = CreateText("ModeText", canvasObject.transform, 22, TextAnchor.UpperLeft);
            SetHudRect(modeText.rectTransform, new Vector2(20f, -60f), new Vector2(360f, 40f), new Vector2(0f, 1f));
            Text debugText = CreateText("DebugText", canvasObject.transform, 22, TextAnchor.UpperLeft);
            SetHudRect(debugText.rectTransform, new Vector2(20f, -100f), new Vector2(360f, 40f), new Vector2(0f, 1f));
            Button allyFullAutoButton = CreateButton(
                "AllyFullAutoButton",
                canvasObject.transform,
                new Color(0.24f, 0.28f, 0.34f, 0.96f),
                out Text allyFullAutoButtonText);
            allyFullAutoButtonText.text = "ALLY AI: COMMAND";
            SetHudRect(
                allyFullAutoButton.GetComponent<RectTransform>(),
                new Vector2(20f, -145f),
                new Vector2(320f, 46f),
                new Vector2(0f, 1f));
            Text controls = CreateText("Controls", canvasObject.transform, 18, TextAnchor.LowerLeft);
            controls.text = "LMB Select/Target | RMB Move | T Target | Q Peek | Space Pause | 1/2/3 Speed | F1 Debug | R Restart";
            SetHudRect(controls.rectTransform, new Vector2(20f, 20f), new Vector2(1200f, 36f), new Vector2(0f, 0f));

            Image selectedInfoPanel = CreateImage("SelectedInfoPanel", canvasObject.transform, new Color(0.035f, 0.045f, 0.06f, 0.92f));
            SetHudRect(selectedInfoPanel.rectTransform, new Vector2(-24f, -24f), new Vector2(410f, 470f), new Vector2(1f, 1f));
            Text selectedInfoTitle = CreateText("SelectedInfoTitle", selectedInfoPanel.transform, 26, TextAnchor.UpperLeft);
            selectedInfoTitle.fontStyle = FontStyle.Bold;
            selectedInfoTitle.color = new Color(0.35f, 0.85f, 1f, 1f);
            SetHudRect(selectedInfoTitle.rectTransform, new Vector2(18f, -18f), new Vector2(374f, 42f), new Vector2(0f, 1f));
            Text selectedInfoBody = CreateText("SelectedInfoBody", selectedInfoPanel.transform, 19, TextAnchor.UpperLeft);
            selectedInfoBody.lineSpacing = 1.1f;
            SetHudRect(selectedInfoBody.rectTransform, new Vector2(18f, -66f), new Vector2(374f, 380f), new Vector2(0f, 1f));

            Image resultPanel = CreateImage("ResultPanel", canvasObject.transform, new Color(0f, 0f, 0f, 0.78f));
            SetHudRect(resultPanel.rectTransform, Vector2.zero, new Vector2(620f, 250f), new Vector2(0.5f, 0.5f));
            Text resultText = CreateText("ResultText", resultPanel.transform, 48, TextAnchor.MiddleCenter);
            SetStretch(resultText.rectTransform, Vector2.zero, Vector2.zero);
            resultPanel.gameObject.SetActive(false);

            hud.SetEditorReferences(
                stateText,
                modeText,
                debugText,
                resultPanel.gameObject,
                resultText,
                selectedInfoPanel.gameObject,
                selectedInfoTitle,
                selectedInfoBody,
                allyFullAutoButton,
                allyFullAutoButtonText,
                director);
            return hud;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Color color,
            out Text label)
        {
            GameObject gameObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            label = CreateText("Label", gameObject.transform, 22, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            SetStretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetHudRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static LineRenderer CreateLineRenderer(string name, Transform parent, Material material, float width)
        {
            GameObject gameObject = new(name, typeof(LineRenderer));
            gameObject.transform.SetParent(parent, false);
            LineRenderer line = gameObject.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static LineRenderer CreateRingLineRenderer(string name, Transform parent, Material material, float width)
        {
            LineRenderer ring = CreateLineRenderer(name, parent, material, width);
            ring.loop = true;
            ring.positionCount = 33;
            return ring;
        }

        private static GridCombatIndicator CreateGridCombatIndicator(
            Transform parent,
            GridMap gridMap,
            ShotEvaluator shotEvaluator,
            CombatTuning tuning,
            Material viewRangeMaterial,
            Material shootableCellMaterial)
        {
            Transform existing = parent.Find("GridCombatIndicator");
            GameObject root = existing != null ? existing.gameObject : new GameObject("GridCombatIndicator");
            root.transform.SetParent(parent, false);
            GridCombatIndicator indicator = root.GetComponent<GridCombatIndicator>();
            if (indicator == null)
                indicator = root.AddComponent<GridCombatIndicator>();

            MeshFilter viewCells = EnsureIndicatorMeshObject(
                "ViewCells",
                root.transform,
                viewRangeMaterial,
                0);
            MeshFilter shootableCells = EnsureIndicatorMeshObject(
                "ShootableCells",
                root.transform,
                shootableCellMaterial,
                1);
            indicator.SetEditorReferences(gridMap, shotEvaluator, tuning, viewCells, shootableCells);
            EditorUtility.SetDirty(indicator);
            return indicator;
        }

        private static MeshFilter EnsureIndicatorMeshObject(
            string name,
            Transform parent,
            Material material,
            int sortingOrder)
        {
            Transform existing = parent.Find(name);
            GameObject meshObject = existing != null
                ? existing.gameObject
                : new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.transform.SetParent(parent, false);
            MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = sortingOrder;
            return meshFilter;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
