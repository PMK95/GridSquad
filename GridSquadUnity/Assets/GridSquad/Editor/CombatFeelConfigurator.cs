using System;
using System.Collections.Generic;
using System.Linq;
using GridSquad;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using MoreMountains.Tools;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GridSquadEditor
{
    public static class CombatFeelConfigurator
    {
        private const string RootPath = "Assets/GridSquad";
        private const string UnitBasePath = RootPath + "/Prefabs/UnitBase.prefab";
        private const string CharacterModelPath = RootPath + "/Prefabs/CharacterModel.prefab";
        private const string AllyUnitPath = RootPath + "/Prefabs/AllyUnit.prefab";
        private const string EnemyUnitPath = RootPath + "/Prefabs/EnemyUnit.prefab";
        private const string WorldUiPath = RootPath + "/Prefabs/CharacterWorldUI.prefab";
        private const string FeedbackRootPath = RootPath + "/Feedbacks";
        private const string FeedbackPrefabPath = FeedbackRootPath + "/Prefabs/DamageFloatingText.prefab";
        private const string DamageChannelPath = FeedbackRootPath + "/Channels/DamageTextChannel.asset";
        private const string CameraChannelPath = FeedbackRootPath + "/Channels/CameraShakeChannel.asset";
        private const string CameraNoisePath = FeedbackRootPath + "/Camera/GridSquadCombatNoise.asset";
        private const string FeelFloatingTextPath = "Assets/Plugins/Feel/MMFeedbacks/MMFeedbacksForThirdParty/TextMeshPro/MMFloatingText/Prefabs/MMFloatingTextMeshPro.prefab";
        private const string FeelNoisePath = "Assets/Plugins/Feel/MMFeedbacks/MMFeedbacksForThirdParty/Cinemachine/Resources/MM_6D_Shake.asset";
        private const string AudioRootPath = RootPath + "/Audio";
        private const string SoundMixerPath = AudioRootPath + "/GridSquadAudioMixer.mixer";
        private const string SoundSettingsPath = AudioRootPath + "/GridSquadSoundSettings.asset";
        private const string FeelSoundMixerPath = "Assets/Plugins/Feel/MMTools/Core/MMAudio/MMSoundManager/Settings/MMSoundManagerAudioMixer.mixer";
        private const string DebugRootPath = RootPath + "/Debug";
        private const string DebugMenuDataPath = DebugRootPath + "/CombatDebugMenuData.asset";
        private const string FeelDebugMenuRoot = "Assets/Plugins/Feel/MMTools/Accessories/MMDebugMenu/Prefabs";
        private const string FeelDebugMenuPrefabPath = FeelDebugMenuRoot + "/MMDebugMenu.prefab";

        [MenuItem("GridSquad/Feel 전투 연출 구성")]
        public static void ConfigureFeelPresentation()
        {
            ConfigurePrefabAssets();
            ConfigureActiveScene();
            ValidateFeelPresentation();
            AssetDatabase.SaveAssets();
            Debug.Log("Feel 기반 전투·화면 연출 구성이 완료되었습니다.");
        }

        [MenuItem("GridSquad/Feel 편의 기능 구성")]
        public static void ConfigureFeelConveniences()
        {
            EnsureConvenienceAssets();
            ConfigureWorldUiPrefab();
            ConfigureSoundManager();
            ConfigureDebugTools();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            ValidateFeelPresentation();
            AssetDatabase.SaveAssets();
            Debug.Log("Feel 편의 기능 구성을 완료했습니다.");
        }

        public static void ConfigurePrefabAssets()
        {
            EnsureFeedbackAssets();
            EnsureConvenienceAssets();
            RemoveMissingPresentationScriptsFromCharacterModel();
            ConfigureWorldUiPrefab();
            ConfigureUnitBasePrefab();
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureActiveScene()
        {
            MMChannel damageChannel = LoadRequiredAsset<MMChannel>(DamageChannelPath);
            MMChannel cameraChannel = LoadRequiredAsset<MMChannel>(CameraChannelPath);
            ConfigureFeedbackRig(damageChannel);
            ConfigureCinemachine(cameraChannel);
            ConfigureHudFeedbacks();
            ConfigureSceneRuntimeReferences();
            ConfigureDebugTools();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        [MenuItem("GridSquad/Feel 전투 연출 검증")]
        public static void ValidateFeelPresentation()
        {
            GameObject unitBase = LoadRequiredAsset<GameObject>(UnitBasePath);
            CombatantFeedbackPresenter presenter = unitBase.GetComponentInChildren<CombatantFeedbackPresenter>(true);
            if (presenter == null)
                throw new InvalidOperationException("UnitBase에 CombatantFeedbackPresenter가 없습니다.");

            string[] requiredPlayerNames =
            {
                "ShotFeedbacks", "HitFeedbacks", "DamageTextFeedbacks", "MissTextFeedbacks",
                "DeathVisualFeedbacks", "DeathShakeFeedbacks", "DefeatShakeFeedbacks"
            };
            foreach (string playerName in requiredPlayerNames)
            {
                Transform playerTransform = FindDescendant(unitBase.transform, playerName);
                if (playerTransform == null || playerTransform.GetComponent<MMF_Player>() == null)
                    throw new InvalidOperationException($"UnitBase의 {playerName} 구성이 누락되었습니다.");
            }

            ValidateCameraShakePlacement(unitBase);
            ValidateVariantSource(AllyUnitPath);
            ValidateVariantSource(EnemyUnitPath);

            GameObject worldUi = LoadRequiredAsset<GameObject>(WorldUiPath);
            if (worldUi.GetComponentInChildren<MMHealthBar>(true) == null
                || worldUi.GetComponentInChildren<MMProgressBar>(true) == null
                || worldUi.GetComponentInChildren<MMBillboard>(true) == null
                || FindDescendant(worldUi.transform, "OutOfAmmoText")?.GetComponent<Text>() == null)
                throw new InvalidOperationException("CharacterWorldUI의 Feel HP 바 구성이 누락되었습니다.");

            MMFloatingTextSpawner spawner = Object.FindFirstObjectByType<MMFloatingTextSpawner>();
            if (spawner == null || spawner.PoolSize != 32 || !spawner.PoolCanExpand)
                throw new InvalidOperationException("플로팅 텍스트 Spawner의 32개 확장 풀이 구성되지 않았습니다.");
            if (Object.FindFirstObjectByType<MMTimeManager>() == null)
                throw new InvalidOperationException("씬에 MMTimeManager가 없습니다.");

            MMSoundManager soundManager = Object.FindFirstObjectByType<MMSoundManager>();
            if (soundManager == null
                || soundManager.settingsSo != LoadRequiredAsset<MMSoundManagerSettingsSO>(SoundSettingsPath)
                || soundManager.GetComponent<FeelConvenienceRuntimeBootstrap>() == null)
            {
                throw new InvalidOperationException("MMSoundManager JSON 설정이 누락되었습니다.");
            }
            MMDebugMenu debugMenu = Object.FindFirstObjectByType<MMDebugMenu>();
            if (debugMenu == null
                || debugMenu.Data != LoadRequiredAsset<MMDebugMenuData>(DebugMenuDataPath)
                || debugMenu.GetComponent<CombatDebugMenuBridge>() == null)
            {
                throw new InvalidOperationException("전투 디버그 메뉴 구성이 누락되었습니다.");
            }
            if (Object.FindFirstObjectByType<MMFPSCounter>() == null)
                throw new InvalidOperationException("FPS 카운터가 구성되지 않았습니다.");

            CinemachineCamera camera = Object.FindFirstObjectByType<CinemachineCamera>();
            if (camera == null
                || camera.GetComponent<CinemachineBasicMultiChannelPerlin>() == null
                || camera.GetComponent<MMCinemachineCameraShaker>() == null)
                throw new InvalidOperationException("Cinemachine Noise 또는 Feel 카메라 Shaker가 누락되었습니다.");

            Debug.Log("Feel 구조 검증을 통과했습니다. 셰이크는 사망과 패배 피드백에만 존재합니다.");
        }

        private static void EnsureFeedbackAssets()
        {
            EnsureFolder("Assets", "GridSquad");
            EnsureFolder(RootPath, "Feedbacks");
            EnsureFolder(FeedbackRootPath, "Prefabs");
            EnsureFolder(FeedbackRootPath, "Channels");
            EnsureFolder(FeedbackRootPath, "Camera");

            CopyAssetIfMissing(FeelFloatingTextPath, FeedbackPrefabPath);
            CopyAssetIfMissing(FeelNoisePath, CameraNoisePath);
            CreateChannelIfMissing(DamageChannelPath);
            CreateChannelIfMissing(CameraChannelPath);
        }

        private static void EnsureConvenienceAssets()
        {
            EnsureFolder(RootPath, "Audio");
            EnsureFolder(RootPath, "Debug");
            CopyAssetIfMissing(FeelSoundMixerPath, SoundMixerPath);
            ConfigureSoundSettingsAsset();
            ConfigureDebugMenuDataAsset();
        }

        private static void ConfigureSoundSettingsAsset()
        {
            MMSoundManagerSettingsSO settings =
                AssetDatabase.LoadAssetAtPath<MMSoundManagerSettingsSO>(SoundSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MMSoundManagerSettingsSO>();
                settings.name = "GridSquadSoundSettings";
                AssetDatabase.CreateAsset(settings, SoundSettingsPath);
            }

            AudioMixer mixer = LoadRequiredAsset<AudioMixer>(SoundMixerPath);
            settings.TargetAudioMixer = mixer;
            settings.MasterAudioMixerGroup = FindRequiredMixerGroup(mixer, "Master");
            settings.MusicAudioMixerGroup = FindRequiredMixerGroup(mixer, "Music");
            settings.SfxAudioMixerGroup = FindRequiredMixerGroup(mixer, "Sfx");
            settings.UIAudioMixerGroup = FindRequiredMixerGroup(mixer, "UI");
            settings.Settings ??= new MMSoundManagerSettings();
            settings.Settings.OverrideMixerSettings = true;
            settings.Settings.MasterVolumeParameter = "MasterVolume";
            settings.Settings.MusicVolumeParameter = "MusicVolume";
            settings.Settings.SfxVolumeParameter = "SfxVolume";
            settings.Settings.UIVolumeParameter = "UiVolume";
            settings.Settings.AutoLoad = true;
            settings.Settings.AutoSave = true;
            EditorUtility.SetDirty(settings);
        }

        private static AudioMixerGroup FindRequiredMixerGroup(AudioMixer mixer, string groupName)
        {
            AudioMixerGroup group = mixer.FindMatchingGroups(string.Empty)
                .FirstOrDefault(candidate => candidate.name == groupName);
            if (group == null)
                throw new InvalidOperationException($"오디오 믹서에서 {groupName} 그룹을 찾을 수 없습니다.");
            return group;
        }

        private static void ConfigureDebugMenuDataAsset()
        {
            MMDebugMenuData data = AssetDatabase.LoadAssetAtPath<MMDebugMenuData>(DebugMenuDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MMDebugMenuData>();
                data.name = "CombatDebugMenuData";
                AssetDatabase.CreateAsset(data, DebugMenuDataPath);
            }

            data.TitlePrefab = LoadRequiredPrefabComponent<MMDebugMenuItemTitle>("MMDebugMenuTitle.prefab");
            data.ButtonPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemButton>("MMDebugMenuButton.prefab");
            data.ButtonBorderPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemButton>("MMDebugMenuButtonBorder.prefab");
            data.CheckboxPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemCheckbox>("MMDebugMenuCheckbox.prefab");
            data.SliderPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemSlider>("MMDebugMenuSlider.prefab");
            data.SpacerSmallPrefab = LoadRequiredAsset<GameObject>(FeelDebugMenuRoot + "/MMDebugMenuSpacerSmall.prefab");
            data.SpacerBigPrefab = LoadRequiredAsset<GameObject>(FeelDebugMenuRoot + "/MMDebugMenuSpacerBig.prefab");
            data.TextTinyPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemText>("MMDebugMenuTextTiny.prefab");
            data.TextSmallPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemText>("MMDebugMenuTextSmall.prefab");
            data.TextLongPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemText>("MMDebugMenuTextLong.prefab");
            data.ValuePrefab = LoadRequiredPrefabComponent<MMDebugMenuItemValue>("MMDebugMenuValue.prefab");
            data.TwoChoicesPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemChoices>("MMDebugMenuChoicesTwo.prefab");
            data.ThreeChoicesPrefab = LoadRequiredPrefabComponent<MMDebugMenuItemChoices>("MMDebugMenuChoicesThree.prefab");
            data.TabPrefab = LoadRequiredPrefabComponent<MMDebugMenuTab>("MMDebugMenuTab.prefab");
            data.TabContentsPrefab = LoadRequiredPrefabComponent<MMDebugMenuTabContents>("MMDebugMenuTabContents.prefab");
            data.TabSpacerPrefab = LoadRequiredAsset<GameObject>(FeelDebugMenuRoot + "/MMDebugMenuTabSpacer.prefab")
                .GetComponent<RectTransform>();
            data.DebugTabPrefab = LoadRequiredPrefabComponent<MMDebugMenuDebugTab>("MMDebugMenuDebugPanel.prefab");
            data.RegularFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            data.BoldFont = data.RegularFont;
            data.BackgroundColor = new Color(0.025f, 0.035f, 0.055f, 0.97f);
            data.AccentColor = new Color(0.2f, 0.9f, 0.58f, 1f);
            data.TextColor = Color.white;
            data.DebugTabName = "로그";
            data.DisplayDebugTab = true;
            data.MaxTabs = 5;
            data.InitialActiveTabIndex = 0;
            data.ToggleDirection = MMDebugMenu.ToggleDirections.RightToLeft;
            data.ToggleDuration = 0.18f;
            data.ToggleKey = Key.Backquote;
            data.Tabs = new List<MMDebugMenuTabData>
            {
                new()
                {
                    Name = "전투",
                    Active = true,
                    MenuItems = CreateCombatDebugMenuItems()
                }
            };
            EditorUtility.SetDirty(data);
        }

        private static MMDebugMenuItemList CreateCombatDebugMenuItems()
        {
            MMDebugMenuItemList items = new();
            items.Add(new MMDebugMenuItem
            {
                Name = "CombatControlTitle",
                Type = MMDebugMenuItem.MMDebugMenuItemTypes.Title,
                TitleText = "전투 제어"
            });
            items.Add(CreateDebugCheckbox(
                "DebugVisible",
                "전체 전술 정보 표시",
                CombatDebugMenuBridge.DebugVisibleEvent,
                false));
            items.Add(CreateDebugCheckbox(
                "FullAuto",
                "아군 완전 자동 전투",
                CombatDebugMenuBridge.FullAutoEvent,
                false));
            items.Add(CreateDebugCheckbox(
                "AutomaticPeek",
                "전체 유닛 자동 피킹",
                CombatDebugMenuBridge.AutomaticPeekEvent,
                true));
            items.Add(CreateDebugCheckbox(
                "Pause",
                "전투 일시정지",
                CombatDebugMenuBridge.PauseEvent,
                false));
            items.Add(new MMDebugMenuItem
            {
                Name = "GameSpeed",
                Type = MMDebugMenuItem.MMDebugMenuItemTypes.Slider,
                SliderMode = MMDebugMenuItemSlider.Modes.Float,
                SliderText = "게임 배속",
                SliderRemapZero = 0.25f,
                SliderRemapOne = 4f,
                SliderInitialValue = 1f,
                SliderEventName = CombatDebugMenuBridge.GameSpeedEvent
            });
            items.Add(new MMDebugMenuItem
            {
                Name = "Restart",
                Type = MMDebugMenuItem.MMDebugMenuItemTypes.Button,
                ButtonText = "현재 전투 다시 시작",
                ButtonType = MMDebugMenuItem.MMDebugMenuItemButtonTypes.Full,
                ButtonEventName = CombatDebugMenuBridge.RestartEvent
            });
            items.Add(new MMDebugMenuItem
            {
                Name = "ShortcutHelp",
                Type = MMDebugMenuItem.MMDebugMenuItemTypes.Text,
                TextType = MMDebugMenuItem.MMDebugMenuItemTextTypes.Small,
                TextContents = "백쿼트(`) 키로 이 메뉴를 열고 닫습니다."
            });
            return items;
        }

        private static MMDebugMenuItem CreateDebugCheckbox(
            string name,
            string label,
            string eventName,
            bool initialState)
        {
            return new MMDebugMenuItem
            {
                Name = name,
                Type = MMDebugMenuItem.MMDebugMenuItemTypes.Checkbox,
                CheckboxText = label,
                CheckboxEventName = eventName,
                CheckboxInitialState = initialState
            };
        }

        private static T LoadRequiredPrefabComponent<T>(string prefabName) where T : Component
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(FeelDebugMenuRoot + "/" + prefabName);
            T component = prefab.GetComponent<T>();
            if (component == null)
                throw new InvalidOperationException($"{prefabName}에서 {typeof(T).Name} 컴포넌트를 찾을 수 없습니다.");
            return component;
        }

        private static void RemoveMissingPresentationScriptsFromCharacterModel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterModelPath);
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, CharacterModelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureWorldUiPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WorldUiPath);
            try
            {
                CharacterWorldUiPresenter presenter = root.GetComponent<CharacterWorldUiPresenter>();
                Transform backgroundTransform = FindRequiredDescendant(root.transform, "HPBackground");
                Image foreground = FindRequiredDescendant(root.transform, "HPFill").GetComponent<Image>();
                Image delayed = EnsureDelayedHealthImage(backgroundTransform, foreground);
                Text outOfAmmoText = EnsureOutOfAmmoText(root.transform);
                Transform worldCanvas = FindRequiredDescendant(root.transform, "WorldCanvas");

                foreground.type = Image.Type.Filled;
                foreground.fillMethod = Image.FillMethod.Horizontal;
                foreground.fillOrigin = (int)Image.OriginHorizontal.Left;

                MMProgressBar progressBar = GetOrAddComponent<MMProgressBar>(backgroundTransform.gameObject);
                progressBar.ForegroundBar = foreground.transform;
                progressBar.DelayedBarDecreasing = delayed.transform;
                progressBar.DelayedBarIncreasing = null;
                progressBar.FillMode = MMProgressBar.FillModes.FillAmount;
                progressBar.TimeScale = MMProgressBar.TimeScales.UnscaledTime;
                progressBar.BarFillMode = MMProgressBar.BarFillModes.FixedDuration;
                progressBar.LerpForegroundBar = true;
                progressBar.LerpForegroundBarDurationDecreasing = 0.05f;
                progressBar.LerpForegroundBarDurationIncreasing = 0.05f;
                progressBar.DecreasingDelay = 0.15f;
                progressBar.LerpDecreasingDelayedBar = true;
                progressBar.LerpDecreasingDelayedBarDuration = 0.18f;
                progressBar.BumpScaleOnChange = true;
                progressBar.BumpOnDecrease = true;
                progressBar.BumpOnIncrease = false;
                progressBar.BumpDuration = 0.12f;
                progressBar.ChangeColorWhenBumping = false;

                MMHealthBar healthBar = GetOrAddComponent<MMHealthBar>(backgroundTransform.gameObject);
                healthBar.HealthBarType = MMHealthBar.HealthBarTypes.Existing;
                healthBar.TimeScale = MMHealthBar.TimeScales.UnscaledTime;
                healthBar.TargetProgressBar = progressBar;
                healthBar.AlwaysVisible = true;
                healthBar.HideBarAtZero = false;
                healthBar.BumpScaleOnChange = false;

                MMBillboard billboard = GetOrAddComponent<MMBillboard>(worldCanvas.gameObject);
                billboard.GrabMainCameraOnStart = true;
                billboard.NestObject = false;
                billboard.OffsetDirection = Vector3.forward;
                billboard.Up = Vector3.up;

                SetObjectReference(presenter, "healthBar", healthBar);
                SetObjectReference(presenter, "outOfAmmoText", outOfAmmoText);
                PrefabUtility.SaveAsPrefabAsset(root, WorldUiPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Text EnsureOutOfAmmoText(Transform root)
        {
            Transform canvas = FindRequiredDescendant(root, "WorldCanvas");
            Transform existing = FindDescendant(canvas, "OutOfAmmoText");
            GameObject textObject;
            if (existing == null)
            {
                textObject = new GameObject(
                    "OutOfAmmoText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text),
                    typeof(Outline));
                textObject.transform.SetParent(canvas, false);
            }
            else
            {
                textObject = existing.gameObject;
                if (textObject.GetComponent<Outline>() == null)
                    textObject.AddComponent<Outline>();
            }

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 39f);
            rect.sizeDelta = new Vector2(210f, 22f);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.25f, 0.12f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "OUT OF AMMO";

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.01f, 0.01f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            textObject.SetActive(false);
            return text;
        }

        private static Image EnsureDelayedHealthImage(Transform background, Image foreground)
        {
            Transform existing = background.Find("HPDelayedFill");
            Image delayed;
            if (existing == null)
            {
                GameObject delayedObject = new(
                    "HPDelayedFill",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                delayedObject.transform.SetParent(background, false);
                delayed = delayedObject.GetComponent<Image>();
            }
            else
            {
                delayed = existing.GetComponent<Image>();
            }

            RectTransform delayedRect = delayed.rectTransform;
            RectTransform foregroundRect = foreground.rectTransform;
            delayedRect.anchorMin = foregroundRect.anchorMin;
            delayedRect.anchorMax = foregroundRect.anchorMax;
            delayedRect.pivot = foregroundRect.pivot;
            delayedRect.anchoredPosition = foregroundRect.anchoredPosition;
            delayedRect.sizeDelta = foregroundRect.sizeDelta;
            delayed.type = Image.Type.Filled;
            delayed.fillMethod = Image.FillMethod.Horizontal;
            delayed.fillOrigin = (int)Image.OriginHorizontal.Left;
            delayed.fillAmount = 1f;
            delayed.color = new Color(1f, 0.55f, 0.08f, 1f);
            delayed.transform.SetSiblingIndex(foreground.transform.GetSiblingIndex());
            foreground.transform.SetAsLastSibling();
            return delayed;
        }

        private static void ConfigureUnitBasePrefab()
        {
            MMChannel damageChannel = LoadRequiredAsset<MMChannel>(DamageChannelPath);
            MMChannel cameraChannel = LoadRequiredAsset<MMChannel>(CameraChannelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(UnitBasePath);
            try
            {
                Combatant combatant = root.GetComponent<Combatant>();
                CombatantFeedbackPresenter presenter = GetOrAddComponent<CombatantFeedbackPresenter>(root);
                Transform feedbackRoot = EnsureChild(root.transform, "FeelFeedbacks");
                LineRenderer tracer = FindRequiredDescendant(root.transform, "ShotTracer").GetComponent<LineRenderer>();
                tracer.enabled = true;
                tracer.gameObject.SetActive(true);
                ParticleSystem muzzleFlash = FindParticleSystem(root, "MuzzleFlash", "Muzzle");
                ParticleSystem hitEffect = FindParticleSystem(root, "HitEffect");
                ParticleSystem deathEffect = EnsureDeathParticle(feedbackRoot, hitEffect);
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != tracer
                        && renderer.GetComponent<ParticleSystemRenderer>() == null
                        && renderer.sharedMaterials.Length > 0
                        && renderer.sharedMaterials[0] != null
                        && renderer.sharedMaterials[0].HasProperty("_EmissionColor"))
                    .ToArray();

                MMF_Player shot = ConfigurePlayer(feedbackRoot, "ShotFeedbacks");
                AddShotFeedbacks(shot, muzzleFlash, tracer);
                MMF_Player hit = ConfigurePlayer(feedbackRoot, "HitFeedbacks");
                AddHitFeedbacks(hit, renderers, hitEffect);
                MMF_Player damageText = ConfigurePlayer(feedbackRoot, "DamageTextFeedbacks", true);
                AddFloatingTextFeedback(damageText, damageChannel, true);
                MMF_Player missText = ConfigurePlayer(feedbackRoot, "MissTextFeedbacks", true);
                AddFloatingTextFeedback(missText, damageChannel, false);
                MMF_Player deathVisual = ConfigurePlayer(feedbackRoot, "DeathVisualFeedbacks", true);
                AddDeathVisualFeedbacks(deathVisual, deathEffect);
                MMF_Player deathShake = ConfigurePlayer(feedbackRoot, "DeathShakeFeedbacks", true);
                AddCameraShakeFeedback(deathShake, cameraChannel, 0.22f, 1.15f, 22f, "유닛 사망 셰이크");
                MMF_Player defeatShake = ConfigurePlayer(feedbackRoot, "DefeatShakeFeedbacks", true);
                AddCameraShakeFeedback(defeatShake, cameraChannel, 0.32f, 2.1f, 28f, "게임 패배 셰이크");

                presenter.SetEditorReferences(
                    shot,
                    tracer,
                    hit,
                    damageText,
                    missText,
                    deathVisual,
                    deathShake,
                    defeatShake);
                SetObjectReference(combatant, "feedbackPresenter", presenter);
                PrefabUtility.SaveAsPrefabAsset(root, UnitBasePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddShotFeedbacks(
            MMF_Player player,
            ParticleSystem muzzleFlash,
            LineRenderer tracer)
        {
            if (muzzleFlash != null)
            {
                player.AddFeedback(new MMF_Particles
                {
                    Label = "총구 파티클",
                    BoundParticleSystem = muzzleFlash,
                    Mode = MMF_Particles.Modes.Play,
                    StopSystemOnInit = true
                });
            }

            player.AddFeedback(new MMF_SetActive
            {
                Label = "트레이서 표시",
                TargetGameObject = tracer.gameObject,
                SetStateOnInit = true,
                StateOnInit = MMF_SetActive.PossibleStates.Inactive,
                SetStateOnPlay = true,
                StateOnPlay = MMF_SetActive.PossibleStates.Active
            });

            Gradient tracerGradient = CreateGradient(
                new Color(1f, 0.82f, 0.28f, 1f),
                new Color(1f, 0.18f, 0.04f, 0f));
            player.AddFeedback(new MMF_LineRenderer
            {
                Label = "트레이서 폭·색상 페이드",
                TargetLineRenderer = tracer,
                Mode = MMF_LineRenderer.Modes.OverTime,
                Duration = 0.09f,
                ModifyWidth = true,
                NewWidth = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)),
                ModifyColor = true,
                NewColor = tracerGradient,
                Timing = UnscaledTiming()
            });
            player.AddFeedback(new MMF_SetActive
            {
                Label = "트레이서 숨김",
                TargetGameObject = tracer.gameObject,
                SetStateOnPlay = true,
                StateOnPlay = MMF_SetActive.PossibleStates.Inactive,
                Timing = new MMFeedbackTiming
                {
                    TimescaleMode = TimescaleModes.Unscaled,
                    InitialDelay = 0.09f
                }
            });
        }

        private static void AddHitFeedbacks(
            MMF_Player player,
            Renderer[] renderers,
            ParticleSystem hitEffect)
        {
            if (renderers.Length > 0)
            {
                player.AddFeedback(new MMF_Flicker
                {
                    Label = "URP 발광 피격 플리커",
                    BoundRenderer = renderers[0],
                    ExtraBoundRenderers = renderers.Skip(1).ToList(),
                    Mode = MMF_Flicker.Modes.PropertyName,
                    PropertyName = "_EmissionColor",
                    FlickerDuration = 0.12f,
                    FlickerPeriod = 0.035f,
                    FlickerColor = new Color(3.5f, 0.55f, 0.08f, 1f),
                    MaterialIndexes = new[] { 0 },
                    UseMaterialPropertyBlocks = true
                });
            }
            if (hitEffect != null)
            {
                player.AddFeedback(new MMF_Particles
                {
                    Label = "피격 파티클",
                    BoundParticleSystem = hitEffect,
                    Mode = MMF_Particles.Modes.Play,
                    MoveToPosition = true,
                    StopSystemOnInit = true
                });
            }
        }

        private static void AddFloatingTextFeedback(
            MMF_Player player,
            MMChannel damageChannel,
            bool damage)
        {
            MMF_FloatingText feedback = new()
            {
                Label = damage ? "실제 감소 데미지" : "빗나감 MISS",
                ChannelMode = MMChannelModes.MMChannel,
                MMChannelDefinition = damageChannel,
                UseIntensityAsValue = damage,
                Value = damage ? "0" : "MISS",
                RoundingMethod = MMF_FloatingText.RoundingMethods.Round,
                ForceColor = true,
                AnimateColorGradient = damage
                    ? CreateGradient(new Color(1f, 0.55f, 0.08f), new Color(0.95f, 0.05f, 0.02f))
                    : CreateGradient(new Color(0.62f, 0.65f, 0.68f), new Color(0.42f, 0.45f, 0.48f)),
                ForceLifetime = true,
                Lifetime = 0.65f,
                PositionMode = MMF_FloatingText.PositionModes.PlayPosition,
                Direction = Vector3.up,
                Timing = UnscaledTiming(true)
            };
            player.AddFeedback(feedback);
        }

        private static void AddDeathVisualFeedbacks(MMF_Player player, ParticleSystem deathEffect)
        {
            if (deathEffect != null)
            {
                player.AddFeedback(new MMF_Particles
                {
                    Label = "강한 사망 파티클",
                    BoundParticleSystem = deathEffect,
                    Mode = MMF_Particles.Modes.Play,
                    MoveToPosition = true,
                    StopSystemOnInit = true
                });
            }
            player.AddFeedback(new MMF_FreezeFrame
            {
                Label = "사망 히트스톱",
                FreezeFrameDuration = 0.04f
            });
        }

        private static void AddCameraShakeFeedback(
            MMF_Player player,
            MMChannel cameraChannel,
            float duration,
            float amplitude,
            float frequency,
            string label)
        {
            player.AddFeedback(new MMF_CameraShake
            {
                Label = label,
                ChannelMode = MMChannelModes.MMChannel,
                MMChannelDefinition = cameraChannel,
                CameraShakeProperties = new MMCameraShakeProperties(duration, amplitude, frequency),
                Timing = UnscaledTiming()
            });
        }

        private static ParticleSystem EnsureDeathParticle(
            Transform feedbackRoot,
            ParticleSystem hitEffect)
        {
            Transform existing = feedbackRoot.Find("DeathEffect");
            GameObject deathObject;
            if (existing != null)
            {
                deathObject = existing.gameObject;
            }
            else if (hitEffect != null)
            {
                deathObject = Object.Instantiate(hitEffect.gameObject, feedbackRoot);
                deathObject.name = "DeathEffect";
            }
            else
            {
                deathObject = new GameObject("DeathEffect", typeof(ParticleSystem));
                deathObject.transform.SetParent(feedbackRoot, false);
            }

            ParticleSystem particle = deathObject.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 4.5f;
            main.startSize = 0.28f;
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particle;
        }

        private static void ConfigureFeedbackRig(MMChannel damageChannel)
        {
            GameObject systems = GameObject.Find("CombatSystems");
            if (systems == null)
                throw new InvalidOperationException("현재 씬에 CombatSystems가 없습니다.");

            MMTimeManager timeManager = systems.GetComponent<MMTimeManager>();
            if (timeManager == null)
                timeManager = systems.AddComponent<MMTimeManager>();
            timeManager.NormalTimeScale = 1f;

            ConfigureSoundManager();

            Transform rig = systems.transform.Find("CombatFeedbackRig");
            if (rig == null)
                rig = new GameObject("CombatFeedbackRig").transform;
            rig.SetParent(systems.transform, false);

            MMFloatingTextSpawner spawner = GetOrAddComponent<MMFloatingTextSpawner>(rig.gameObject);
            spawner.ChannelMode = MMChannelModes.MMChannel;
            spawner.MMChannelDefinition = damageChannel;
            spawner.UseUnscaledTime = true;
            spawner.PoolerMode = MMFloatingTextSpawner.PoolerModes.Simple;
            GameObject floatingTextPrefab = LoadRequiredAsset<GameObject>(FeedbackPrefabPath);
            spawner.PooledSimpleMMFloatingText = floatingTextPrefab.GetComponent<MMFloatingText>();
            spawner.PoolSize = 32;
            spawner.PoolCanExpand = true;
            spawner.Lifetime = new Vector2(0.65f, 0.65f);
            spawner.AnimateMovement = true;
            spawner.AnimateX = false;
            spawner.AnimateY = true;
            spawner.RemapYZero = Vector2.zero;
            spawner.RemapYOne = new Vector2(1.4f, 1.8f);
            spawner.AnimateYCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            spawner.AlwaysFaceCamera = true;
            spawner.AutoGrabMainCameraOnStart = true;
            spawner.AnimateScale = true;
            spawner.AnimateScaleCurve = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(0.15f, 1.15f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0.9f));
            spawner.AnimateOpacity = true;
            spawner.AnimateOpacityCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.72f, 1f),
                new Keyframe(1f, 0f));
            spawner.IntensityImpactsLifetime = false;
            spawner.IntensityImpactsMovement = false;
            spawner.IntensityImpactsScale = false;
        }

        private static void ConfigureCinemachine(MMChannel cameraChannel)
        {
            CinemachineCamera camera = Object.FindFirstObjectByType<CinemachineCamera>();
            if (camera == null)
                throw new InvalidOperationException("현재 씬에 CinemachineCamera가 없습니다.");

            CinemachineBasicMultiChannelPerlin noise =
                GetOrAddComponent<CinemachineBasicMultiChannelPerlin>(camera.gameObject);
            noise.NoiseProfile = LoadRequiredAsset<NoiseSettings>(CameraNoisePath);
            noise.AmplitudeGain = 0f;
            noise.FrequencyGain = 1f;

            MMCinemachineCameraShaker shaker =
                GetOrAddComponent<MMCinemachineCameraShaker>(camera.gameObject);
            shaker.ChannelMode = MMChannelModes.MMChannel;
            shaker.MMChannelDefinition = cameraChannel;
            shaker.DefaultShakeAmplitude = 1.15f;
            shaker.DefaultShakeFrequency = 22f;
            shaker.LerpSpeed = 12f;
        }

        private static void ConfigureHudFeedbacks()
        {
            CombatHudController hud = Object.FindFirstObjectByType<CombatHudController>();
            if (hud == null)
                throw new InvalidOperationException("현재 씬에 CombatHudController가 없습니다.");

            Transform selectedPanel = FindRequiredDescendant(hud.transform, "SelectedInfoPanel");
            Transform autoButton = FindRequiredDescendant(hud.transform, "AllyFullAutoButton");
            Transform resultPanel = FindRequiredDescendant(hud.transform, "ResultPanel");
            CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(resultPanel.gameObject);

            Transform feedbackRoot = EnsureChild(hud.transform, "FeelFeedbacks");
            MMF_Player selection = ConfigurePlayer(feedbackRoot, "SelectionChangedFeedbacks", true);
            selection.AddFeedback(CreateScaleBump(selectedPanel, 0.12f, 1.035f, "선택 변경 스케일"));
            MMF_Player automatic = ConfigurePlayer(feedbackRoot, "AutomaticModeChangedFeedbacks", true);
            automatic.AddFeedback(CreateScaleBump(autoButton, 0.12f, 1.06f, "자동전투 변경 스케일"));
            MMF_Player result = ConfigurePlayer(feedbackRoot, "ResultPanelFeedbacks", true);
            result.AddFeedback(new MMF_CanvasGroup
            {
                Label = "승패 패널 페이드",
                TargetCanvasGroup = canvasGroup,
                Mode = MMF_FeedbackBase.Modes.OverTime,
                Duration = 0.24f,
                RemapZero = 0f,
                RemapOne = 1f,
                AlphaCurve = new MMTweenType(AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)),
                Timing = UnscaledTiming()
            });
            result.AddFeedback(new MMF_Scale
            {
                Label = "승패 패널 스케일 진입",
                AnimateScaleTarget = resultPanel,
                Mode = MMF_Scale.Modes.ToDestination,
                MovementMode = MMF_Scale.MovementModes.Duration,
                AnimateScaleDuration = 0.24f,
                DestinationScale = Vector3.one,
                DetermineScaleOnPlay = true,
                Timing = UnscaledTiming()
            });

            SetObjectReference(hud, "selectionChangedFeedbacks", selection);
            SetObjectReference(hud, "automaticModeChangedFeedbacks", automatic);
            SetObjectReference(hud, "resultPanelFeedbacks", result);
        }

        private static MMF_Scale CreateScaleBump(
            Transform target,
            float duration,
            float maximumScale,
            string label)
        {
            AnimationCurve curve = new(
                new Keyframe(0f, 0f),
                new Keyframe(0.42f, 1f),
                new Keyframe(1f, 0f));
            return new MMF_Scale
            {
                Label = label,
                AnimateScaleTarget = target,
                Mode = MMF_Scale.Modes.Absolute,
                MovementMode = MMF_Scale.MovementModes.Duration,
                AnimateScaleDuration = duration,
                RemapCurveZero = 1f,
                RemapCurveOne = maximumScale,
                AnimateScaleTweenX = new MMTweenType(curve),
                AnimateScaleTweenY = new MMTweenType(curve),
                AnimateScaleTweenZ = new MMTweenType(curve),
                UniformScaling = true,
                AllowAdditivePlays = true,
                Timing = UnscaledTiming()
            };
        }

        private static void ConfigureSceneRuntimeReferences()
        {
            MMTimeManager timeManager = Object.FindFirstObjectByType<MMTimeManager>();
            CombatDirector director = Object.FindFirstObjectByType<CombatDirector>();
            TacticalInputController input = Object.FindFirstObjectByType<TacticalInputController>();
            if (director == null || input == null || timeManager == null)
                throw new InvalidOperationException("전투 시간 관리자 연결 대상이 누락되었습니다.");

            SetObjectReference(director, "timeManager", timeManager);
            SetObjectReference(input, "timeManager", timeManager);
            foreach (Combatant combatant in Object.FindObjectsByType<Combatant>(FindObjectsSortMode.None))
            {
                CombatantFeedbackPresenter presenter =
                    combatant.GetComponentInChildren<CombatantFeedbackPresenter>(true);
                if (presenter == null)
                    throw new InvalidOperationException($"{combatant.name}에 Feel Presenter가 상속되지 않았습니다.");
                SetObjectReference(combatant, "feedbackPresenter", presenter);
            }
        }

        private static void ConfigureSoundManager()
        {
            GameObject soundRoot = GameObject.Find("MMSoundManager");
            if (soundRoot == null)
                soundRoot = new GameObject("MMSoundManager");
            soundRoot.transform.SetParent(null);

            GetOrAddComponent<FeelConvenienceRuntimeBootstrap>(soundRoot);
            MMSoundManager soundManager = GetOrAddComponent<MMSoundManager>(soundRoot);
            soundManager.settingsSo = LoadRequiredAsset<MMSoundManagerSettingsSO>(SoundSettingsPath);
            soundManager.AudioSourcePoolSize = 24;
            soundManager.PoolCanExpand = true;
            EditorUtility.SetDirty(soundRoot);
        }

        private static void ConfigureDebugTools()
        {
            CombatDirector director = Object.FindFirstObjectByType<CombatDirector>();
            TacticalInputController input = Object.FindFirstObjectByType<TacticalInputController>();
            CombatHudController hud = Object.FindFirstObjectByType<CombatHudController>();
            if (director == null || input == null || hud == null)
                throw new InvalidOperationException("디버그 메뉴 연결 대상이 누락되었습니다.");

            MMDebugMenu debugMenu = Object.FindFirstObjectByType<MMDebugMenu>();
            if (debugMenu == null)
            {
                GameObject prefab = LoadRequiredAsset<GameObject>(FeelDebugMenuPrefabPath);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "CombatDebugMenu";
                instance.transform.localScale = Vector3.one;
                debugMenu = instance.GetComponent<MMDebugMenu>();
            }

            debugMenu.Data = LoadRequiredAsset<MMDebugMenuData>(DebugMenuDataPath);
            CombatDebugMenuBridge bridge = GetOrAddComponent<CombatDebugMenuBridge>(debugMenu.gameObject);
            bridge.SetEditorReferences(director, input);
            ConfigureFpsCounter(hud.transform);
            EditorUtility.SetDirty(debugMenu);
            EditorUtility.SetDirty(bridge);
        }

        private static void ConfigureFpsCounter(Transform hudRoot)
        {
            Transform existing = FindDescendant(hudRoot, "FPSCounter");
            GameObject counterObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    "FPSCounter",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text),
                    typeof(Outline));
            counterObject.transform.SetParent(hudRoot, false);
            counterObject.layer = 5;

            RectTransform rect = counterObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-18f, -18f);
            rect.sizeDelta = new Vector2(180f, 32f);

            Text text = counterObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.color = new Color(0.65f, 1f, 0.78f, 1f);
            text.raycastTarget = false;
            text.text = "FPS";

            Outline outline = counterObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            MMFPSCounter counter = GetOrAddComponent<MMFPSCounter>(counterObject);
            counter.UpdateInterval = 0.35f;
            counter.Mode = MMFPSCounter.Modes.InstantAndMovingAverage;
        }

        private static MMF_Player ConfigurePlayer(
            Transform parent,
            string name,
            bool forceUnscaled = false)
        {
            Transform child = EnsureChild(parent, name);
            MMF_Player player = GetOrAddComponent<MMF_Player>(child.gameObject);
            player.FeedbacksList = new List<MMF_Feedback>();
            player.ForceTimescaleMode = forceUnscaled;
            player.ForcedTimescaleMode = TimescaleModes.Unscaled;
            return player;
        }

        private static MMFeedbackTiming UnscaledTiming(bool constantIntensity = false)
        {
            return new MMFeedbackTiming
            {
                TimescaleMode = TimescaleModes.Unscaled,
                ConstantIntensity = constantIntensity
            };
        }

        private static void ValidateCameraShakePlacement(GameObject unitBase)
        {
            foreach (MMF_Player player in unitBase.GetComponentsInChildren<MMF_Player>(true))
            {
                int shakeCount = player.FeedbacksList?.Count(feedback => feedback is MMF_CameraShake) ?? 0;
                bool shakeAllowed = player.name == "DeathShakeFeedbacks"
                    || player.name == "DefeatShakeFeedbacks";
                if (shakeCount > 0 && !shakeAllowed)
                    throw new InvalidOperationException($"허용되지 않은 셰이크가 {player.name}에 있습니다.");
                if (shakeAllowed && shakeCount != 1)
                    throw new InvalidOperationException($"{player.name}의 셰이크 개수가 1개가 아닙니다.");
            }
        }

        private static void ValidateVariantSource(string variantPath)
        {
            GameObject variant = LoadRequiredAsset<GameObject>(variantPath);
            if (PrefabUtility.GetPrefabAssetType(variant) != PrefabAssetType.Variant)
                throw new InvalidOperationException($"{variantPath}가 Prefab Variant가 아닙니다.");
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
            if (source == null || AssetDatabase.GetAssetPath(source) != UnitBasePath)
                throw new InvalidOperationException($"{variantPath}가 UnitBase를 상속하지 않습니다.");
        }

        private static ParticleSystem FindParticleSystem(
            GameObject root,
            params string[] preferredNames)
        {
            foreach (string preferredName in preferredNames)
            {
                ParticleSystem named = root.GetComponentsInChildren<ParticleSystem>(true)
                    .FirstOrDefault(system => system.name == preferredName);
                if (named != null)
                    return named;
            }
            return null;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;
            GameObject childObject = new(name);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static Transform FindRequiredDescendant(Transform root, string name)
        {
            Transform found = FindDescendant(root, name);
            if (found == null)
                throw new InvalidOperationException($"{root.name} 아래에서 {name}을 찾을 수 없습니다.");
            return found;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindDescendant(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return gradient;
        }

        private static void SetObjectReference(
            Object target,
            string propertyName,
            Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{target.name}의 {propertyName} 필드를 찾을 수 없습니다.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void CopyAssetIfMissing(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
                return;
            if (!AssetDatabase.CopyAsset(source, destination))
                throw new InvalidOperationException($"Feel 자산 복사에 실패했습니다: {source}");
        }

        private static void CreateChannelIfMissing(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<MMChannel>(path) != null)
                return;
            MMChannel channel = ScriptableObject.CreateInstance<MMChannel>();
            channel.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(channel, path);
        }

        private static T LoadRequiredAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"필수 자산을 찾을 수 없습니다: {path}");
            return asset;
        }
    }
}
