using System.Collections.Generic;
using System.Linq;
using GridSquad;
using GridSquad.Editor;
using UnityEditor;
using UnityEngine;

namespace GridSquadEditor
{
    public static class CombatDataValidator
    {
        private static readonly EquipmentSlotKind[] RequiredSlotKinds =
        {
            EquipmentSlotKind.LeftHand,
            EquipmentSlotKind.RightHand,
            EquipmentSlotKind.Head,
            EquipmentSlotKind.Torso,
            EquipmentSlotKind.Legs,
            EquipmentSlotKind.Hands,
            EquipmentSlotKind.Feet,
            EquipmentSlotKind.SupportOne,
            EquipmentSlotKind.SupportTwo
        };
        private static readonly Dictionary<string, float> ExpectedInitialItemWeights = new()
        {
            ["rifle"] = 4f,
            ["smg"] = 3f,
            ["shotgun"] = 5f,
            ["riot_baton"] = 1.5f,
            ["ballistic_plate"] = 3f,
            ["frag_grenade"] = 1f,
            ["combat_stim"] = 0.5f,
            ["bandage"] = 0.25f,
            ["rapid_recovery_armor"] = 6f
        };

        [MenuItem("GridSquad/전투 데이터 검증")]
        public static void ValidateProjectData()
            => ValidateProjectDataAndReturnErrorCount();

        public static int ValidateProjectDataAndReturnErrorCount()
        {
            int errorCount = 0;
            errorCount += ValidateCombatActions();
            errorCount += ValidateUnitStats();
            errorCount += ValidateUnitsAndTraits();
            errorCount += ValidateItemsAndEquipment();
            errorCount += ValidateUiPrefabReferences();
            errorCount += ValidateMissingMonoBehaviours();
            if (errorCount == 0)
                Debug.Log("[전투 데이터] 행동, 스탯, 아이템, 9슬롯 장비, 유닛 마이그레이션, UI 프리팹 참조 검증 완료");
            else
                Debug.LogError($"[전투 데이터] {errorCount}개의 데이터 오류를 확인했습니다.");
            return errorCount;
        }

        private static int ValidateCombatActions()
        {
            int errors = 0;
            HashSet<string> actionIds = new();
            int defaultAttackCount = 0;
            int movementCommandCount = 0;
            foreach (CombatActionDefinition definition in LoadAssets<CombatActionDefinition>())
            {
                if (string.IsNullOrWhiteSpace(definition.ActionId))
                    errors += LogError("행동 ID가 비어 있습니다.", definition);
                else if (!actionIds.Add(definition.ActionId))
                    errors += LogError($"행동 ID가 중복되었습니다: {definition.ActionId}", definition);
                if (definition.Behavior == null)
                    errors += LogError("행동 전략이 연결되지 않았습니다.", definition);
                if (definition.CooldownSeconds <= 0f)
                    errors += LogError("행동 쿨다운은 0초보다 커야 합니다.", definition);
                if (definition.HasCapability(CombatActionCapabilityFlags.DefaultAttack))
                {
                    defaultAttackCount++;
                    if (!definition.HasCapability(CombatActionCapabilityFlags.PlayerVisible)
                        || definition.TargetingMode != CombatActionTargetingMode.ShootableTarget
                        || definition.PlayerSlotOrder != 0)
                    {
                        errors += LogError(
                            "기본 공격은 첫 슬롯의 플레이어 대상 지정 행동이어야 합니다.",
                            definition);
                    }
                }
                if (definition.HasCapability(CombatActionCapabilityFlags.MovementCommand))
                    movementCommandCount++;
            }
            if (defaultAttackCount != 1)
                errors += LogError($"기본 공격 행동은 정확히 하나여야 합니다. 현재 {defaultAttackCount}개", null);
            if (movementCommandCount != 1)
                errors += LogError($"이동 명령 행동은 정확히 하나여야 합니다. 현재 {movementCommandCount}개", null);
            return errors;
        }

        private static int ValidateUnitStats()
        {
            int errors = 0;
            HashSet<string> statIds = new();
            List<UnitStatDefinition> definitions = LoadAssets<UnitStatDefinition>().ToList();
            foreach (UnitStatDefinition definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.StatId))
                    errors += LogError("스탯 ID가 비어 있습니다.", definition);
                else if (!statIds.Add(definition.StatId))
                    errors += LogError($"스탯 ID가 중복되었습니다: {definition.StatId}", definition);
                errors += ValidateExpectedStatPresentation(definition);
            }
            UnitStatCatalog catalog = LoadFirstAsset<UnitStatCatalog>();
            if (catalog == null
                || catalog.MaximumHealth == null
                || catalog.MovementSpeedMultiplier == null
                || catalog.HitChanceBonusPercent == null
                || catalog.DamageMultiplier == null
                || catalog.Defense == null
                || catalog.FireRateMultiplier == null
                || catalog.CarryCapacity == null)
            {
                errors += LogError("7개 핵심 유닛 스탯 카탈로그가 완전하지 않습니다.", catalog);
            }
            else
            {
                HashSet<UnitStatDefinition> catalogDefinitions =
                    new(catalog.CoreStats.Where(definition => definition != null));
                if (catalogDefinitions.Count != 7 || definitions.Count != 7)
                    errors += LogError("운영 스탯은 카탈로그에 등록된 7개여야 합니다.", catalog);
            }
            return errors;
        }

        private static int ValidateExpectedStatPresentation(UnitStatDefinition definition)
        {
            (UnitStatCategory category, UnitStatDisplayFormat format)? expected =
                definition.StatId switch
                {
                    "maximum_health" => (UnitStatCategory.Survivability, UnitStatDisplayFormat.Integer),
                    "defense" => (UnitStatCategory.Survivability, UnitStatDisplayFormat.Integer),
                    "damage_multiplier" => (UnitStatCategory.Offense, UnitStatDisplayFormat.PercentMultiplier),
                    "hit_chance_bonus_percent" => (UnitStatCategory.Offense, UnitStatDisplayFormat.PercentagePoints),
                    "fire_rate_multiplier" => (UnitStatCategory.Offense, UnitStatDisplayFormat.PercentMultiplier),
                    "movement_speed_multiplier" => (UnitStatCategory.Mobility, UnitStatDisplayFormat.PercentMultiplier),
                    "carry_capacity" => (UnitStatCategory.Utility, UnitStatDisplayFormat.Kilograms),
                    _ => null
                };
            if (!expected.HasValue)
                return LogError($"허용되지 않은 운영 스탯입니다: {definition.StatId}", definition);
            return definition.Category != expected.Value.category
                || definition.DisplayFormat != expected.Value.format
                    ? LogError($"스탯 분류 또는 표시 형식이 올바르지 않습니다: {definition.DisplayName}", definition)
                    : 0;
        }

        private static int ValidateUnitsAndTraits()
        {
            int errors = 0;
            UnitStatCatalog catalog = LoadFirstAsset<UnitStatCatalog>();
            HashSet<UnitStatDefinition> catalogStats = catalog != null
                ? new HashSet<UnitStatDefinition>(
                    catalog.CoreStats.Where(definition => definition != null))
                : new HashSet<UnitStatDefinition>();
            foreach (UnitDefinition unit in LoadAssets<UnitDefinition>())
            {
                HashSet<UnitStatDefinition> baseStats = new();
                foreach (UnitStatValue value in unit.BaseStatValues)
                {
                    if (value.Definition == null)
                        errors += LogError("기본 스탯 참조가 비어 있습니다.", unit);
                    else if (!baseStats.Add(value.Definition))
                        errors += LogError($"기본 스탯이 중복되었습니다: {value.Definition.DisplayName}", unit);
                    else if (!catalogStats.Contains(value.Definition))
                        errors += LogError($"기본 스탯이 핵심 카탈로그 밖을 참조합니다: {value.Definition.DisplayName}", unit);
                }
            }
            foreach (UnitTraitDefinition trait in LoadAssets<UnitTraitDefinition>())
            {
                if (trait.Icon == null)
                    errors += LogError("특성 아이콘이 비어 있습니다.", trait);
                foreach (UnitStatModifier modifier in trait.Modifiers)
                {
                    if (modifier.Definition == null)
                        errors += LogError("특성 스탯 수정자 참조가 비어 있습니다.", trait);
                    else if (!catalogStats.Contains(modifier.Definition))
                        errors += LogError($"특성 스탯이 핵심 카탈로그 밖을 참조합니다: {modifier.Definition.DisplayName}", trait);
                }
            }
            foreach (DashActionBehaviorDefinition dash in LoadAssets<DashActionBehaviorDefinition>())
            {
                if (dash.KnockbackCells < 1)
                    errors += LogError("돌진 밀치기 거리는 1칸 이상이어야 합니다.", dash);
            }
            return errors;
        }

        private static int ValidateItemsAndEquipment()
        {
            int errors = 0;
            UnitStatCatalog statCatalog = LoadFirstAsset<UnitStatCatalog>();
            HashSet<UnitStatDefinition> catalogStats = statCatalog != null
                ? new HashSet<UnitStatDefinition>(
                    statCatalog.CoreStats.Where(definition => definition != null))
                : new HashSet<UnitStatDefinition>();
            EquipmentLayoutDefinition layout = LoadFirstAsset<EquipmentLayoutDefinition>();
            if (layout == null)
                return LogError("전투 장비 레이아웃이 없습니다.", null);
            if (layout.Slots.Count != RequiredSlotKinds.Length)
                errors += LogError($"장비 슬롯은 정확히 9개여야 합니다. 현재 {layout.Slots.Count}개", layout);
            HashSet<string> slotIds = new();
            HashSet<EquipmentSlotKind> slotKinds = new();
            foreach (EquipmentSlotDefinition slot in layout.Slots)
            {
                if (slot == null)
                {
                    errors += LogError("장비 레이아웃에 빈 슬롯 참조가 있습니다.", layout);
                    continue;
                }
                if (!slotIds.Add(slot.SlotId))
                    errors += LogError($"장비 슬롯 ID가 중복되었습니다: {slot.SlotId}", slot);
                if (!slotKinds.Add(slot.SlotKind))
                    errors += LogError($"장비 슬롯 종류가 중복되었습니다: {slot.SlotKind}", slot);
            }
            foreach (EquipmentSlotKind requiredKind in RequiredSlotKinds)
                if (!slotKinds.Contains(requiredKind))
                    errors += LogError($"필수 장비 슬롯이 없습니다: {requiredKind}", layout);

            ItemCatalog itemCatalog = LoadFirstAsset<ItemCatalog>();
            if (itemCatalog == null)
                return errors + LogError("아이템 카탈로그가 없습니다.", null);
            HashSet<string> itemIds = new();
            foreach (ItemDefinition item in itemCatalog.Items)
            {
                if (item == null)
                {
                    errors += LogError("아이템 카탈로그에 빈 참조가 있습니다.", itemCatalog);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.EquipmentId))
                    errors += LogError("아이템 ID가 비어 있습니다.", item);
                else if (!itemIds.Add(item.EquipmentId))
                    errors += LogError($"아이템 ID가 중복되었습니다: {item.EquipmentId}", item);
                SerializedObject serializedItem = new(item);
                if (serializedItem.FindProperty("weight")?.floatValue < 0f)
                    errors += LogError($"아이템 무게가 음수입니다: {item.DisplayName}", item);
                if (serializedItem.FindProperty("maximumStack")?.intValue < 1)
                    errors += LogError($"아이템 최대 중첩량이 1보다 작습니다: {item.DisplayName}", item);
                if (ExpectedInitialItemWeights.TryGetValue(item.EquipmentId, out float expectedWeight)
                    && !Mathf.Approximately(item.Weight, expectedWeight))
                {
                    errors += LogError(
                        $"초기 아이템 무게가 기획값과 다릅니다: {item.DisplayName} {item.Weight:0.##}/{expectedWeight:0.##}kg",
                        item);
                }
                if (item.EquipmentId == "bandage" && item.MaximumStack <= 1)
                    errors += LogError("붕대는 수량 중첩 아이템이어야 합니다.", item);
                if (item.EquipmentId != "bandage" && item.MaximumStack != 1)
                    errors += LogError($"붕대 외 아이템은 개별 인스턴스여야 합니다: {item.DisplayName}", item);
                if (item is EquippableDefinition equipment && !CanEquipInAnyLayoutSlot(equipment, layout))
                    errors += LogError($"호환되는 슬롯이 없는 장비입니다: {equipment.DisplayName}", equipment);
                if (item is EquippableDefinition statEquipment)
                {
                    foreach (UnitStatModifier modifier in statEquipment.StatModifiers)
                    {
                        if (modifier.Definition == null)
                            errors += LogError($"장비 스탯 수정자 참조가 비어 있습니다: {item.DisplayName}", item);
                        else if (!catalogStats.Contains(modifier.Definition))
                            errors += LogError($"장비 스탯이 핵심 카탈로그 밖을 참조합니다: {item.DisplayName}", item);
                    }
                }
                if (item is ArmorDefinition armor && armor.Defense <= 0)
                    errors += LogError($"방어구 방어력은 1 이상이어야 합니다: {armor.DisplayName}", armor);
                if (item is WeaponDefinition weapon && weapon.AttackBehavior == null)
                    errors += LogError($"무기 공격 전략이 없습니다: {weapon.DisplayName}", weapon);
                foreach (ItemActionGrant grant in item.ActionGrants)
                    if (grant.Action == null)
                        errors += LogError($"아이템 행동 공급 참조가 비어 있습니다: {item.DisplayName}", item);
                errors += ValidateSampleItemConfiguration(item);
            }

            errors += ValidateUnitItemMigrations(layout);
            errors += ValidateUnitBasePrefab(layout);
            return errors;
        }

        private static int ValidateSampleItemConfiguration(ItemDefinition item)
        {
            int errors = 0;
            switch (item.EquipmentId)
            {
                case "rifle":
                    if (!HasActionGrant(item, "basic_attack", ItemActionAvailability.Equipped)
                        || !HasActionGrant(item, "grenade", ItemActionAvailability.Equipped))
                    {
                        errors += LogError("소총은 일반 발사와 무한 유탄 발사 행동을 공급해야 합니다.", item);
                    }
                    break;
                case "rapid_recovery_armor":
                    CombatActionDefinition recovery = FindGrantedAction(item, "armor_rapid_recovery");
                    if (recovery == null
                        || recovery.CooldownSeconds != 20f
                        || recovery.WindupSeconds != 1f
                        || recovery.Behavior is not RestoreHealthActionBehaviorDefinition recoveryBehavior
                        || recoveryBehavior.RestoreAmount != 25)
                    {
                        errors += LogError("급속회복 상의의 회복량·준비시간·재사용 대기시간이 올바르지 않습니다.", item);
                    }
                    break;
                case "ballistic_plate":
                    if (item is not AdditionalEquipmentDefinition plate
                        || plate.PassiveKind != SupportEquipmentPassiveKind.RegeneratingBallisticPlate
                        || plate.MaximumPassiveCharges != 3
                        || plate.PassiveRechargeSeconds != 10f)
                    {
                        errors += LogError("방탄판의 3회 차단·10초 재생 설정이 올바르지 않습니다.", item);
                    }
                    break;
                case "bandage":
                    CombatActionDefinition bandage = FindGrantedAction(item, "bandage_quick_use");
                    if (bandage == null
                        || bandage.WindupSeconds != 0.75f
                        || bandage.Behavior is not RestoreHealthActionBehaviorDefinition bandageBehavior
                        || bandageBehavior.RestoreAmount != 20
                        || !HasActionGrant(item, "bandage_quick_use", ItemActionAvailability.Carried))
                    {
                        errors += LogError("붕대의 수량 소모형 빠른 회복 설정이 올바르지 않습니다.", item);
                    }
                    break;
                case "frag_grenade":
                    if (!HasActionGrant(item, "grenade", ItemActionAvailability.Equipped))
                        errors += LogError("수류탄 보조장비가 수류탄 행동을 공급하지 않습니다.", item);
                    break;
                case "combat_stim":
                    if (!HasActionGrant(item, "stim", ItemActionAvailability.Equipped))
                        errors += LogError("자극제 보조장비가 자극제 행동을 공급하지 않습니다.", item);
                    break;
            }
            return errors;
        }

        private static bool HasActionGrant(
            ItemDefinition item,
            string actionId,
            ItemActionAvailability availability)
            => item.ActionGrants.Any(grant => grant.Action?.ActionId == actionId && grant.Availability == availability);

        private static CombatActionDefinition FindGrantedAction(ItemDefinition item, string actionId)
            => item.ActionGrants
                .Select(grant => grant.Action)
                .FirstOrDefault(action => action != null && action.ActionId == actionId);

        private static int ValidateUnitItemMigrations(EquipmentLayoutDefinition layout)
        {
            int errors = 0;
            CombatActionDefinition grenade = LoadAssets<CombatActionDefinition>().FirstOrDefault(action => action.ActionId == "grenade");
            CombatActionDefinition stim = LoadAssets<CombatActionDefinition>().FirstOrDefault(action => action.ActionId == "stim");
            CombatActionDefinition switchWeapon = LoadAssets<CombatActionDefinition>().FirstOrDefault(action => action.ActionId == "switch_weapon");
            if (switchWeapon != null)
                errors += LogError("제거된 무기 전환 행동 에셋이 남아 있습니다.", switchWeapon);
            if (grenade != null && grenade.StartingCharges >= 0)
                errors += LogError("수류탄 행동은 무한 사용이어야 합니다.", grenade);
            if (stim != null && stim.StartingCharges >= 0)
                errors += LogError("자극제 행동은 무한 사용이어야 합니다.", stim);
            foreach (UnitDefinition unit in LoadAssets<UnitDefinition>())
            {
                if (unit.ActionDefinitions.Contains(grenade)
                    || unit.ActionDefinitions.Contains(stim)
                    || unit.ActionDefinitions.Contains(switchWeapon))
                {
                    errors += LogError($"수류탄·자극제·무기 전환은 유닛 고유 행동에 남아 있으면 안 됩니다: {unit.DisplayName}", unit);
                }
                HashSet<EquipmentSlotDefinition> assignedSlots = new();
                foreach (EquipmentSlotAssignment assignment in unit.DefaultEquipmentAssignments)
                {
                    if (assignment.Slot == null)
                    {
                        errors += LogError("유닛 기본 장비 슬롯 참조가 비어 있습니다.", unit);
                        continue;
                    }
                    if (!layout.Slots.Contains(assignment.Slot))
                        errors += LogError($"유닛 장비가 현재 9슬롯 레이아웃 밖을 참조합니다: {unit.DisplayName}", unit);
                    if (!assignedSlots.Add(assignment.Slot))
                        errors += LogError($"유닛 기본 장비 슬롯이 중복되었습니다: {unit.DisplayName}", unit);
                    if (assignment.Equipment != null && !CanEquipInSlot(assignment.Equipment, assignment.Slot))
                        errors += LogError($"장비와 슬롯 규칙이 맞지 않습니다: {unit.DisplayName}", unit);
                }
                WeaponDefinition firstWeapon = unit.GetDefaultWeapon(0);
                WeaponDefinition secondWeapon = unit.GetDefaultWeapon(1);
                bool firstEquipped = firstWeapon == null || unit.DefaultEquipmentAssignments.Any(
                    assignment => assignment.Slot?.SlotKind == EquipmentSlotKind.LeftHand
                        && assignment.Equipment == firstWeapon);
                bool secondCarried = secondWeapon == null || unit.StartingInventoryItems.Any(
                    item => item.Definition == secondWeapon);
                if (!firstEquipped || !secondCarried)
                    errors += LogError($"기존 두 무기 마이그레이션이 완료되지 않았습니다: {unit.DisplayName}", unit);

                float totalWeight = unit.DefaultEquipmentAssignments.Sum(
                    assignment => assignment.Equipment != null ? assignment.Equipment.Weight : 0f);
                totalWeight += unit.StartingInventoryItems.Sum(
                    item => item.Definition != null ? item.Definition.Weight * item.Quantity : 0f);
                float capacity = 30f;
                UnitStatCatalog statCatalog = LoadFirstAsset<UnitStatCatalog>();
                if (statCatalog?.CarryCapacity != null)
                {
                    UnitStatValue custom = unit.BaseStatValues.FirstOrDefault(value => value.Definition == statCatalog.CarryCapacity);
                    capacity = custom.Definition != null ? custom.Value : statCatalog.CarryCapacity.DefaultValue;
                }
                if (totalWeight > capacity + 0.001f)
                    errors += LogError($"초기 소지 무게가 한도를 초과합니다: {unit.DisplayName} {totalWeight:0.##}/{capacity:0.##}kg", unit);
            }
            return errors;
        }

        private static int ValidateUnitBasePrefab(EquipmentLayoutDefinition layout)
        {
            const string path = "Assets/GridSquad/Prefabs/Units/UnitBase.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return LogError("UnitBase 프리팹이 없습니다.", null);
            int errors = 0;
            if (prefab.GetComponent<UnitInventory>() == null)
                errors += LogError("UnitBase 프리팹에 UnitInventory가 없습니다.", prefab);
            if (prefab.GetComponent<UnitItemInteractionController>() == null)
                errors += LogError("UnitBase 프리팹에 UnitItemInteractionController가 없습니다.", prefab);
            EquipmentLoadout loadout = prefab.GetComponent<EquipmentLoadout>();
            if (loadout == null || loadout.Layout != layout || loadout.Layout.Slots.Count != 9)
                errors += LogError("UnitBase 프리팹에 신규 9슬롯 장비 레이아웃이 연결되지 않았습니다.", prefab);
            return errors;
        }

        private static int ValidateMissingMonoBehaviours()
        {
            int errors = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/GridSquad" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
                if (missingCount > 0)
                    errors += LogError($"프리팹에 Missing MonoBehaviour가 있습니다: {path} ({missingCount}개)", prefab);
            }
            return errors;
        }

        private static int ValidateUiPrefabReferences()
        {
            int errors = 0;
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GridSquad/Resources/UI/SelectionUiRoot.prefab");
            if (root == null)
                return LogError("SelectionUiRoot 프리팹이 없습니다.", null);
            SelectionInspectController inspect = root.GetComponent<SelectionInspectController>();
            SelectionDetailWindowController detail = root.GetComponentInChildren<SelectionDetailWindowController>(true);
            ContextFloatingMenuController menu = root.GetComponentInChildren<ContextFloatingMenuController>(true);
            if (inspect == null || detail == null || menu == null)
                errors += LogError("선택 인포·상세창·플로팅 메뉴 프리팹 구성이 완전하지 않습니다.", root);
            errors += ValidateSerializedObjectReferences(inspect, new[]
            {
                "inspectRoot", "nameText", "healthText", "statusText", "actionContainer",
                "actionButtonPrefab", "statsButton", "equipmentInventoryButton", "traitsButton", "detailWindow"
            });
            errors += ValidateSerializedObjectReferences(detail, new[]
            {
                "windowRoot", "titleText", "summaryText", "weightText", "paperDollPanel",
                "inventoryListContent", "inventoryDropTarget", "worldDropTarget",
                "equipmentSlotPrefab", "inventoryItemPrefab", "equipmentInventoryTabButton", "floatingMenu"
            });
            errors += ValidateSerializedObjectReferences(menu, new[]
            {
                "menuRoot", "commandContainer", "commandButtonPrefab", "messageText", "overlayRoot", "overlayCanvas"
            });
            Canvas overlayCanvas = root.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(canvas => canvas.name == "FloatingUiOverlay");
            if (overlayCanvas == null
                || !overlayCanvas.overrideSorting
                || overlayCanvas.sortingOrder < 100
                || overlayCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                errors += LogError("플로팅 UI 오버레이 Canvas 정렬 또는 Raycaster 구성이 올바르지 않습니다.", root);
            }
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GridSquad/Prefabs/UI/EquipmentSlotView.prefab");
            if (slotPrefab == null || slotPrefab.GetComponent<CanvasGroup>() == null)
                errors += LogError("EquipmentSlotView 프리팹에 CanvasGroup이 없습니다.", slotPrefab);
            string[] actionButtonPaths =
            {
                "Assets/GridSquad/Prefabs/UI/CombatActionButton.prefab",
                "Assets/GridSquad/Resources/UI/CombatActionButton.prefab"
            };
            foreach (string actionButtonPath in actionButtonPaths)
            {
                GameObject actionButton = AssetDatabase.LoadAssetAtPath<GameObject>(actionButtonPath);
                CombatActionButtonView view = actionButton != null
                    ? actionButton.GetComponent<CombatActionButtonView>()
                    : null;
                RectTransform rect = actionButton != null
                    ? actionButton.transform as RectTransform
                    : null;
                if (view == null || rect == null || rect.sizeDelta != new Vector2(72f, 72f))
                    errors += LogError($"액션 버튼이 72x72 정사각형이 아닙니다: {actionButtonPath}", actionButton);
                if (view == null || view.CooldownFill == null || view.CooldownFill.sprite == null)
                    errors += LogError($"액션 버튼 Fill 스프라이트가 비어 있습니다: {actionButtonPath}", actionButton);
            }
            GameObject tooltipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GridSquad/Resources/UI/UiTooltip.prefab");
            if (tooltipPrefab == null || tooltipPrefab.GetComponent<UiTooltipPresenter>() == null)
                errors += LogError("공용 툴팁 프리팹 구성이 완전하지 않습니다.", tooltipPrefab);
            GameObject detailRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GridSquad/Resources/UI/UiTooltipRow.prefab");
            if (detailRowPrefab == null || detailRowPrefab.GetComponent<UiTooltipRowView>() == null)
                errors += LogError("상세 정보 행 프리팹 구성이 완전하지 않습니다.", detailRowPrefab);
            return errors;
        }

        private static int ValidateSerializedObjectReferences(Object target, IEnumerable<string> propertyNames)
        {
            if (target == null)
                return 1;
            int errors = 0;
            SerializedObject serialized = new(target);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                    errors += LogError($"프리팹 참조가 비어 있습니다: {target.GetType().Name}.{propertyName}", target);
            }
            return errors;
        }

        private static bool CanEquipInAnyLayoutSlot(
            EquippableDefinition equipment,
            EquipmentLayoutDefinition layout)
            => layout.Slots.Any(slot => slot != null && CanEquipInSlot(equipment, slot));

        private static bool CanEquipInSlot(EquippableDefinition equipment, EquipmentSlotDefinition slot)
        {
            if (equipment == null || slot == null)
                return false;
            return equipment switch
            {
                WeaponDefinition => slot.SlotKind == EquipmentSlotKind.LeftHand,
                OffHandDefinition => slot.SlotKind == EquipmentSlotKind.RightHand,
                ArmorDefinition armor => slot.SlotKind == armor.ArmorSlotKind,
                AdditionalEquipmentDefinition => slot.SlotKind is EquipmentSlotKind.SupportOne or EquipmentSlotKind.SupportTwo,
                _ => false
            };
        }

        private static IEnumerable<T> LoadAssets<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    yield return asset;
            }
        }

        private static T LoadFirstAsset<T>() where T : Object
        {
            foreach (T asset in LoadAssets<T>())
                return asset;
            return null;
        }

        private static int LogError(string message, Object context)
        {
            Debug.LogError($"[전투 데이터] {message}", context);
            return 1;
        }
    }
}
