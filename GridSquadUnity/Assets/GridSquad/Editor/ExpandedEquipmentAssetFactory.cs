using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GridSquad.Editor
{
    public static class ExpandedEquipmentAssetFactory
    {
        private const string EquipmentRoot = "Assets/GridSquad/Equipment";
        private const string ArmorRoot = EquipmentRoot + "/Armor";
        private const string SupportRoot = EquipmentRoot + "/Support";
        private const string WeaponRoot = EquipmentRoot + "/Weapons/Definitions";
        private const string ActionRoot = "Assets/GridSquad/Data/Actions/Equipment";
        private const string GeneratedIconRoot = "Assets/GridSquad/Art/UI/Equipment/Generated";
        private const string EquipmentPrefabRoot = "Assets/GridSquad/Prefabs/Equipment";
        private const string WeaponPrefabRoot = "Assets/GridSquad/Prefabs/Weapons";
        private const string MaterialRoot = "Assets/GridSquad/Materials/Equipment";

        private enum EquipmentModelShape
        {
            Helmet,
            Vest,
            Gloves,
            LegArmor,
            Boots,
            Drone,
            Canister,
            Beacon,
            Backpack,
            Bandage
        }

        [MenuItem("GridSquad/장비/확장 장비 에셋 생성")]
        public static void EnsureExpandedEquipmentAssets()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            ConfigureGeneratedIconImporters();

            Material blueMaterial = EnsureMaterial("EquipmentBlue", new Color(0.08f, 0.23f, 0.32f));
            Material orangeMaterial = EnsureMaterial("EquipmentOrange", new Color(0.32f, 0.16f, 0.06f));
            Material darkMaterial = EnsureMaterial("EquipmentDark", new Color(0.08f, 0.09f, 0.11f));
            Material medicalMaterial = EnsureMaterial("EquipmentMedical", new Color(0.08f, 0.28f, 0.24f));

            GameObject reconVisorModel = EnsureEquipmentModelPrefab(
                "Armor/ReconVisorModel.prefab", EquipmentModelShape.Helmet, blueMaterial);
            GameObject impactHelmetModel = EnsureEquipmentModelPrefab(
                "Armor/ImpactHelmetModel.prefab", EquipmentModelShape.Helmet, orangeMaterial);
            GameObject reactiveVestModel = EnsureEquipmentModelPrefab(
                "Armor/ReactiveVestModel.prefab", EquipmentModelShape.Vest, orangeMaterial);
            GameObject medicRigModel = EnsureEquipmentModelPrefab(
                "Armor/MedicRigModel.prefab", EquipmentModelShape.Vest, medicalMaterial);
            GameObject stabilizerGlovesModel = EnsureEquipmentModelPrefab(
                "Armor/StabilizerGlovesModel.prefab", EquipmentModelShape.Gloves, darkMaterial);
            GameObject arcGauntletsModel = EnsureEquipmentModelPrefab(
                "Armor/ArcGauntletsModel.prefab", EquipmentModelShape.Gloves, blueMaterial);
            GameObject servoLegArmorModel = EnsureEquipmentModelPrefab(
                "Armor/ServoLegArmorModel.prefab", EquipmentModelShape.LegArmor, blueMaterial);
            GameObject anchorGreavesModel = EnsureEquipmentModelPrefab(
                "Armor/AnchorGreavesModel.prefab", EquipmentModelShape.LegArmor, orangeMaterial);
            GameObject phaseBootsModel = EnsureEquipmentModelPrefab(
                "Armor/PhaseBootsModel.prefab", EquipmentModelShape.Boots, blueMaterial);
            GameObject breacherBootsModel = EnsureEquipmentModelPrefab(
                "Armor/BreacherBootsModel.prefab", EquipmentModelShape.Boots, orangeMaterial);

            CombatActionDefinition reconVisorAction = CreateTacticalAction(
                "ReconVisor", "recon_visor_scan", "전술 스캔",
                "6초 동안 사격 간격을 18% 줄여 표적 대응 속도를 높입니다.",
                LoadGeneratedIcon("ReconVisor.png"),
                TacticalEquipmentActionEffect.CombatStim,
                TacticalEquipmentActionAnimation.UseItem,
                0, 6f, 1f, 0.82f, 6, 0, 0f, 18f, 0.45f, 310);
            CombatActionDefinition impactHelmetAction = CreateTacticalAction(
                "ImpactHelmet", "impact_helmet_guard", "충격 흡수",
                "8초 동안 다음 피해 1회를 막습니다.",
                LoadGeneratedIcon("ImpactHelmet.png"),
                TacticalEquipmentActionEffect.TemporaryBarrier,
                TacticalEquipmentActionAnimation.ShieldBlock,
                1, 8f, 1f, 1f, 1, 0, 0f, 16f, 0.35f, 311);
            CombatActionDefinition reactiveVestAction = CreateTacticalAction(
                "ReactiveVest", "reactive_vest_barrier", "반응 방벽",
                "10초 동안 다음 피해 2회를 막습니다.",
                LoadGeneratedIcon("ReactiveVest.png"),
                TacticalEquipmentActionEffect.TemporaryBarrier,
                TacticalEquipmentActionAnimation.ShieldBlock,
                2, 10f, 1f, 1f, 1, 0, 0f, 24f, 0.5f, 312);
            CombatActionDefinition medicRigAction = CreateTacticalAction(
                "MedicRig", "medic_rig_treatment", "전장 처치",
                "자신의 체력을 35 회복합니다.",
                LoadGeneratedIcon("MedicRig.png"),
                TacticalEquipmentActionEffect.RestoreHealth,
                TacticalEquipmentActionAnimation.UseItem,
                35, 0f, 1f, 1f, 1, 0, 0f, 24f, 0.8f, 313);
            CombatActionDefinition stabilizerGlovesAction = CreateTacticalAction(
                "StabilizerGloves", "stabilizer_precision_shot", "안정화 사격",
                "7칸 안의 적에게 32 피해를 주는 정밀 사격을 합니다.",
                LoadGeneratedIcon("StabilizerGloves.png"),
                TacticalEquipmentActionEffect.DirectDamage,
                TacticalEquipmentActionAnimation.WeaponAttack,
                32, 0f, 1f, 1f, 7, 0, 0f, 15f, 0.4f, 314);
            CombatActionDefinition arcGauntletsAction = CreateTacticalAction(
                "ArcGauntlets", "arc_gauntlets_strike", "전격 타격",
                "2칸 안의 적에게 24 피해를 주고 1.2초 기절시킵니다.",
                LoadGeneratedIcon("ArcGauntlets.png"),
                TacticalEquipmentActionEffect.DirectDamage,
                TacticalEquipmentActionAnimation.MeleeAttack,
                24, 0f, 1f, 1f, 2, 0, 1.2f, 18f, 0.35f, 315);
            CombatActionDefinition servoLegArmorAction = CreateTacticalAction(
                "ServoLegArmor", "servo_leg_overdrive", "서보 과출력",
                "7초 동안 이동 속도를 45% 높입니다.",
                LoadGeneratedIcon("ServoLegArmor.png"),
                TacticalEquipmentActionEffect.CombatStim,
                TacticalEquipmentActionAnimation.UseItem,
                0, 7f, 1.45f, 1f, 1, 0, 0f, 20f, 0.35f, 316);
            CombatActionDefinition anchorGreavesAction = CreateTacticalAction(
                "AnchorGreaves", "anchor_greaves_lock", "지면 고정",
                "12초 동안 다음 피해 2회를 막습니다.",
                LoadGeneratedIcon("AnchorGreaves.png"),
                TacticalEquipmentActionEffect.TemporaryBarrier,
                TacticalEquipmentActionAnimation.ShieldBlock,
                2, 12f, 1f, 1f, 1, 0, 0f, 26f, 0.5f, 317);
            CombatActionDefinition phaseBootsAction = CreateDashAction(
                "PhaseBoots", "phase_boots_step", "위상 이동",
                "직선으로 최대 4칸을 빠르게 이동합니다.",
                LoadGeneratedIcon("PhaseBoots.png"), 4, 4.5f, 12f, 318);
            CombatActionDefinition breacherBootsAction = CreateDashAction(
                "BreacherBoots", "breacher_boots_charge", "돌파 질주",
                "직선으로 최대 3칸을 강하게 돌진합니다.",
                LoadGeneratedIcon("BreacherBoots.png"), 3, 5f, 10f, 319);

            ArmorDefinition[] armors =
            {
                CreateArmor("ReconVisor", "recon_visor", "정찰 바이저",
                    "전술 스캔 기능이 달린 경량 머리 장비입니다.", EquipmentSlotKind.Head, 2,
                    LoadGeneratedIcon("ReconVisor.png"), reconVisorAction, 1.5f, reconVisorModel),
                CreateArmor("ImpactHelmet", "impact_helmet", "충격 흡수 헬멧",
                    "충격 흡수층으로 순간 피해를 버팁니다.", EquipmentSlotKind.Head, 4,
                    LoadGeneratedIcon("ImpactHelmet.png"), impactHelmetAction, 2.2f, impactHelmetModel),
                CreateArmor("ReactiveVest", "reactive_vest", "반응 장갑복",
                    "피격 순간 전개되는 반응식 장갑복입니다.", EquipmentSlotKind.Torso, 6,
                    LoadGeneratedIcon("ReactiveVest.png"), reactiveVestAction, 6.5f, reactiveVestModel),
                CreateArmor("MedicRig", "medic_rig", "의무 지원복",
                    "자가 처치 장치를 내장한 전장 의무 장비입니다.", EquipmentSlotKind.Torso, 3,
                    LoadGeneratedIcon("MedicRig.png"), medicRigAction, 5.5f, medicRigModel),
                CreateArmor("StabilizerGloves", "stabilizer_gloves", "안정화 장갑",
                    "반동을 억제하는 정밀 사격용 장갑입니다.", EquipmentSlotKind.Hands, 2,
                    LoadGeneratedIcon("StabilizerGloves.png"), stabilizerGlovesAction, 1.2f, stabilizerGlovesModel),
                CreateArmor("ArcGauntlets", "arc_gauntlets", "전도성 건틀릿",
                    "근거리 전격을 방출하는 강화 건틀릿입니다.", EquipmentSlotKind.Hands, 3,
                    LoadGeneratedIcon("ArcGauntlets.png"), arcGauntletsAction, 2f, arcGauntletsModel),
                CreateArmor("ServoLegArmor", "servo_leg_armor", "서보 하의",
                    "보행 출력을 증폭하는 동력식 하체 장갑입니다.", EquipmentSlotKind.Legs, 3,
                    LoadGeneratedIcon("ServoLegArmor.png"), servoLegArmorAction, 3.8f, servoLegArmorModel),
                CreateArmor("AnchorGreaves", "anchor_greaves", "고정식 각갑",
                    "지면 고정 장치가 달린 중형 하체 장갑입니다.", EquipmentSlotKind.Legs, 5,
                    LoadGeneratedIcon("AnchorGreaves.png"), anchorGreavesAction, 5f, anchorGreavesModel),
                CreateArmor("PhaseBoots", "phase_boots", "위상 부츠",
                    "짧은 거리를 순간 이동하는 경량 전술화입니다.", EquipmentSlotKind.Feet, 2,
                    LoadGeneratedIcon("PhaseBoots.png"), phaseBootsAction, 1.5f, phaseBootsModel),
                CreateArmor("BreacherBoots", "breacher_boots", "충격 돌파화",
                    "강한 추진력으로 전선을 돌파하는 중형 전술화입니다.", EquipmentSlotKind.Feet, 4,
                    LoadGeneratedIcon("BreacherBoots.png"), breacherBootsAction, 2.7f, breacherBootsModel)
            };

            WeaponAttackBehaviorDefinition hitscan = AssetDatabase.LoadAssetAtPath<WeaponAttackBehaviorDefinition>(
                EquipmentRoot + "/Weapons/AttackBehaviors/HitscanAttack.asset");
            WeaponAttackBehaviorDefinition melee = AssetDatabase.LoadAssetAtPath<WeaponAttackBehaviorDefinition>(
                EquipmentRoot + "/Weapons/AttackBehaviors/MeleeAttack.asset");
            Material burstMaterial = EnsureMaterial("BurstCarbine", new Color(0.09f, 0.16f, 0.2f));
            Material railMaterial = EnsureMaterial("RailRifle", new Color(0.06f, 0.12f, 0.18f));
            Material axeMaterial = EnsureMaterial("BreachAxe", new Color(0.18f, 0.12f, 0.07f));
            WeaponPresentation burstPresentation = EnsureWeaponPresentation(
                "BurstCarbineRoot.prefab", burstMaterial, WeaponAttackMode.Hitscan, 0.65f);
            WeaponPresentation railPresentation = EnsureWeaponPresentation(
                "RailRifleRoot.prefab", railMaterial, WeaponAttackMode.Hitscan, 1.05f);
            WeaponPresentation axePresentation = EnsureWeaponPresentation(
                "BreachAxeRoot.prefab", axeMaterial, WeaponAttackMode.Melee, 0.72f);

            CombatActionDefinition burstAction = CreateTacticalAction(
                "BurstCarbine", "burst_carbine_three_round", "3점사",
                "6칸 안의 적에게 30 피해를 집중합니다.", LoadGeneratedIcon("BurstCarbine.png"),
                TacticalEquipmentActionEffect.DirectDamage, TacticalEquipmentActionAnimation.WeaponAttack,
                30, 0f, 1f, 1f, 6, 0, 0f, 10f, 0.35f, 330);
            CombatActionDefinition railAction = CreateTacticalAction(
                "RailRifle", "rail_rifle_penetrator", "관통 사격",
                "9칸 안의 적에게 58 피해를 줍니다.", LoadGeneratedIcon("RailRifle.png"),
                TacticalEquipmentActionEffect.DirectDamage, TacticalEquipmentActionAnimation.WeaponAttack,
                58, 0f, 1f, 1f, 9, 0, 0f, 22f, 0.65f, 331);
            CombatActionDefinition axeAction = CreateTacticalAction(
                "BreachAxe", "breach_axe_sunder", "균열 강타",
                "2칸 안의 적에게 42 피해를 주고 1칸 밀칩니다.", LoadGeneratedIcon("BreachAxe.png"),
                TacticalEquipmentActionEffect.DirectDamage, TacticalEquipmentActionAnimation.MeleeAttack,
                42, 0f, 1f, 1f, 2, 1, 0f, 14f, 0.45f, 332);
            CombatActionDefinition shieldAction = CreateTacticalAction(
                "RiotShield", "riot_shield_guard", "방패 전개",
                "8초 동안 다음 피해 3회를 막습니다.", LoadGeneratedIcon("RiotShield.png"),
                TacticalEquipmentActionEffect.TemporaryBarrier, TacticalEquipmentActionAnimation.ShieldBlock,
                3, 8f, 1f, 1f, 1, 0, 0f, 24f, 0.45f, 333);

            CombatActionDefinition basicAttack = AssetDatabase.LoadAssetAtPath<CombatActionDefinition>(
                "Assets/GridSquad/Data/Actions/BasicAttackAction.asset");
            WeaponDefinition burstCarbine = CreateWeapon(
                "BurstCarbine", "burst_carbine", "점사 카빈", "빠른 3점사에 특화된 돌격 카빈입니다.",
                LoadGeneratedIcon("BurstCarbine.png"), burstPresentation, hitscan,
                18, 0.35f, 0.55f, 24, 96, 1.45f, 6f, 78f, 3.4f,
                WeaponHandedness.TwoHanded, basicAttack, burstAction);
            WeaponDefinition railRifle = CreateWeapon(
                "RailRifle", "rail_rifle", "레일 지정사수소총", "긴 사거리와 강한 단발 화력을 가진 전자기 소총입니다.",
                LoadGeneratedIcon("RailRifle.png"), railPresentation, hitscan,
                52, 0.8f, 1.8f, 4, 16, 2.4f, 9f, 88f, 5.8f,
                WeaponHandedness.TwoHanded, basicAttack, railAction);
            WeaponDefinition breachAxe = CreateWeapon(
                "BreachAxe", "breach_axe", "돌파 도끼", "장갑과 엄폐물을 부수는 중형 근접 도끼입니다.",
                LoadGeneratedIcon("BreachAxe.png"), axePresentation, melee,
                34, 0.2f, 1.05f, 1, 0, 0.1f, 1.6f, 100f, 4.2f,
                WeaponHandedness.OneHanded, basicAttack, axeAction);

            GameObject shieldModel = EnsureShieldPrefab("RiotShieldRoot.prefab", darkMaterial);
            OffHandDefinition riotShield = EnsureAsset<OffHandDefinition>(WeaponRoot + "/RiotShield.asset");
            riotShield.SetEditorEquipmentPresentation(
                "riot_shield", "진압 방패", "피해를 막는 전개 행동을 제공하는 보조손 방패입니다.",
                LoadGeneratedIcon("RiotShield.png"), 5.2f);
            riotShield.SetEditorPresentationPrefab(shieldModel);
            riotShield.SetEditorWorldPresentationPrefab(shieldModel);
            riotShield.SetEditorActionGrants(new[]
            {
                new ItemActionGrant(shieldAction, ItemActionAvailability.Equipped)
            });
            EditorUtility.SetDirty(riotShield);

            GameObject medicalDroneModel = EnsureEquipmentModelPrefab(
                "Support/MedicalDroneModel.prefab", EquipmentModelShape.Drone, medicalMaterial);
            GameObject smokeProjectorModel = EnsureEquipmentModelPrefab(
                "Support/SmokeProjectorModel.prefab", EquipmentModelShape.Canister, orangeMaterial);
            GameObject targetingBeaconModel = EnsureEquipmentModelPrefab(
                "Support/TargetingBeaconModel.prefab", EquipmentModelShape.Beacon, blueMaterial);
            GameObject ammoPackModel = EnsureEquipmentModelPrefab(
                "Support/AmmoPackModel.prefab", EquipmentModelShape.Backpack, darkMaterial);

            CombatActionDefinition medicalDroneAction = CreateTacticalAction(
                "MedicalDrone", "medical_drone_repair", "치료 드론",
                "치료 드론을 가동해 자신의 체력을 30 회복합니다.", LoadGeneratedIcon("MedicalDrone.png"),
                TacticalEquipmentActionEffect.RestoreHealth, TacticalEquipmentActionAnimation.UseItem,
                30, 0f, 1f, 1f, 1, 0, 0f, 20f, 0.7f, 340);
            CombatActionDefinition smokeProjectorAction = CreateTacticalAction(
                "SmokeProjector", "smoke_projector_screen", "연막 전개",
                "연막 엄폐로 9초 동안 다음 피해 2회를 막습니다.", LoadGeneratedIcon("SmokeProjector.png"),
                TacticalEquipmentActionEffect.TemporaryBarrier, TacticalEquipmentActionAnimation.Throw,
                2, 9f, 1f, 1f, 1, 0, 0f, 22f, 0.55f, 341);
            CombatActionDefinition targetingBeaconAction = CreateTacticalAction(
                "TargetingBeacon", "targeting_beacon_lase", "표적 조명",
                "8칸 안의 적에게 26 피해를 주고 0.8초 기절시킵니다.", LoadGeneratedIcon("TargetingBeacon.png"),
                TacticalEquipmentActionEffect.DirectDamage, TacticalEquipmentActionAnimation.UseItem,
                26, 0f, 1f, 1f, 8, 0, 0.8f, 18f, 0.5f, 342);
            CombatActionDefinition ammoPackAction = CreateTacticalAction(
                "AmmoPack", "ammo_pack_replenish", "전술 탄약 보급",
                "현재 무기에 탄약 36발을 즉시 보급합니다.", LoadGeneratedIcon("AmmoPack.png"),
                TacticalEquipmentActionEffect.ReplenishAmmunition, TacticalEquipmentActionAnimation.UseItem,
                36, 0f, 1f, 1f, 1, 0, 0f, 28f, 0.8f, 343);

            AdditionalEquipmentDefinition[] supports =
            {
                CreateSupport("MedicalDrone", "medical_drone", "의무 드론",
                    "자가 치료를 지원하는 소형 드론입니다.", LoadGeneratedIcon("MedicalDrone.png"),
                    medicalDroneAction, 2.2f, medicalDroneModel),
                CreateSupport("SmokeProjector", "smoke_projector", "연막 투사기",
                    "피해를 흘려내는 전술 연막 장치입니다.", LoadGeneratedIcon("SmokeProjector.png"),
                    smokeProjectorAction, 1.8f, smokeProjectorModel),
                CreateSupport("TargetingBeacon", "targeting_beacon", "표적 지시기",
                    "원거리 표적을 조명하는 휴대식 센서입니다.", LoadGeneratedIcon("TargetingBeacon.png"),
                    targetingBeaconAction, 1.6f, targetingBeaconModel),
                CreateSupport("AmmoPack", "ammo_pack", "탄약 배낭",
                    "현재 무기의 탄약을 보충하는 전술 배낭입니다.", LoadGeneratedIcon("AmmoPack.png"),
                    ammoPackAction, 4f, ammoPackModel)
            };

            ConfigurePreviouslyMissingIcons(medicalMaterial);
            ConfigureUnitBaseOffHandAndAnimations();
            AppendCreatedItemsToCatalogs(
                armors,
                new[] { burstCarbine, railRifle, breachAxe },
                riotShield,
                supports);
            AssetDatabase.SaveAssets();
            Debug.Log("[확장 장비] 방어구 10개, 무기 3개, 방패 1개, 보조장비 4개와 고유 행동 생성을 완료했습니다.");
        }

        private static ArmorDefinition CreateArmor(
            string assetName,
            string equipmentId,
            string displayName,
            string description,
            EquipmentSlotKind slotKind,
            int blockCount,
            Sprite icon,
            CombatActionDefinition action,
            float weight,
            GameObject model)
        {
            ArmorDefinition armor = EnsureAsset<ArmorDefinition>($"{ArmorRoot}/{assetName}.asset");
            armor.SetEditorEquipmentPresentation(equipmentId, displayName, description, icon, weight);
            armor.SetEditorArmorSlotKind(slotKind);
            armor.SetEditorMaximumBlockCount(blockCount);
            armor.SetEditorWorldPresentationPrefab(model);
            armor.SetEditorActionGrants(new[]
            {
                new ItemActionGrant(action, ItemActionAvailability.Equipped)
            });
            EditorUtility.SetDirty(armor);
            return armor;
        }

        private static AdditionalEquipmentDefinition CreateSupport(
            string assetName,
            string equipmentId,
            string displayName,
            string description,
            Sprite icon,
            CombatActionDefinition action,
            float weight,
            GameObject model)
        {
            AdditionalEquipmentDefinition support = EnsureAsset<AdditionalEquipmentDefinition>(
                $"{SupportRoot}/{assetName}.asset");
            support.SetEditorEquipmentPresentation(equipmentId, displayName, description, icon, weight);
            support.SetEditorGrantedActions(new[] { action });
            support.SetEditorPassiveConfiguration(SupportEquipmentPassiveKind.None, 1, 10f);
            support.SetEditorWorldPresentationPrefab(model);
            EditorUtility.SetDirty(support);
            return support;
        }

        private static WeaponDefinition CreateWeapon(
            string assetName,
            string equipmentId,
            string displayName,
            string description,
            Sprite icon,
            WeaponPresentation presentation,
            WeaponAttackBehaviorDefinition behavior,
            int damage,
            float aimDuration,
            float fireInterval,
            int magazine,
            int reserve,
            float reload,
            float range,
            float hitChance,
            float weight,
            WeaponHandedness handedness,
            params CombatActionDefinition[] actions)
        {
            WeaponDefinition weapon = EnsureAsset<WeaponDefinition>($"{WeaponRoot}/{assetName}.asset");
            weapon.SetEditorEquipmentPresentation(equipmentId, displayName, description, icon, weight);
            weapon.SetEditorHandedness(handedness);
            weapon.PresentationPrefab = presentation;
            weapon.SetEditorWorldPresentationPrefab(presentation != null ? presentation.gameObject : null);
            weapon.SetEditorAttackConfiguration(behavior, Array.Empty<WeaponHitEffectDefinition>());
            weapon.Damage = damage;
            weapon.AimEnterDuration = aimDuration;
            weapon.AimedShotInterval = fireInterval;
            weapon.MagazineCapacity = magazine;
            weapon.StartingReserveAmmo = reserve;
            weapon.ReloadDuration = reload;
            weapon.RangeInCells = range;
            weapon.BaseHitChancePercent = hitChance;
            weapon.SetEditorActionGrants(actions
                .Where(action => action != null)
                .Distinct()
                .Select(action => new ItemActionGrant(action, ItemActionAvailability.Equipped))
                .ToArray());
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static CombatActionDefinition CreateTacticalAction(
            string assetName,
            string actionId,
            string displayName,
            string description,
            Sprite icon,
            TacticalEquipmentActionEffect effect,
            TacticalEquipmentActionAnimation animation,
            int amount,
            float duration,
            float movementMultiplier,
            float fireIntervalMultiplier,
            int range,
            int knockback,
            float stun,
            float cooldown,
            float windup,
            int order)
        {
            TacticalEquipmentActionBehaviorDefinition behavior =
                EnsureAsset<TacticalEquipmentActionBehaviorDefinition>(
                    $"{ActionRoot}/{assetName}Behavior.asset");
            behavior.SetEditorConfiguration(
                effect, animation, amount, duration, movementMultiplier, fireIntervalMultiplier,
                range, knockback, stun);
            EditorUtility.SetDirty(behavior);

            CombatActionDefinition action = EnsureAsset<CombatActionDefinition>(
                $"{ActionRoot}/{assetName}Action.asset");
            action.SetEditorConfiguration(actionId, displayName, behavior, true, true, -1, cooldown, windup);
            action.SetEditorPresentation(description, icon);
            SetPlayerActionOrder(action, order);
            EditorUtility.SetDirty(action);
            return action;
        }

        private static CombatActionDefinition CreateDashAction(
            string assetName,
            string actionId,
            string displayName,
            string description,
            Sprite icon,
            int maximumCells,
            float speedMultiplier,
            float cooldown,
            int order)
        {
            DashActionBehaviorDefinition behavior = EnsureAsset<DashActionBehaviorDefinition>(
                $"{ActionRoot}/{assetName}Behavior.asset");
            behavior.SetEditorConfiguration(maximumCells, speedMultiplier, 0f);
            EditorUtility.SetDirty(behavior);
            CombatActionDefinition action = EnsureAsset<CombatActionDefinition>(
                $"{ActionRoot}/{assetName}Action.asset");
            action.SetEditorConfiguration(actionId, displayName, behavior, true, true, -1, cooldown, 0.15f);
            action.SetEditorPresentation(description, icon);
            SetPlayerActionOrder(action, order);
            EditorUtility.SetDirty(action);
            return action;
        }

        private static void ConfigurePreviouslyMissingIcons(Material material)
        {
            AssignSerializedIcon("Assets/GridSquad/Equipment/Armor/RecoveryArmor.asset", "RecoveryArmorIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Equipment/Consumables/Bandage.asset", "BandageIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Actions/BasicAttackAction.asset", "BasicAttackIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Actions/RepositionAction.asset", "RepositionIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Actions/RecoveryAction.asset", "RecoveryActionIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Actions/BandageAction.asset", "BandageActionIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/AssaultTraining.asset", "AssaultTrainingIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/FieldSense.asset", "FieldSenseIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/FocusedFire.asset", "FocusedFireIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/HeavyArmor.asset", "HeavyArmorIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/Lightweight.asset", "LightweightIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/PrecisionShooting.asset", "PrecisionShootingIcon.png");
            AssignSerializedIcon("Assets/GridSquad/Data/Traits/Toughness.asset", "ToughnessIcon.png");

            GameObject recoveryArmorModel = EnsureEquipmentModelPrefab(
                "Armor/RecoveryArmorModel.prefab", EquipmentModelShape.Vest, material);
            GameObject bandageModel = EnsureEquipmentModelPrefab(
                "Consumables/BandageModel.prefab", EquipmentModelShape.Bandage, material);
            ArmorDefinition recoveryArmor = AssetDatabase.LoadAssetAtPath<ArmorDefinition>(
                "Assets/GridSquad/Equipment/Armor/RecoveryArmor.asset");
            ConsumableItemDefinition bandage = AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(
                "Assets/GridSquad/Equipment/Consumables/Bandage.asset");
            recoveryArmor?.SetEditorWorldPresentationPrefab(recoveryArmorModel);
            bandage?.SetEditorWorldPresentationPrefab(bandageModel);
            if (recoveryArmor != null)
                EditorUtility.SetDirty(recoveryArmor);
            if (bandage != null)
                EditorUtility.SetDirty(bandage);
        }

        private static void ConfigureUnitBaseOffHandAndAnimations()
        {
            const string path = "Assets/GridSquad/Prefabs/Units/UnitBase.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform leftHand = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "hand.l");
                if (leftHand != null)
                {
                    Transform socket = leftHand.Find("OffHandSocket");
                    if (socket == null)
                    {
                        socket = new GameObject("OffHandSocket").transform;
                        socket.SetParent(leftHand, false);
                    }
                    socket.localPosition = new Vector3(0f, 0.06f, 0f);
                    socket.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    OffHandMount mount = root.GetComponent<OffHandMount>();
                    if (mount == null)
                        mount = root.AddComponent<OffHandMount>();
                    mount.SetEditorSocket(socket);
                }

                AnimationClip[] meleeClips = AssetDatabase.LoadAllAssetsAtPath(
                        "Assets/GridSquad/Art/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium/Rig_Medium_CombatMelee.fbx")
                    .OfType<AnimationClip>()
                    .ToArray();
                UnitAnimationController animationController =
                    root.GetComponentInChildren<UnitAnimationController>(true);
                animationController?.SetEditorMeleeAttackClip(
                    meleeClips.FirstOrDefault(clip => clip.name == "Melee_1H_Attack_Chop"));
                animationController?.SetEditorShieldBlockClip(
                    meleeClips.FirstOrDefault(clip => clip.name == "Melee_Block_Attack"));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AppendCreatedItemsToCatalogs(
            IEnumerable<ArmorDefinition> armors,
            IEnumerable<WeaponDefinition> weapons,
            OffHandDefinition shield,
            IEnumerable<AdditionalEquipmentDefinition> supports)
        {
            List<EquippableDefinition> createdEquipment = armors.Cast<EquippableDefinition>()
                .Concat(weapons)
                .Concat(new EquippableDefinition[] { shield })
                .Concat(supports)
                .Where(item => item != null)
                .ToList();

            EquipmentCatalog equipmentCatalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(
                EquipmentAssetFactory.EquipmentCatalogPath);
            if (equipmentCatalog != null)
            {
                equipmentCatalog.SetEditorEquipment(equipmentCatalog.Equipment
                    .Concat(createdEquipment)
                    .Where(item => item != null)
                    .GroupBy(item => item.EquipmentId)
                    .Select(group => group.Last())
                    .ToArray());
                EditorUtility.SetDirty(equipmentCatalog);
            }

            ItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(
                EquipmentAssetFactory.ItemCatalogPath);
            if (itemCatalog != null)
            {
                itemCatalog.SetEditorItems(itemCatalog.Items
                    .Concat(createdEquipment)
                    .Where(item => item != null)
                    .GroupBy(item => item.EquipmentId)
                    .Select(group => group.Last())
                    .ToArray());
                EditorUtility.SetDirty(itemCatalog);
            }

            WeaponCatalog weaponCatalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(
                "Assets/GridSquad/Equipment/Catalogs/WeaponCatalog.asset");
            if (weaponCatalog != null)
            {
                WeaponDefinition[] allWeaponDefinitions = AssetDatabase.FindAssets(
                        "t:WeaponDefinition",
                        new[] { WeaponRoot })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<WeaponDefinition>)
                    .Where(weapon => weapon != null)
                    .GroupBy(weapon => weapon.EquipmentId)
                    .Select(group => group.Last())
                    .OrderBy(weapon => weapon.DisplayName)
                    .ToArray();
                weaponCatalog.SetEditorWeapons(allWeaponDefinitions);
                EditorUtility.SetDirty(weaponCatalog);
            }
        }

        private static WeaponPresentation EnsureWeaponPresentation(
            string fileName,
            Material material,
            WeaponAttackMode mode,
            float length)
        {
            GameObject root = new GameObject(fileName.Replace(".prefab", string.Empty));
            Transform aim = new GameObject("GunAim").transform;
            aim.SetParent(root.transform, false);
            if (mode == WeaponAttackMode.Melee)
            {
                AddPrimitivePart(aim, "Handle", PrimitiveType.Cylinder, material,
                    new Vector3(0f, 0f, length * 0.1f), new Vector3(0.055f, length * 0.5f, 0.055f),
                    Quaternion.Euler(90f, 0f, 0f));
                AddPrimitivePart(aim, "AxeHead", PrimitiveType.Cube, material,
                    new Vector3(0.16f, 0f, length * 0.75f), new Vector3(0.32f, 0.08f, 0.28f),
                    Quaternion.Euler(0f, 0f, 18f));
            }
            else
            {
                AddPrimitivePart(aim, "Receiver", PrimitiveType.Cube, material,
                    Vector3.zero, new Vector3(0.17f, 0.13f, length * 0.55f), Quaternion.identity);
                AddPrimitivePart(aim, "Barrel", PrimitiveType.Cylinder, material,
                    new Vector3(0f, 0f, length * 0.55f), new Vector3(0.045f, length * 0.35f, 0.045f),
                    Quaternion.Euler(90f, 0f, 0f));
                AddPrimitivePart(aim, "Stock", PrimitiveType.Cube, material,
                    new Vector3(0f, 0f, -length * 0.35f), new Vector3(0.12f, 0.16f, length * 0.18f),
                    Quaternion.Euler(12f, 0f, 0f));
                AddPrimitivePart(aim, "Magazine", PrimitiveType.Cube, material,
                    new Vector3(0f, -0.12f, -0.02f), new Vector3(0.1f, 0.2f, 0.1f),
                    Quaternion.Euler(8f, 0f, 0f));
            }
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(aim, false);
            muzzle.localPosition = new Vector3(0f, 0f, length);
            WeaponPresentation presentation = root.AddComponent<WeaponPresentation>();
            presentation.SetEditorReferences(aim, muzzle, null, Vector3.forward);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{WeaponPrefabRoot}/{fileName}");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<WeaponPresentation>();
        }

        private static GameObject EnsureShieldPrefab(string fileName, Material material)
        {
            GameObject root = new GameObject(fileName.Replace(".prefab", string.Empty));
            AddPrimitivePart(root.transform, "ShieldPlate", PrimitiveType.Cube, material,
                new Vector3(0f, 0.16f, 0f), new Vector3(0.52f, 0.7f, 0.09f), Quaternion.identity);
            AddPrimitivePart(root.transform, "Viewport", PrimitiveType.Cube, material,
                new Vector3(0f, 0.35f, -0.06f), new Vector3(0.3f, 0.12f, 0.03f), Quaternion.identity);
            AddPrimitivePart(root.transform, "Handle", PrimitiveType.Cylinder, material,
                new Vector3(0f, 0.02f, 0.12f), new Vector3(0.05f, 0.2f, 0.05f),
                Quaternion.Euler(90f, 0f, 0f));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{WeaponPrefabRoot}/{fileName}");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject EnsureEquipmentModelPrefab(
            string relativePath,
            EquipmentModelShape shape,
            Material material)
        {
            string path = $"{EquipmentPrefabRoot}/{relativePath}";
            EnsureAssetFolderForPath(path);
            GameObject root = new GameObject(relativePath.Replace(".prefab", string.Empty).Split('/').Last());
            switch (shape)
            {
                case EquipmentModelShape.Helmet:
                    AddPrimitivePart(root.transform, "Shell", PrimitiveType.Sphere, material,
                        Vector3.up * 0.2f, new Vector3(0.42f, 0.34f, 0.42f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "Visor", PrimitiveType.Cube, material,
                        new Vector3(0f, 0.18f, 0.33f), new Vector3(0.34f, 0.11f, 0.04f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Vest:
                    AddPrimitivePart(root.transform, "Chest", PrimitiveType.Cube, material,
                        Vector3.up * 0.28f, new Vector3(0.5f, 0.55f, 0.22f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "ShoulderLeft", PrimitiveType.Sphere, material,
                        new Vector3(-0.32f, 0.42f, 0f), Vector3.one * 0.18f, Quaternion.identity);
                    AddPrimitivePart(root.transform, "ShoulderRight", PrimitiveType.Sphere, material,
                        new Vector3(0.32f, 0.42f, 0f), Vector3.one * 0.18f, Quaternion.identity);
                    break;
                case EquipmentModelShape.Gloves:
                    AddPrimitivePart(root.transform, "GloveLeft", PrimitiveType.Capsule, material,
                        new Vector3(-0.18f, 0.18f, 0f), new Vector3(0.16f, 0.24f, 0.16f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "GloveRight", PrimitiveType.Capsule, material,
                        new Vector3(0.18f, 0.18f, 0f), new Vector3(0.16f, 0.24f, 0.16f), Quaternion.identity);
                    break;
                case EquipmentModelShape.LegArmor:
                    AddPrimitivePart(root.transform, "LegLeft", PrimitiveType.Capsule, material,
                        new Vector3(-0.16f, 0.3f, 0f), new Vector3(0.17f, 0.42f, 0.17f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "LegRight", PrimitiveType.Capsule, material,
                        new Vector3(0.16f, 0.3f, 0f), new Vector3(0.17f, 0.42f, 0.17f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Boots:
                    AddPrimitivePart(root.transform, "BootLeft", PrimitiveType.Cube, material,
                        new Vector3(-0.18f, 0.12f, 0.08f), new Vector3(0.18f, 0.24f, 0.38f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "BootRight", PrimitiveType.Cube, material,
                        new Vector3(0.18f, 0.12f, 0.08f), new Vector3(0.18f, 0.24f, 0.38f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Drone:
                    AddPrimitivePart(root.transform, "DroneBody", PrimitiveType.Cube, material,
                        Vector3.up * 0.28f, new Vector3(0.55f, 0.2f, 0.42f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "RotorLeft", PrimitiveType.Cylinder, material,
                        new Vector3(-0.34f, 0.3f, 0f), new Vector3(0.16f, 0.03f, 0.16f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "RotorRight", PrimitiveType.Cylinder, material,
                        new Vector3(0.34f, 0.3f, 0f), new Vector3(0.16f, 0.03f, 0.16f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Canister:
                    AddPrimitivePart(root.transform, "Canister", PrimitiveType.Cylinder, material,
                        Vector3.up * 0.28f, new Vector3(0.24f, 0.34f, 0.24f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Beacon:
                    AddPrimitivePart(root.transform, "Sensor", PrimitiveType.Cube, material,
                        Vector3.up * 0.42f, new Vector3(0.34f, 0.24f, 0.25f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "Stand", PrimitiveType.Cylinder, material,
                        Vector3.up * 0.18f, new Vector3(0.06f, 0.25f, 0.06f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Backpack:
                    AddPrimitivePart(root.transform, "Pack", PrimitiveType.Cube, material,
                        Vector3.up * 0.3f, new Vector3(0.45f, 0.58f, 0.28f), Quaternion.identity);
                    AddPrimitivePart(root.transform, "AmmoBox", PrimitiveType.Cube, material,
                        new Vector3(0f, 0.24f, 0.2f), new Vector3(0.38f, 0.22f, 0.16f), Quaternion.identity);
                    break;
                case EquipmentModelShape.Bandage:
                    AddPrimitivePart(root.transform, "Bandage", PrimitiveType.Cylinder, material,
                        Vector3.up * 0.12f, new Vector3(0.18f, 0.24f, 0.18f), Quaternion.Euler(0f, 0f, 90f));
                    break;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AddPrimitivePart(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Material material,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialRoot}/{name}.mat";
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

        private static void AssignSerializedIcon(string assetPath, string iconName)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Sprite icon = LoadGeneratedIcon(iconName);
            if (asset == null || icon == null)
                return;
            SerializedObject serializedAsset = new SerializedObject(asset);
            SerializedProperty iconProperty = serializedAsset.FindProperty("icon");
            if (iconProperty == null)
                return;
            iconProperty.objectReferenceValue = icon;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static Sprite LoadGeneratedIcon(string fileName)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedIconRoot}/{fileName}");

        private static void ConfigureGeneratedIconImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedIconRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;
                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.mipmapEnabled;
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
            SerializedObject serializedAction = new SerializedObject(action);
            serializedAction.FindProperty("playerSlotOrder").intValue = order;
            serializedAction.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            EnsureAssetFolderForPath(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureAssetFolderForPath(string assetPath)
        {
            string folder = assetPath.Substring(0, assetPath.LastIndexOf('/'));
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolderForPath($"{ActionRoot}/placeholder.asset");
            EnsureAssetFolderForPath($"{EquipmentPrefabRoot}/Armor/placeholder.prefab");
            EnsureAssetFolderForPath($"{EquipmentPrefabRoot}/Support/placeholder.prefab");
            EnsureAssetFolderForPath($"{EquipmentPrefabRoot}/Consumables/placeholder.prefab");
            EnsureAssetFolderForPath($"{MaterialRoot}/placeholder.mat");
        }
    }
}
