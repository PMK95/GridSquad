using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GridSquad.Editor
{
    public static class PrototypeLoopConfigurator
    {
        private const string DataRoot = "Assets/GridSquad/Data/Progression";
        private const string BasicEquipmentRoot = "Assets/GridSquad/Data/Equipment/Basic";
        private const string BasicIconRoot = "Assets/GridSquad/Art/UI/Equipment/Generated/Basic";
        private const string BasicAtlasPath =
            "Assets/GridSquad/Art/UI/Equipment/Generated/BasicEquipmentAtlas.png";
        private const string PrefabRoot = "Assets/GridSquad/Prefabs/UI";
        private const string EntryScenePath = "Assets/GridSquad/Scenes/Entry.unity";
        private const string LoadingScenePath =
            "Assets/GridSquad/Scenes/GridSquadLoading.unity";
        private const string BaseScenePath = "Assets/GridSquad/Scenes/BasePrototype.unity";
        private const string CombatScenePath = "Assets/GridSquad/Scenes/CombatFeasibility.unity";

        [MenuItem("Tools/GridSquad/프로토타입 루프 구성")]
        public static void ConfigurePrototypeLoop()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(BasicEquipmentRoot);
            EnsureFolder(BasicIconRoot);
            ConfigureBasicEquipment();
            UnitStatCatalog statCatalog =
                AssetDatabase.LoadAssetAtPath<UnitStatCatalog>(
                    "Assets/GridSquad/Data/Stats/UnitStatCatalog.asset");
            UnitStatDefinition traumaResistance = CreateOrLoad<UnitStatDefinition>(
                "Assets/GridSquad/Data/Stats/TraumaResistanceStat.asset");
            traumaResistance.SetEditorConfiguration(
                "trauma_resistance",
                "트라우마 저항",
                0f,
                -90f,
                false,
                80,
                "후유증 단계에 도달하기 위한 누적 트라우마 기준을 백분율로 조정합니다.",
                UnitStatCategory.Survivability,
                UnitStatDisplayFormat.PercentagePoints);
            statCatalog.SetEditorConfiguration(
                statCatalog.MaximumHealth,
                statCatalog.MovementSpeedMultiplier,
                statCatalog.HitChanceBonusPercent,
                statCatalog.DamageMultiplier,
                statCatalog.CarryCapacity,
                statCatalog.Defense,
                statCatalog.FireRateMultiplier,
                traumaResistance);
            EditorUtility.SetDirty(statCatalog);
            EditorUtility.SetDirty(traumaResistance);

            UnitDefinition[] units = LoadAssets<UnitDefinition>(
                "Assets/GridSquad/Data/Units");
            for (int index = 0; index < units.Length; index++)
            {
                units[index].SetEditorUnitId(CreateStableId(units[index].name));
                EditorUtility.SetDirty(units[index]);
            }
            ItemDefinition[] items = LoadAssets<ItemDefinition>(
                "Assets/GridSquad/Data/Equipment");

            AftereffectDefinition minor = CreateAftereffect(
                "MinorFatigue",
                "minor_fatigue",
                "전투 피로",
                AftereffectSeverity.Minor,
                1,
                new UnitStatModifier(
                    statCatalog.FireRateMultiplier,
                    UnitStatModifierOperation.Multiply,
                    0.9f));
            AftereffectDefinition major = CreateAftereffect(
                "MajorInjury",
                "major_injury",
                "중상 후유증",
                AftereffectSeverity.Major,
                2,
                new UnitStatModifier(
                    statCatalog.MaximumHealth,
                    UnitStatModifierOperation.Multiply,
                    0.85f));
            AftereffectDefinition severe = CreateAftereffect(
                "SevereTrauma",
                "severe_trauma",
                "심각한 전투 후유증",
                AftereffectSeverity.Severe,
                3,
                new UnitStatModifier(
                    statCatalog.MaximumHealth,
                    UnitStatModifierOperation.Multiply,
                    0.7f));
            AftereffectRuleSet rules = CreateOrLoad<AftereffectRuleSet>(
                $"{DataRoot}/PrototypeAftereffectRules.asset");
            rules.SetEditorConfiguration(
                0.5f,
                1f,
                2f,
                new[] { minor, major, severe });
            EditorUtility.SetDirty(rules);

            EncounterDefinition[] encounters = new EncounterDefinition[3];
            int[] enemyCounts = { 2, 3, 4 };
            for (int stageIndex = 0; stageIndex < encounters.Length; stageIndex++)
            {
                encounters[stageIndex] = CreateOrLoad<EncounterDefinition>(
                    $"{DataRoot}/PrototypeEncounter{stageIndex + 1}.asset");
                EnemySpawnDefinition[] spawns = Enumerable.Range(0, enemyCounts[stageIndex])
                    .Select(index => new EnemySpawnDefinition(
                        $"enemy_{index + 1}",
                        units[(stageIndex + index) % units.Length]))
                    .ToArray();
                encounters[stageIndex].SetEditorConfiguration(
                    $"prototype_encounter_{stageIndex + 1}",
                    new[] { "ally_1", "ally_2", "ally_3" },
                    spawns);
                EditorUtility.SetDirty(encounters[stageIndex]);
            }

            MissionDefinition mission = CreateOrLoad<MissionDefinition>(
                $"{DataRoot}/PrototypeMission.asset");
            mission.SetEditorConfiguration(
                "prototype_mission",
                "폐허 정찰 임무",
                1,
                3,
                new[]
                {
                    new MissionStageDefinition(
                        "stage_1",
                        CombatScenePath,
                        encounters[0],
                        true),
                    new MissionStageDefinition(
                        "stage_2",
                        CombatScenePath,
                        encounters[1],
                        true),
                    new MissionStageDefinition(
                        "stage_3",
                        CombatScenePath,
                        encounters[2],
                        false)
                });
            EditorUtility.SetDirty(mission);

            GameContentCatalog catalog = CreateOrLoad<GameContentCatalog>(
                $"{DataRoot}/GameContentCatalog.asset");
            EquipmentLayoutDefinition equipmentLayout =
                AssetDatabase.LoadAssetAtPath<EquipmentLayoutDefinition>(
                    "Assets/GridSquad/Data/Equipment/Layouts/CombatEquipmentLayout.asset");
            catalog.SetEditorConfiguration(
                units,
                items,
                new[] { mission },
                rules,
                statCatalog,
                equipmentLayout);
            EditorUtility.SetDirty(catalog);
            catalog.BuildIndexes();
            _ = new BaseStateFactory().Create(catalog);

            GameObject applicationPrefab = CreateApplicationPrefab(catalog);
            CreateBaseScene(applicationPrefab);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(EntryScenePath, true),
                new EditorBuildSettingsScene(LoadingScenePath, true),
                new EditorBuildSettingsScene(BaseScenePath, true),
                new EditorBuildSettingsScene(CombatScenePath, true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[프로토타입 루프] 기지, 3스테이지 임무, 후유증 규칙, UI 프리팹 구성을 완료했습니다.");
        }

        private static AftereffectDefinition CreateAftereffect(
            string assetName,
            string id,
            string displayName,
            AftereffectSeverity severity,
            int restMissions,
            UnitStatModifier modifier)
        {
            AftereffectDefinition definition = CreateOrLoad<AftereffectDefinition>(
                $"{DataRoot}/{assetName}.asset");
            definition.SetEditorConfiguration(
                id,
                displayName,
                severity,
                restMissions,
                false,
                new[] { modifier });
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureBasicEquipment()
        {
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(BasicAtlasPath);
            if (atlas == null)
                throw new InvalidOperationException($"기본형 장비 아이콘 아틀라스가 없습니다: {BasicAtlasPath}");

            int cellWidth = atlas.width / 3;
            int cellHeight = atlas.height / 3;
            Sprite[] icons =
            {
                CreateOrLoadSprite("BasicRifle", atlas, 0, 2, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicShield", atlas, 1, 2, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicHelmet", atlas, 2, 2, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicVest", atlas, 0, 1, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicLegGuards", atlas, 1, 1, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicGloves", atlas, 2, 1, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicBoots", atlas, 0, 0, cellWidth, cellHeight),
                CreateOrLoadSprite("BasicUtilityPouch", atlas, 1, 0, cellWidth, cellHeight)
            };

            WeaponDefinition rifle = CreateOrLoad<WeaponDefinition>(
                $"{BasicEquipmentRoot}/BasicRifle.asset");
            WeaponDefinition rifleReference =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                    "Assets/GridSquad/Data/Equipment/Weapons/Definitions/ARWeaponDefinition.asset");
            rifle.SetEditorEquipmentPresentation(
                "basic_rifle",
                "기본형 소총",
                "추가 특수 능력이 없는 표준 제식 소총입니다.",
                icons[0],
                4.2f,
                1);
            rifle.SetEditorStatModifiers(Array.Empty<UnitStatModifier>());
            rifle.SetEditorActionGrants(Array.Empty<ItemActionGrant>());
            rifle.SetEditorDurability(100, 1);
            rifle.SetEditorHandedness(WeaponHandedness.TwoHanded);
            if (rifleReference != null)
            {
                rifle.PresentationPrefab = rifleReference.PresentationPrefab;
                rifle.Damage = rifleReference.Damage;
                rifle.AimEnterDuration = rifleReference.AimEnterDuration;
                rifle.AimedShotInterval = rifleReference.AimedShotInterval;
                rifle.MagazineCapacity = rifleReference.MagazineCapacity;
                rifle.StartingReserveAmmo = rifleReference.StartingReserveAmmo;
                rifle.ReloadDuration = rifleReference.ReloadDuration;
                rifle.RangeInCells = rifleReference.RangeInCells;
                rifle.BaseHitChancePercent = rifleReference.BaseHitChancePercent;
                rifle.SetEditorAttackConfiguration(
                    rifleReference.AttackBehavior,
                    rifleReference.HitEffects.ToArray());
                rifle.SetEditorWorldPresentationPrefab(
                    rifleReference.WorldPresentationPrefab);
            }
            EditorUtility.SetDirty(rifle);

            OffHandDefinition shield = CreateOrLoad<OffHandDefinition>(
                $"{BasicEquipmentRoot}/BasicShield.asset");
            OffHandDefinition shieldReference =
                AssetDatabase.LoadAssetAtPath<OffHandDefinition>(
                    "Assets/GridSquad/Data/Equipment/Weapons/Definitions/RiotShield.asset");
            ConfigureBasicEquippable(
                shield,
                "basic_shield",
                "기본형 보조 방패",
                "추가 특수 능력이 없는 표준 보조 방패입니다.",
                icons[1],
                3.5f);
            shield.SetEditorPresentationPrefab(shieldReference?.PresentationPrefab);
            shield.SetEditorWorldPresentationPrefab(
                shieldReference?.WorldPresentationPrefab);
            EditorUtility.SetDirty(shield);

            CreateBasicArmor(
                "BasicHelmet",
                "basic_helmet",
                "기본형 전투 헬멧",
                icons[2],
                EquipmentSlotKind.Head,
                2,
                1.8f);
            CreateBasicArmor(
                "BasicVest",
                "basic_vest",
                "기본형 방탄복",
                icons[3],
                EquipmentSlotKind.Torso,
                4,
                4.5f);
            CreateBasicArmor(
                "BasicLegGuards",
                "basic_leg_guards",
                "기본형 다리 보호대",
                icons[4],
                EquipmentSlotKind.Legs,
                2,
                2.8f);
            CreateBasicArmor(
                "BasicGloves",
                "basic_gloves",
                "기본형 전술 장갑",
                icons[5],
                EquipmentSlotKind.Hands,
                1,
                1.2f);
            CreateBasicArmor(
                "BasicBoots",
                "basic_boots",
                "기본형 전투화",
                icons[6],
                EquipmentSlotKind.Feet,
                1,
                1.6f);

            AdditionalEquipmentDefinition pouch =
                CreateOrLoad<AdditionalEquipmentDefinition>(
                    $"{BasicEquipmentRoot}/BasicUtilityPouch.asset");
            ConfigureBasicEquippable(
                pouch,
                "basic_utility_pouch",
                "기본형 전술 파우치",
                "특수 기능 없이 물품을 고정하는 표준 전술 파우치입니다.",
                icons[7],
                0.8f);
            pouch.SetEditorGrantedActions(Array.Empty<CombatActionDefinition>());
            pouch.SetEditorPassiveConfiguration(
                SupportEquipmentPassiveKind.None,
                1,
                10f);
            EditorUtility.SetDirty(pouch);
        }

        private static void CreateBasicArmor(
            string assetName,
            string id,
            string displayName,
            Sprite icon,
            EquipmentSlotKind slotKind,
            int defense,
            float weight)
        {
            ArmorDefinition armor = CreateOrLoad<ArmorDefinition>(
                $"{BasicEquipmentRoot}/{assetName}.asset");
            ConfigureBasicEquippable(
                armor,
                id,
                displayName,
                "특수 기능 없이 기본 방호만 제공하는 표준 장비입니다.",
                icon,
                weight);
            armor.SetEditorArmorSlotKind(slotKind);
            armor.SetEditorDefense(defense);
            armor.SetEditorMaximumBlockCount(100);
            EditorUtility.SetDirty(armor);
        }

        private static void ConfigureBasicEquippable(
            EquippableDefinition equipment,
            string id,
            string displayName,
            string description,
            Sprite icon,
            float weight)
        {
            equipment.SetEditorEquipmentPresentation(
                id,
                displayName,
                description,
                icon,
                weight,
                1);
            equipment.SetEditorStatModifiers(Array.Empty<UnitStatModifier>());
            equipment.SetEditorActionGrants(Array.Empty<ItemActionGrant>());
            equipment.SetEditorDurability(100, 1);
        }

        private static Sprite CreateOrLoadSprite(
            string name,
            Texture2D atlas,
            int column,
            int row,
            int cellWidth,
            int cellHeight)
        {
            string path = $"{BasicIconRoot}/{name}.asset";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;
            Rect rect = new(
                column * cellWidth,
                row * cellHeight,
                cellWidth,
                cellHeight);
            Sprite sprite = Sprite.Create(
                atlas,
                rect,
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static GameObject CreateApplicationPrefab(GameContentCatalog catalog)
        {
            GameObject root = new("PrototypeGameApplication");
            PrototypeGameApplication application =
                root.AddComponent<PrototypeGameApplication>();
            application.SetEditorConfiguration(catalog, BaseScenePath);

            GameObject canvasObject = new(
                "PrototypeLoopCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PrototypeLoopUiController));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject basePanel = CreatePanel(
                "BasePanel",
                canvasObject.transform,
                new Color(0.035f, 0.045f, 0.065f, 0.98f));
            TMP_Text title = CreateText(
                "Title",
                basePanel.transform,
                "기지 작전실",
                42,
                TextAlignmentOptions.Center);
            SetRect(title.rectTransform, 0.15f, 0.88f, 0.85f, 0.98f);
            TMP_Text briefing = CreateText(
                "Briefing",
                basePanel.transform,
                "출격 대원을 최대 3명 선발하세요. 복귀 시 체력은 회복되지만 후유증과 장비 상태는 유지됩니다.",
                23,
                TextAlignmentOptions.Center);
            SetRect(briefing.rectTransform, 0.12f, 0.79f, 0.88f, 0.88f);

            GameObject roster = new(
                "RosterContent",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            roster.transform.SetParent(basePanel.transform, false);
            SetRect((RectTransform)roster.transform, 0.18f, 0.24f, 0.82f, 0.78f);
            VerticalLayoutGroup layout = roster.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            roster.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            Toggle togglePrefab = CreateRosterTogglePrefab();
            Button launch = CreateButton("LaunchButton", basePanel.transform, "임무 출발");
            SetRect((RectTransform)launch.transform, 0.57f, 0.08f, 0.82f, 0.17f);
            Button repair = CreateButton("RepairAllButton", basePanel.transform, "전체 무료 수리");
            SetRect((RectTransform)repair.transform, 0.18f, 0.08f, 0.43f, 0.17f);
            TMP_Text baseStatus = CreateText(
                "BaseStatus",
                basePanel.transform,
                "1~3명을 선발하세요.",
                21,
                TextAlignmentOptions.Center);
            SetRect(baseStatus.rectTransform, 0.18f, 0.17f, 0.82f, 0.23f);

            GameObject betweenPanel = CreatePanel(
                "BetweenStagePanel",
                canvasObject.transform,
                new Color(0.02f, 0.025f, 0.04f, 0.95f));
            TMP_Text stageStatus = CreateText(
                "StageStatus",
                betweenPanel.transform,
                "스테이지 완료",
                30,
                TextAlignmentOptions.Center);
            SetRect(stageStatus.rectTransform, 0.2f, 0.52f, 0.8f, 0.72f);
            Button continueButton = CreateButton(
                "ContinueButton",
                betweenPanel.transform,
                "다음 스테이지");
            SetRect((RectTransform)continueButton.transform, 0.52f, 0.34f, 0.76f, 0.44f);
            Button extractButton = CreateButton(
                "ExtractButton",
                betweenPanel.transform,
                "기지로 철수");
            SetRect((RectTransform)extractButton.transform, 0.24f, 0.34f, 0.48f, 0.44f);
            betweenPanel.SetActive(false);

            canvasObject.GetComponent<PrototypeLoopUiController>().SetEditorReferences(
                basePanel,
                roster.transform,
                togglePrefab,
                launch,
                repair,
                baseStatus,
                betweenPanel,
                stageStatus,
                continueButton,
                extractButton);

            string prefabPath = $"{PrefabRoot}/PrototypeGameApplication.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Toggle CreateRosterTogglePrefab()
        {
            GameObject root = new(
                "PrototypeRosterToggle",
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle),
                typeof(LayoutElement));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 86f);
            root.GetComponent<Image>().color = new Color(0.11f, 0.14f, 0.19f, 1f);
            root.GetComponent<LayoutElement>().preferredHeight = 86f;

            GameObject checkmark = new("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(root.transform, false);
            SetRect((RectTransform)checkmark.transform, 0.02f, 0.25f, 0.07f, 0.75f);
            checkmark.GetComponent<Image>().color = new Color(0.2f, 0.85f, 0.55f, 1f);
            TMP_Text label = CreateText(
                "Label",
                root.transform,
                "대원",
                18,
                TextAlignmentOptions.Left);
            SetRect(label.rectTransform, 0.09f, 0.05f, 0.98f, 0.95f);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = root.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            string path = $"{PrefabRoot}/PrototypeRosterToggle.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<Toggle>();
        }

        private static void CreateBaseScene(GameObject applicationPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(applicationPrefab, scene);
            GameObject eventSystem = new(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            EditorSceneManager.SaveScene(scene, BaseScenePath);
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Color color)
        {
            GameObject panel = new(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            SetRect((RectTransform)panel.transform, 0f, 0f, 1f, 1f);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            GameObject buttonObject = new(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color =
                new Color(0.13f, 0.35f, 0.5f, 1f);
            TMP_Text text = CreateText(
                "Label",
                buttonObject.transform,
                label,
                22,
                TextAlignmentOptions.Center);
            SetRect(text.rectTransform, 0f, 0f, 1f, 1f);
            return buttonObject.GetComponent<Button>();
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T[] LoadAssets<T>(string folder) where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .OrderBy(asset => asset.name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string CreateStableId(string value)
            => string.Concat(
                value.Select(character =>
                    char.IsLetterOrDigit(character)
                        ? char.ToLowerInvariant(character)
                        : '_'));

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
