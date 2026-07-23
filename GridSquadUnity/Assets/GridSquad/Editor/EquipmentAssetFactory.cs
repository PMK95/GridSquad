using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GridSquad.Editor
{
    public static class EquipmentAssetFactory
    {
        public const string EquipmentRoot = "Assets/GridSquad/Equipment";
        private const string EquipmentCatalogsRoot = EquipmentRoot + "/Catalogs";
        private const string EquipmentLayoutsRoot = EquipmentRoot + "/Layouts";
        private const string EquipmentSlotsRoot = EquipmentRoot + "/Slots";
        private const string EquipmentArmorRoot = EquipmentRoot + "/Armor";
        private const string EquipmentConsumablesRoot = EquipmentRoot + "/Consumables";
        private const string EquipmentSupportRoot = EquipmentRoot + "/Support";
        private const string EquipmentWeaponsRoot = EquipmentRoot + "/Weapons";
        private const string WeaponDefinitionsRoot = EquipmentWeaponsRoot + "/Definitions";
        private const string WeaponAttackBehaviorsRoot = EquipmentWeaponsRoot + "/AttackBehaviors";
        private const string WeaponHitEffectsRoot = EquipmentWeaponsRoot + "/HitEffects";
        private const string ActionDataRoot = "Assets/GridSquad/Data/Actions";
        private const string StatDataRoot = "Assets/GridSquad/Data/Stats";
        public const string EquipmentCatalogPath = EquipmentCatalogsRoot + "/EquipmentCatalog.asset";
        public const string ItemCatalogPath = EquipmentCatalogsRoot + "/ItemCatalog.asset";
        public const string EquipmentLayoutPath = EquipmentLayoutsRoot + "/CombatEquipmentLayout.asset";
        public const string EquipmentSlotPrefabPath = "Assets/GridSquad/Prefabs/UI/EquipmentSlotView.prefab";
        public const string EquipmentItemCardPrefabPath = "Assets/GridSquad/Prefabs/UI/EquipmentItemCard.prefab";
        public const string SelectionUiRootPrefabPath = "Assets/GridSquad/Resources/UI/SelectionUiRoot.prefab";
        public const string CombatActionButtonPrefabPath = "Assets/GridSquad/Resources/UI/CombatActionButton.prefab";
        public const string ContextCommandButtonPrefabPath = "Assets/GridSquad/Resources/UI/ContextCommandButton.prefab";

        private const string RiflePath = WeaponDefinitionsRoot + "/WeaponDefinition.asset";
        private const string SmgPath = WeaponDefinitionsRoot + "/SmgWeaponDefinition.asset";
        private const string ShotgunPath = WeaponDefinitionsRoot + "/Shotgun.asset";
        private const string BatonPath = WeaponDefinitionsRoot + "/RiotBaton.asset";
        private const string BallisticPlatePath = EquipmentSupportRoot + "/BallisticPlate.asset";
        private const string RecoveryArmorPath = EquipmentArmorRoot + "/RecoveryArmor.asset";
        private const string BandagePath = EquipmentConsumablesRoot + "/Bandage.asset";
        private const string RecoveryActionPath = ActionDataRoot + "/RecoveryAction.asset";
        private const string RecoveryBehaviorPath = ActionDataRoot + "/RecoveryBehavior.asset";
        private const string BandageActionPath = ActionDataRoot + "/BandageAction.asset";
        private const string BandageBehaviorPath = ActionDataRoot + "/BandageBehavior.asset";
        private const string GrenadeEquipmentPath = EquipmentSupportRoot + "/GrenadeEquipment.asset";
        private const string StimEquipmentPath = EquipmentSupportRoot + "/StimEquipment.asset";
        private const string WeaponCatalogPath = EquipmentCatalogsRoot + "/WeaponCatalog.asset";

        [MenuItem("GridSquad/장비 시스템 에셋 생성 및 마이그레이션")]
        public static void EnsureProjectEquipmentAssets()
        {
            EnsureFolder("Assets/GridSquad", "Equipment");
            EnsureFolder(EquipmentRoot, "Catalogs");
            EnsureFolder(EquipmentRoot, "Layouts");
            EnsureFolder(EquipmentRoot, "Slots");
            EnsureFolder(EquipmentRoot, "Armor");
            EnsureFolder(EquipmentRoot, "Consumables");
            EnsureFolder(EquipmentRoot, "Support");
            EnsureFolder(EquipmentRoot, "Weapons");
            EnsureFolder(EquipmentWeaponsRoot, "Definitions");
            EnsureFolder(EquipmentWeaponsRoot, "AttackBehaviors");
            EnsureFolder(EquipmentWeaponsRoot, "HitEffects");
            EnsureFolder("Assets/GridSquad/Data", "Actions");
            EnsureFolder("Assets/GridSquad/Data", "Stats");
            EnsureFolder("Assets/GridSquad/Prefabs", "UI");
            EnsureFolder("Assets/GridSquad", "Resources");
            EnsureFolder("Assets/GridSquad/Resources", "UI");
            EnsureFolder("Assets/GridSquad/Materials", "Weapons");
            EnsureFolder("Assets/GridSquad/Art/UI", "Equipment");
            ConfigureEquipmentIconImporters();

            EquipmentSlotDefinition leftHand = EnsureSlot(
                "LeftHandSlot", "left_hand", "왼손", EquipmentCategory.Weapon, EquipmentSlotKind.LeftHand, new Vector2(-0.32f, 0.08f));
            EquipmentSlotDefinition rightHand = EnsureSlot(
                "RightHandSlot", "right_hand", "오른손", EquipmentCategory.OffHand, EquipmentSlotKind.RightHand, new Vector2(0.32f, 0.08f));
            EquipmentSlotDefinition head = EnsureSlot(
                "HeadSlot", "head", "머리", EquipmentCategory.Armor, EquipmentSlotKind.Head, new Vector2(0f, 0.42f));
            EquipmentSlotDefinition torso = EnsureSlot(
                "TorsoSlot", "torso", "상의", EquipmentCategory.Armor, EquipmentSlotKind.Torso, new Vector2(0f, 0.18f));
            EquipmentSlotDefinition legs = EnsureSlot(
                "LegsSlot", "legs", "하의", EquipmentCategory.Armor, EquipmentSlotKind.Legs, new Vector2(0f, -0.05f));
            EquipmentSlotDefinition hands = EnsureSlot(
                "HandsSlot", "hands", "장갑", EquipmentCategory.Armor, EquipmentSlotKind.Hands, new Vector2(-0.22f, -0.12f));
            EquipmentSlotDefinition feet = EnsureSlot(
                "FeetSlot", "feet", "신발", EquipmentCategory.Armor, EquipmentSlotKind.Feet, new Vector2(0f, -0.34f));
            EquipmentSlotDefinition supportOne = EnsureSlot(
                "SupportSlot1", "support_1", "보조장비 1", EquipmentCategory.Support, EquipmentSlotKind.SupportOne, new Vector2(-0.24f, -0.42f));
            EquipmentSlotDefinition supportTwo = EnsureSlot(
                "SupportSlot2", "support_2", "보조장비 2", EquipmentCategory.Support, EquipmentSlotKind.SupportTwo, new Vector2(0.24f, -0.42f));
            EquipmentSlotDefinition[] slots =
            {
                leftHand, rightHand, head, torso, legs, hands, feet, supportOne, supportTwo
            };

            EquipmentLayoutDefinition layout = EnsureAsset<EquipmentLayoutDefinition>(EquipmentLayoutPath);
            layout.SetEditorSlots(slots);
            EditorUtility.SetDirty(layout);

            HitscanWeaponAttackBehaviorDefinition hitscan = EnsureAsset<HitscanWeaponAttackBehaviorDefinition>(
                WeaponAttackBehaviorsRoot + "/HitscanAttack.asset");
            ShotgunWeaponAttackBehaviorDefinition shotgunAttack = EnsureAsset<ShotgunWeaponAttackBehaviorDefinition>(
                WeaponAttackBehaviorsRoot + "/ShotgunAttack.asset");
            MeleeWeaponAttackBehaviorDefinition meleeAttack = EnsureAsset<MeleeWeaponAttackBehaviorDefinition>(
                WeaponAttackBehaviorsRoot + "/MeleeAttack.asset");
            StunWeaponHitEffectDefinition stun = EnsureAsset<StunWeaponHitEffectDefinition>(
                WeaponHitEffectsRoot + "/RiotBatonStun.asset");
            KnockbackWeaponHitEffectDefinition knockback = EnsureAsset<KnockbackWeaponHitEffectDefinition>(
                WeaponHitEffectsRoot + "/ShotgunKnockback.asset");

            WeaponDefinition rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RiflePath);
            WeaponDefinition smg = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SmgPath);
            ConfigureExistingWeapon(rifle, "rifle", "소총", "균형 잡힌 제식 소총", "Rifle.png", hitscan, 4f, WeaponHandedness.TwoHanded);
            ConfigureExistingWeapon(smg, "smg", "기관단총", "근거리 연속 사격에 특화된 기관단총", "Smg.png", hitscan, 3f, WeaponHandedness.TwoHanded);

            Material shotgunMaterial = EnsureMaterial("Shotgun", new Color(0.16f, 0.19f, 0.21f));
            Material batonMaterial = EnsureMaterial("RiotBaton", new Color(0.035f, 0.045f, 0.055f));
            WeaponPresentation shotgunPresentation = EnsureWeaponPresentation(
                "Assets/GridSquad/Prefabs/Weapons/ShotgunGunRoot.prefab", shotgunMaterial, false);
            WeaponPresentation batonPresentation = EnsureWeaponPresentation(
                "Assets/GridSquad/Prefabs/Weapons/RiotBatonRoot.prefab", batonMaterial, true);
            WeaponDefinition shotgun = EnsureAsset<WeaponDefinition>(ShotgunPath);
            ConfigureWeapon(
                shotgun, "shotgun", "샷건", "5발 산탄과 1칸 밀치기를 제공하는 근거리 화기",
                "Shotgun.png", shotgunPresentation, shotgunAttack, new WeaponHitEffectDefinition[] { knockback },
                45, 0.45f, 1.05f, 6, 24, 1.8f, 4f, 72f, 5f, WeaponHandedness.TwoHanded);
            WeaponDefinition baton = EnsureAsset<WeaponDefinition>(BatonPath);
            ConfigureWeapon(
                baton, "riot_baton", "진압봉", "20 피해와 50% 확률의 1.5초 기절을 가하는 근접 무기",
                "RiotBaton.png", batonPresentation, meleeAttack, new WeaponHitEffectDefinition[] { stun },
                20, 0.25f, 0.85f, 1, 0, 0.1f, 1f, 100f, 1.5f, WeaponHandedness.OneHanded);

            CombatActionDefinition basicAttack = AssetDatabase.LoadAssetAtPath<CombatActionDefinition>(
                ActionDataRoot + "/BasicAttackAction.asset");
            CombatActionDefinition grenadeAction = AssetDatabase.LoadAssetAtPath<CombatActionDefinition>(
                ActionDataRoot + "/GrenadeAction.asset");
            CombatActionDefinition stimAction = AssetDatabase.LoadAssetAtPath<CombatActionDefinition>(
                ActionDataRoot + "/StimAction.asset");
            CombatActionDefinition dashAction = AssetDatabase.LoadAssetAtPath<CombatActionDefinition>(
                ActionDataRoot + "/DashAction.asset");

            ConfigureWeaponActionGrants(rifle, basicAttack, grenadeAction);
            ConfigureWeaponActionGrants(smg, basicAttack);
            ConfigureWeaponActionGrants(shotgun, basicAttack);
            ConfigureWeaponActionGrants(baton, basicAttack);

            AdditionalEquipmentDefinition ballisticPlate = EnsureAsset<AdditionalEquipmentDefinition>(BallisticPlatePath);
            ballisticPlate.SetEditorEquipmentPresentation(
                "ballistic_plate", "방탄판", "피해를 최대 세 번 차단하고 10초마다 한 번 재충전합니다.",
                LoadEquipmentIcon("BallisticPlate.png"), 3f);
            ballisticPlate.SetEditorGrantedActions(Array.Empty<CombatActionDefinition>());
            ballisticPlate.SetEditorPassiveConfiguration(
                SupportEquipmentPassiveKind.RegeneratingBallisticPlate, 3, 10f);
            EditorUtility.SetDirty(ballisticPlate);

            RestoreHealthActionBehaviorDefinition recoveryBehavior =
                EnsureAsset<RestoreHealthActionBehaviorDefinition>(RecoveryBehaviorPath);
            recoveryBehavior.SetEditorRestoreAmount(25);
            CombatActionDefinition recoveryAction = EnsureAsset<CombatActionDefinition>(RecoveryActionPath);
            recoveryAction.SetEditorConfiguration(
                "armor_rapid_recovery", "급속 회복", recoveryBehavior, false, false, -1, 20f, 1f);
            recoveryAction.SetEditorPresentation("1초 준비 후 HP를 25 회복합니다.", null);
            SetPlayerActionOrder(recoveryAction, 210);

            RestoreHealthActionBehaviorDefinition bandageBehavior =
                EnsureAsset<RestoreHealthActionBehaviorDefinition>(BandageBehaviorPath);
            bandageBehavior.SetEditorRestoreAmount(20);
            CombatActionDefinition bandageAction = EnsureAsset<CombatActionDefinition>(BandageActionPath);
            bandageAction.SetEditorConfiguration(
                "bandage_quick_use", "붕대 사용", bandageBehavior, false, false, -1, 0f, 0.75f);
            bandageAction.SetEditorPresentation("0.75초 준비 후 HP를 20 회복하고 붕대 한 개를 소모합니다.", null);
            SetPlayerActionOrder(bandageAction, 220);

            ArmorDefinition recoveryArmor = EnsureAsset<ArmorDefinition>(RecoveryArmorPath);
            recoveryArmor.SetEditorEquipmentPresentation(
                "rapid_recovery_armor", "급속회복 상의", "착용 중 급속 회복 행동을 제공합니다.", null, 6f);
            recoveryArmor.SetEditorArmorSlotKind(EquipmentSlotKind.Torso);
            recoveryArmor.SetEditorDefense(12);
            recoveryArmor.SetEditorActionGrants(new[]
            {
                new ItemActionGrant(recoveryAction, ItemActionAvailability.Equipped)
            });
            EditorUtility.SetDirty(recoveryArmor);

            ConsumableItemDefinition bandage = EnsureAsset<ConsumableItemDefinition>(BandagePath);
            bandage.SetEditorEquipmentPresentation(
                "bandage", "붕대", "빠르게 체력을 회복하는 수량 제한 소모품입니다.", null, 0.25f, 10);
            bandage.SetEditorActionGrants(new[]
            {
                new ItemActionGrant(bandageAction, ItemActionAvailability.Carried)
            });
            EditorUtility.SetDirty(bandage);
            SetPlayerActionOrder(grenadeAction, 100);
            SetPlayerActionOrder(dashAction, 200);
            SetPlayerActionOrder(stimAction, 200);
            SetActionInfiniteCharges(grenadeAction);
            SetActionInfiniteCharges(stimAction);

            AdditionalEquipmentDefinition grenadeEquipment = EnsureAsset<AdditionalEquipmentDefinition>(GrenadeEquipmentPath);
            grenadeEquipment.SetEditorEquipmentPresentation(
                "frag_grenade", "수류탄", "장착 시 수류탄 행동을 제공합니다.",
                grenadeAction != null ? grenadeAction.Icon : null, 1f);
            grenadeEquipment.SetEditorGrantedActions(new[] { grenadeAction });
            EditorUtility.SetDirty(grenadeEquipment);
            AdditionalEquipmentDefinition stimEquipment = EnsureAsset<AdditionalEquipmentDefinition>(StimEquipmentPath);
            stimEquipment.SetEditorEquipmentPresentation(
                "combat_stim", "자극제", "장착 시 자극제 행동을 제공합니다.",
                stimAction != null ? stimAction.Icon : null, 0.5f);
            stimEquipment.SetEditorGrantedActions(new[] { stimAction });
            EditorUtility.SetDirty(stimEquipment);

            EquipmentCatalog equipmentCatalog = EnsureAsset<EquipmentCatalog>(EquipmentCatalogPath);
            equipmentCatalog.SetEditorEquipment(new EquippableDefinition[]
            {
                rifle, smg, shotgun, baton, recoveryArmor, ballisticPlate, grenadeEquipment, stimEquipment
            }.Where(item => item != null).ToArray());
            EditorUtility.SetDirty(equipmentCatalog);

            ItemCatalog itemCatalog = EnsureAsset<ItemCatalog>(ItemCatalogPath);
            itemCatalog.SetEditorItems(new ItemDefinition[]
            {
                rifle, smg, shotgun, baton, recoveryArmor, ballisticPlate,
                grenadeEquipment, stimEquipment, bandage
            }.Where(item => item != null).ToArray());
            EditorUtility.SetDirty(itemCatalog);

            WeaponCatalog weaponCatalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WeaponCatalogPath);
            if (weaponCatalog != null)
            {
                weaponCatalog.SetEditorWeapons(new[] { rifle, smg, shotgun, baton }.Where(item => item != null).ToArray());
                EditorUtility.SetDirty(weaponCatalog);
            }

            MigrateUnitDefinitions(
                leftHand, torso, supportOne, supportTwo,
                recoveryArmor, ballisticPlate, grenadeEquipment, stimEquipment, bandage,
                grenadeAction, stimAction);
            EnsureCarryCapacityStat();
            ConfigureUnitBase(layout);
            ExpandedEquipmentAssetFactory.EnsureExpandedEquipmentAssets();
            CreateEquipmentUiPrefabs();
            CreateSelectionUiPrefabs();
            DeveloperInventoryPanelPrefabFactory.CreateAndAttachDeveloperInventoryPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[장비 시스템] 슬롯, 장비, 무기 효과, 통합 선택 UI 에셋 마이그레이션 완료");
        }

        private static void ConfigureExistingWeapon(
            WeaponDefinition weapon,
            string equipmentId,
            string displayName,
            string description,
            string iconName,
            WeaponAttackBehaviorDefinition behavior,
            float weight,
            WeaponHandedness handedness)
        {
            if (weapon == null)
                return;
            weapon.SetEditorEquipmentPresentation(
                equipmentId, displayName, description, LoadEquipmentIcon(iconName), weight);
            weapon.SetEditorHandedness(handedness);
            weapon.SetEditorAttackConfiguration(behavior, Array.Empty<WeaponHitEffectDefinition>());
            EditorUtility.SetDirty(weapon);
        }

        private static void ConfigureWeaponActionGrants(
            WeaponDefinition weapon,
            params CombatActionDefinition[] actions)
        {
            if (weapon == null)
                return;
            ItemActionGrant[] grants = actions
                .Where(action => action != null)
                .Distinct()
                .Select(action => new ItemActionGrant(action, ItemActionAvailability.Equipped))
                .ToArray();
            weapon.SetEditorActionGrants(grants);
            EditorUtility.SetDirty(weapon);
        }

        private static void EnsureCarryCapacityStat()
        {
            const string catalogPath = StatDataRoot + "/UnitStatCatalog.asset";
            UnitStatDefinition carryCapacity = EnsureAsset<UnitStatDefinition>(
                StatDataRoot + "/CarryCapacityStat.asset");
            UnitStatDefinition defense = EnsureAsset<UnitStatDefinition>(
                StatDataRoot + "/DefenseStat.asset");
            UnitStatDefinition fireRate = EnsureAsset<UnitStatDefinition>(
                StatDataRoot + "/FireRateStat.asset");
            carryCapacity.SetEditorConfiguration(
                "carry_capacity",
                "휴대 한도",
                30f,
                0f,
                false,
                500,
                "과적재 판정 전에 휴대할 수 있는 인벤토리 최대 무게입니다.",
                UnitStatCategory.Utility,
                UnitStatDisplayFormat.Kilograms);
            defense.SetEditorConfiguration(
                "defense",
                "방어력",
                0f,
                0f,
                true,
                10,
                "피해를 감소시키는 수치입니다. 방어력이 높아질수록 추가 효율은 점차 줄어듭니다.",
                UnitStatCategory.Survivability,
                UnitStatDisplayFormat.Integer);
            fireRate.SetEditorConfiguration(
                "fire_rate_multiplier",
                "사격 속도",
                1f,
                0.1f,
                false,
                40,
                "조준과 반복 사격 속도에 적용되는 배율입니다. 100%가 표준 속도입니다.",
                UnitStatCategory.Offense,
                UnitStatDisplayFormat.PercentMultiplier);
            EditorUtility.SetDirty(carryCapacity);
            EditorUtility.SetDirty(defense);
            EditorUtility.SetDirty(fireRate);
            UnitStatCatalog catalog = AssetDatabase.LoadAssetAtPath<UnitStatCatalog>(catalogPath);
            if (catalog == null)
                return;
            catalog.SetEditorConfiguration(
                catalog.MaximumHealth,
                catalog.MovementSpeedMultiplier,
                catalog.HitChanceBonusPercent,
                catalog.DamageMultiplier,
                carryCapacity,
                defense,
                fireRate);
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureWeapon(
            WeaponDefinition weapon,
            string equipmentId,
            string displayName,
            string description,
            string iconName,
            WeaponPresentation presentation,
            WeaponAttackBehaviorDefinition behavior,
            WeaponHitEffectDefinition[] effects,
            int damage,
            float aimDuration,
            float fireInterval,
            int magazine,
            int reserve,
            float reload,
            float range,
            float hitChance,
            float weight,
            WeaponHandedness handedness)
        {
            weapon.SetEditorEquipmentPresentation(
                equipmentId, displayName, description, LoadEquipmentIcon(iconName), weight);
            weapon.SetEditorHandedness(handedness);
            weapon.PresentationPrefab = presentation;
            weapon.SetEditorAttackConfiguration(behavior, effects);
            weapon.Damage = damage;
            weapon.AimEnterDuration = aimDuration;
            weapon.AimedShotInterval = fireInterval;
            weapon.MagazineCapacity = magazine;
            weapon.StartingReserveAmmo = reserve;
            weapon.ReloadDuration = reload;
            weapon.RangeInCells = range;
            weapon.BaseHitChancePercent = hitChance;
            EditorUtility.SetDirty(weapon);
        }

        private static void MigrateUnitDefinitions(
            EquipmentSlotDefinition leftHand,
            EquipmentSlotDefinition torso,
            EquipmentSlotDefinition supportOne,
            EquipmentSlotDefinition supportTwo,
            ArmorDefinition recoveryArmor,
            AdditionalEquipmentDefinition ballisticPlate,
            AdditionalEquipmentDefinition grenadeEquipment,
            AdditionalEquipmentDefinition stimEquipment,
            ConsumableItemDefinition bandage,
            CombatActionDefinition grenadeAction,
            CombatActionDefinition stimAction)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/GridSquad/Data/Units" }))
            {
                UnitDefinition unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (unit == null)
                    continue;
                bool grantsGrenade = unit.name != "ParkDojin";
                bool grantsStim = unit.name is "KangMinjun" or "LimGyeongho" or "OhJaehyeon" or "YoonTaeho";
                bool usesHeavyArmor = unit.Traits.Any(trait => trait != null && trait.name == "HeavyArmor");
                WeaponDefinition firstWeapon = unit.GetDefaultWeapon(0);
                WeaponDefinition secondWeapon = unit.GetDefaultWeapon(1);
                EquippableDefinition firstSupport = usesHeavyArmor
                    ? ballisticPlate
                    : grantsGrenade ? grenadeEquipment : null;
                EquippableDefinition secondSupport = grantsStim
                    ? stimEquipment
                    : usesHeavyArmor && grantsGrenade ? grenadeEquipment : null;
                unit.SetEditorDefaultEquipment(new[]
                {
                    new EquipmentSlotAssignment(leftHand, firstWeapon),
                    new EquipmentSlotAssignment(torso, usesHeavyArmor ? recoveryArmor : null),
                    new EquipmentSlotAssignment(supportOne, firstSupport),
                    new EquipmentSlotAssignment(supportTwo, secondSupport)
                });
                List<StartingInventoryItem> startingItems = new();
                if (secondWeapon != null)
                    startingItems.Add(new StartingInventoryItem(secondWeapon, 1));
                if (bandage != null)
                    startingItems.Add(new StartingInventoryItem(bandage, 3));
                unit.SetEditorStartingInventory(startingItems.ToArray());

                SerializedObject serializedUnit = new(unit);
                SerializedProperty actions = serializedUnit.FindProperty("actionDefinitions");
                List<CombatActionDefinition> intrinsicActions = unit.ActionDefinitions
                    .Where(action => action != null
                        && action != grenadeAction
                        && action != stimAction
                        && action.ActionId != "switch_weapon")
                    .Distinct()
                    .ToList();
                actions.arraySize = intrinsicActions.Count;
                for (int index = 0; index < intrinsicActions.Count; index++)
                    actions.GetArrayElementAtIndex(index).objectReferenceValue = intrinsicActions[index];
                serializedUnit.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(unit);
            }
        }

        private static void ConfigureUnitBase(EquipmentLayoutDefinition layout)
        {
            const string path = "Assets/GridSquad/Prefabs/Units/UnitBase.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform current in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current.gameObject);
                EquipmentLoadout equipmentLoadout = root.GetComponent<EquipmentLoadout>();
                if (equipmentLoadout == null)
                    equipmentLoadout = root.AddComponent<EquipmentLoadout>();
                if (root.GetComponent<UnitInventory>() == null)
                    root.AddComponent<UnitInventory>();
                if (root.GetComponent<UnitItemInteractionController>() == null)
                    root.AddComponent<UnitItemInteractionController>();
                if (root.GetComponent<CombatantItemContextCommandProvider>() == null)
                    root.AddComponent<CombatantItemContextCommandProvider>();
                equipmentLoadout.SetEditorConfiguration(layout, Array.Empty<EquipmentSlotAssignment>());
                root.GetComponent<WeaponLoadout>()?.SetEditorEquipmentLoadout(equipmentLoadout);
                CombatActionLoadout actionLoadout = root.GetComponent<CombatActionLoadout>();
                if (actionLoadout != null)
                {
                    actionLoadout.SetEditorInnateDefinitions(actionLoadout.InnateDefinitions
                        .Where(action => action != null && action.ActionId != "basic_attack")
                        .ToArray());
                }
                AnimationClip meleeClip = AssetDatabase.LoadAllAssetsAtPath(
                        "Assets/GridSquad/Art/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium/Rig_Medium_CombatMelee.fbx")
                    .OfType<AnimationClip>()
                    .FirstOrDefault(clip => clip.name == "Melee_1H_Attack_Slice_Horizontal");
                root.GetComponentInChildren<UnitAnimationController>(true)?.SetEditorMeleeAttackClip(meleeClip);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static WeaponPresentation EnsureWeaponPresentation(
            string path,
            Material material,
            bool isBaton)
        {
            GameObject root = new(isBaton ? "RiotBatonRoot" : "ShotgunGunRoot");
            Transform gunAim = new GameObject("GunAim").transform;
            gunAim.SetParent(root.transform, false);
            GameObject body = GameObject.CreatePrimitive(isBaton ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            body.name = isBaton ? "Baton" : "Receiver";
            body.transform.SetParent(gunAim, false);
            body.transform.localRotation = isBaton ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            body.transform.localScale = isBaton ? new Vector3(0.055f, 0.48f, 0.055f) : new Vector3(0.16f, 0.12f, 0.58f);
            body.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            if (!isBaton)
            {
                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "Barrel";
                barrel.transform.SetParent(gunAim, false);
                barrel.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                barrel.transform.localScale = new Vector3(0.055f, 0.38f, 0.055f);
                barrel.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
            }
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gunAim, false);
            muzzle.localPosition = new Vector3(0f, 0f, isBaton ? 0.5f : 0.9f);
            WeaponPresentation presentation = root.AddComponent<WeaponPresentation>();
            presentation.SetEditorReferences(gunAim, muzzle, null, Vector3.forward);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<WeaponPresentation>();
        }

        private static void CreateEquipmentUiPrefabs()
        {
            CreateEquipmentSlotPrefab();
            CreateEquipmentItemCardPrefab();
        }

        private static EquipmentSlotView CreateEquipmentSlotPrefab()
        {
            GameObject root = new(
                "EquipmentSlotView",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentSlotView));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(104f, 82f);
            root.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.15f, 1f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EquipmentSlotPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<EquipmentSlotView>();
        }

        private static EquipmentItemCardView CreateEquipmentItemCardPrefab()
        {
            GameObject root = new(
                "EquipmentItemCard", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(EquipmentItemCardView));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 68f);
            root.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.13f, 1f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EquipmentItemCardPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<EquipmentItemCardView>();
        }

        private static void CreateSelectionUiPrefabs()
        {
            EquipmentSlotView slotPrefab = AssetDatabase.LoadAssetAtPath<EquipmentSlotView>(EquipmentSlotPrefabPath);
            EquipmentItemCardView itemPrefab = AssetDatabase.LoadAssetAtPath<EquipmentItemCardView>(EquipmentItemCardPrefabPath);
            CombatActionButtonView actionPrefab = CreateCombatActionButtonPrefab();
            Button commandButtonPrefab = CreateContextCommandButtonPrefab();

            GameObject root = new(
                "SelectionUiRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            Stretch(root.GetComponent<RectTransform>());

            Text messageText = CreateText("CommandMessage", root.transform, string.Empty, 16, TextAnchor.MiddleCenter);
            SetRect(messageText.rectTransform, new Vector2(0.3f, 0.02f), new Vector2(0.7f, 0.075f));

            GameObject overlayObject = new(
                "FloatingUiOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            overlayObject.transform.SetParent(root.transform, false);
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            Canvas overlayCanvas = overlayObject.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 100;

            GameObject menuObject = CreatePanel("ContextMenu", overlayObject.transform, new Color(0.025f, 0.035f, 0.05f, 0.99f));
            RectTransform menuRect = menuObject.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuRect.pivot = new Vector2(0f, 1f);
            menuRect.sizeDelta = new Vector2(280f, 40f);
            VerticalLayoutGroup menuLayout = menuObject.AddComponent<VerticalLayoutGroup>();
            menuLayout.padding = new RectOffset(6, 6, 6, 6);
            menuLayout.spacing = 3f;
            menuLayout.childControlHeight = false;
            menuLayout.childForceExpandHeight = false;
            ContentSizeFitter menuFitter = menuObject.AddComponent<ContentSizeFitter>();
            menuFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ContextFloatingMenuController floatingMenu = menuObject.AddComponent<ContextFloatingMenuController>();
            floatingMenu.SetEditorReferences(
                menuRect,
                menuRect,
                commandButtonPrefab,
                messageText,
                overlayRect,
                overlayCanvas);

            SelectionDetailWindowController detailWindow = CreateSelectionDetailWindow(
                root.transform, floatingMenu, slotPrefab, itemPrefab);

            GameObject inspect = CreatePanel("SelectionInspect", root.transform, new Color(0.025f, 0.04f, 0.055f, 0.97f));
            RectTransform inspectRect = inspect.GetComponent<RectTransform>();
            inspectRect.anchorMin = inspectRect.anchorMax = Vector2.zero;
            inspectRect.pivot = Vector2.zero;
            inspectRect.anchoredPosition = new Vector2(18f, 18f);
            inspectRect.sizeDelta = new Vector2(560f, 210f);

            Image portrait = CreateImage("Portrait", inspect.transform, new Color(0.12f, 0.16f, 0.2f, 1f));
            SetRect(portrait.rectTransform, new Vector2(0.025f, 0.34f), new Vector2(0.22f, 0.94f));
            portrait.preserveAspect = true;
            Text nameText = CreateText("Name", inspect.transform, "선택 대상", 24, TextAnchor.MiddleLeft);
            SetRect(nameText.rectTransform, new Vector2(0.25f, 0.76f), new Vector2(0.96f, 0.96f));
            Text subtitleText = CreateText("Subtitle", inspect.transform, string.Empty, 15, TextAnchor.MiddleLeft);
            subtitleText.color = new Color(0.58f, 0.78f, 0.9f);
            SetRect(subtitleText.rectTransform, new Vector2(0.25f, 0.64f), new Vector2(0.96f, 0.78f));
            Image healthBack = CreateImage("HealthBack", inspect.transform, new Color(0.12f, 0.05f, 0.05f, 1f));
            SetRect(healthBack.rectTransform, new Vector2(0.25f, 0.52f), new Vector2(0.96f, 0.62f));
            Image healthFill = CreateImage("HealthFill", healthBack.transform, new Color(0.2f, 0.72f, 0.33f, 1f));
            Stretch(healthFill.rectTransform);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            Text healthText = CreateText("HealthText", healthBack.transform, "HP", 13, TextAnchor.MiddleCenter);
            Text statusText = CreateText("Status", inspect.transform, string.Empty, 14, TextAnchor.UpperLeft);
            SetRect(statusText.rectTransform, new Vector2(0.25f, 0.27f), new Vector2(0.96f, 0.49f));

            Button statsButton = CreateButton("StatsButton", inspect.transform, "스탯", new Color(0.1f, 0.16f, 0.22f));
            Button equipmentInventoryButton = CreateButton(
                "EquipmentInventoryButton",
                inspect.transform,
                "장비 / 인벤토리",
                new Color(0.1f, 0.16f, 0.22f));
            Button traitsButton = CreateButton("TraitsButton", inspect.transform, "특성", new Color(0.1f, 0.16f, 0.22f));
            SetRect(statsButton.GetComponent<RectTransform>(), new Vector2(0.025f, 0.06f), new Vector2(0.21f, 0.25f));
            SetRect(equipmentInventoryButton.GetComponent<RectTransform>(), new Vector2(0.23f, 0.06f), new Vector2(0.57f, 0.25f));
            SetRect(traitsButton.GetComponent<RectTransform>(), new Vector2(0.59f, 0.06f), new Vector2(0.82f, 0.25f));

            GameObject actionViewportObject = new(
                "ActionViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            actionViewportObject.transform.SetParent(root.transform, false);
            RectTransform actionViewportRect = actionViewportObject.GetComponent<RectTransform>();
            actionViewportRect.anchorMin = new Vector2(0f, 0f);
            actionViewportRect.anchorMax = new Vector2(1f, 0f);
            actionViewportRect.pivot = Vector2.zero;
            actionViewportRect.offsetMin = new Vector2(590f, 18f);
            actionViewportRect.offsetMax = new Vector2(-18f, 118f);
            actionViewportObject.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.035f, 0.75f);
            actionViewportObject.GetComponent<Mask>().showMaskGraphic = true;

            GameObject actionContainerObject = new(
                "ActionContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            actionContainerObject.transform.SetParent(actionViewportObject.transform, false);
            RectTransform actionRect = actionContainerObject.GetComponent<RectTransform>();
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(0f, 0.5f);
            actionRect.pivot = new Vector2(0f, 0.5f);
            actionRect.anchoredPosition = Vector2.zero;
            actionRect.sizeDelta = new Vector2(0f, 100f);
            HorizontalLayoutGroup actionLayout = actionContainerObject.GetComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = false;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;
            actionContainerObject.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect actionScroll = actionViewportObject.GetComponent<ScrollRect>();
            actionScroll.viewport = actionViewportRect;
            actionScroll.content = actionRect;
            actionScroll.horizontal = true;
            actionScroll.vertical = false;
            actionScroll.movementType = ScrollRect.MovementType.Clamped;

            SelectionInspectController inspectController = root.AddComponent<SelectionInspectController>();
            inspectController.SetEditorReferences(
                null, inspect, portrait, nameText, subtitleText, healthText, healthFill, statusText,
                actionContainerObject.transform, actionPrefab, statsButton, equipmentInventoryButton,
                traitsButton, detailWindow);

            overlayObject.transform.SetAsLastSibling();

            PrefabUtility.SaveAsPrefabAsset(root, SelectionUiRootPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static SelectionDetailWindowController CreateSelectionDetailWindow(
            Transform parent,
            ContextFloatingMenuController floatingMenu,
            EquipmentSlotView slotPrefab,
            EquipmentItemCardView itemPrefab)
        {
            GameObject window = CreatePanel("SelectionDetailWindow", parent, new Color(0.018f, 0.028f, 0.045f, 0.995f));
            RectTransform rect = window.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1120f, 700f);
            Text title = CreateText("Title", window.transform, "상세", 28, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0.04f, 0.9f), new Vector2(0.65f, 0.98f));
            Text weight = CreateText("Weight", window.transform, "무게", 17, TextAnchor.MiddleRight);
            SetRect(weight.rectTransform, new Vector2(0.65f, 0.9f), new Vector2(0.9f, 0.98f));
            Button close = CreateButton("CloseButton", window.transform, "X", new Color(0.45f, 0.12f, 0.12f));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.92f, 0.91f), new Vector2(0.98f, 0.975f));

            Button stats = CreateButton("StatsTab", window.transform, "스탯", new Color(0.08f, 0.16f, 0.24f));
            Button equipmentInventory = CreateButton(
                "EquipmentInventoryTab",
                window.transform,
                "장비 / 인벤토리",
                new Color(0.08f, 0.16f, 0.24f));
            Button traits = CreateButton("TraitsTab", window.transform, "특성", new Color(0.08f, 0.16f, 0.24f));
            SetRect(stats.GetComponent<RectTransform>(), new Vector2(0.04f, 0.82f), new Vector2(0.19f, 0.89f));
            SetRect(equipmentInventory.GetComponent<RectTransform>(), new Vector2(0.20f, 0.82f), new Vector2(0.44f, 0.89f));
            SetRect(traits.GetComponent<RectTransform>(), new Vector2(0.45f, 0.82f), new Vector2(0.60f, 0.89f));

            Text summary = CreateText("Summary", window.transform, string.Empty, 17, TextAnchor.UpperLeft);
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            summary.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(summary.rectTransform, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.8f));

            GameObject paperDollObject = CreatePanel(
                "PaperDollPanel",
                window.transform,
                new Color(0.035f, 0.055f, 0.075f, 0.96f));
            RectTransform paperDollRect = paperDollObject.GetComponent<RectTransform>();
            SetRect(paperDollRect, new Vector2(0.04f, 0.14f), new Vector2(0.49f, 0.8f));
            CreatePaperDollSilhouette(paperDollObject.transform);
            Text paperDollTitle = CreateText("PaperDollTitle", paperDollObject.transform, "장비", 20, TextAnchor.UpperLeft);
            SetRect(paperDollTitle.rectTransform, new Vector2(0.03f, 0.91f), new Vector2(0.28f, 0.99f));

            GameObject inventoryPanel = CreatePanel(
                "InventoryPanel",
                window.transform,
                new Color(0.035f, 0.055f, 0.075f, 0.96f));
            SetRect(inventoryPanel.GetComponent<RectTransform>(), new Vector2(0.51f, 0.14f), new Vector2(0.96f, 0.8f));
            Text inventoryTitle = CreateText("InventoryTitle", inventoryPanel.transform, "인벤토리", 20, TextAnchor.UpperLeft);
            SetRect(inventoryTitle.rectTransform, new Vector2(0.04f, 0.91f), new Vector2(0.5f, 0.99f));

            GameObject viewport = new(
                "InventoryViewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(Mask),
                typeof(InventoryDropZone));
            viewport.transform.SetParent(inventoryPanel.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            SetRect(viewportRect, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.89f));
            viewport.GetComponent<Image>().color = new Color(0.018f, 0.03f, 0.045f, 0.98f);
            viewport.GetComponent<Mask>().showMaskGraphic = true;
            InventoryDropZone inventoryDropTarget = viewport.GetComponent<InventoryDropZone>();

            GameObject inventoryListObject = new(
                "InventoryListContent",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            inventoryListObject.transform.SetParent(viewport.transform, false);
            RectTransform inventoryListRect = inventoryListObject.GetComponent<RectTransform>();
            inventoryListRect.anchorMin = new Vector2(0f, 1f);
            inventoryListRect.anchorMax = new Vector2(1f, 1f);
            inventoryListRect.pivot = new Vector2(0.5f, 1f);
            inventoryListRect.anchoredPosition = Vector2.zero;
            inventoryListRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup inventoryLayout = inventoryListObject.GetComponent<VerticalLayoutGroup>();
            inventoryLayout.padding = new RectOffset(8, 8, 8, 8);
            inventoryLayout.spacing = 6f;
            inventoryLayout.childControlWidth = true;
            inventoryLayout.childControlHeight = false;
            inventoryLayout.childForceExpandWidth = true;
            inventoryLayout.childForceExpandHeight = false;
            inventoryListObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect inventoryScroll = inventoryPanel.AddComponent<ScrollRect>();
            inventoryScroll.viewport = viewportRect;
            inventoryScroll.content = inventoryListRect;
            inventoryScroll.horizontal = false;
            inventoryScroll.vertical = true;
            inventoryScroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject dropZone = CreatePanel("WorldItemDropZone", window.transform, new Color(0.36f, 0.12f, 0.1f, 0.9f));
            WorldItemDropZone worldDropTarget = dropZone.AddComponent<WorldItemDropZone>();
            SetRect(dropZone.GetComponent<RectTransform>(), new Vector2(0.32f, 0.035f), new Vector2(0.68f, 0.105f));
            CreateText("Label", dropZone.transform, "여기에 놓아 현재 칸에 버리기", 15, TextAnchor.MiddleCenter);

            SelectionDetailWindowController controller = window.AddComponent<SelectionDetailWindowController>();
            controller.SetEditorReferences(
                window,
                title,
                summary,
                weight,
                paperDollRect,
                inventoryListObject.transform,
                inventoryDropTarget,
                worldDropTarget,
                slotPrefab,
                itemPrefab,
                stats,
                equipmentInventory,
                traits,
                close,
                floatingMenu,
                null);
            return controller;
        }

        private static void CreatePaperDollSilhouette(Transform parent)
        {
            Color silhouette = new(0.16f, 0.2f, 0.25f, 0.72f);
            CreateSilhouettePart("Head", parent, silhouette, new Vector2(0.42f, 0.72f), new Vector2(0.58f, 0.88f));
            CreateSilhouettePart("Torso", parent, silhouette, new Vector2(0.38f, 0.41f), new Vector2(0.62f, 0.72f));
            CreateSilhouettePart("LeftArm", parent, silhouette, new Vector2(0.27f, 0.42f), new Vector2(0.39f, 0.68f));
            CreateSilhouettePart("RightArm", parent, silhouette, new Vector2(0.61f, 0.42f), new Vector2(0.73f, 0.68f));
            CreateSilhouettePart("LeftLeg", parent, silhouette, new Vector2(0.39f, 0.12f), new Vector2(0.49f, 0.42f));
            CreateSilhouettePart("RightLeg", parent, silhouette, new Vector2(0.51f, 0.12f), new Vector2(0.61f, 0.42f));
        }

        private static void CreateSilhouettePart(
            string objectName,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Image image = CreateImage(objectName, parent, color);
            image.raycastTarget = false;
            SetRect(image.rectTransform, anchorMin, anchorMax);
        }

        private static CombatActionButtonView CreateCombatActionButtonPrefab()
        {
            GameObject root = new("CombatActionButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CombatActionButtonView));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(72f, 72f);
            root.GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.18f, 0.98f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CombatActionButtonPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<CombatActionButtonView>();
        }

        private static Button CreateContextCommandButtonPrefab()
        {
            GameObject root = new("ContextCommandButton", typeof(RectTransform), typeof(Image), typeof(Button));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(268f, 52f);
            root.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 1f);
            CreateText("Label", root.transform, "명령", 15, TextAnchor.MiddleLeft).rectTransform.offsetMin = new Vector2(12f, 0f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ContextCommandButtonPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<Button>();
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            CreateText("Label", buttonObject.transform, label, 15, TextAnchor.MiddleCenter);
            return buttonObject.GetComponent<Button>();
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int size,
            TextAnchor alignment)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }

        private static EquipmentSlotDefinition EnsureSlot(
            string assetName,
            string id,
            string displayName,
            EquipmentCategory category,
            EquipmentSlotKind slotKind,
            Vector2 position)
        {
            EquipmentSlotDefinition slot = EnsureAsset<EquipmentSlotDefinition>(
                $"{EquipmentSlotsRoot}/{assetName}.asset");
            slot.SetEditorConfiguration(id, displayName, category, slotKind, position);
            EditorUtility.SetDirty(slot);
            return slot;
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = $"Assets/GridSquad/Materials/Weapons/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Sprite LoadEquipmentIcon(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/GridSquad/Art/UI/Equipment/{fileName}");

        private static void ConfigureEquipmentIconImporters()
        {
            string[] iconNames = { "Rifle.png", "Smg.png", "Shotgun.png", "RiotBaton.png", "BallisticPlate.png" };
            foreach (string iconName in iconNames)
            {
                string path = $"Assets/GridSquad/Art/UI/Equipment/{iconName}";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                if (changed)
                    importer.SaveAndReimport();
            }
        }

        private static void SetPlayerActionOrder(CombatActionDefinition action, int order)
        {
            if (action == null)
                return;
            SerializedObject serializedAction = new(action);
            serializedAction.FindProperty("playerSlotOrder").intValue = order;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
        }

        private static void SetActionInfiniteCharges(CombatActionDefinition action)
        {
            if (action == null)
                return;
            SerializedObject serializedAction = new(action);
            serializedAction.FindProperty("startingCharges").intValue = -1;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
