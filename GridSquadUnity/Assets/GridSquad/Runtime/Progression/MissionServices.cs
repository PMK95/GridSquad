using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class MissionStateFactory
    {
        public ActiveMissionState Create(
            BaseState baseState,
            MissionDefinition mission,
            IReadOnlyList<string> selectedUnitIds,
            Func<UnitState, int> getMaximumHealth,
            Func<UnitState, float> getTraumaResistance,
            int randomSeed)
        {
            if (baseState == null)
                throw new ArgumentNullException(nameof(baseState));
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));
            if (selectedUnitIds == null
                || selectedUnitIds.Count < mission.MinimumSquadSize
                || selectedUnitIds.Count > mission.MaximumSquadSize)
            {
                throw new InvalidOperationException(
                    $"선발 인원은 {mission.MinimumSquadSize}~{mission.MaximumSquadSize}명이어야 합니다.");
            }

            ActiveMissionState state = new(mission.MissionId, randomSeed);
            HashSet<string> uniqueIds = new();
            foreach (string unitId in selectedUnitIds)
            {
                if (!uniqueIds.Add(unitId))
                    throw new InvalidOperationException($"선발 대원이 중복되었습니다: {unitId}");
                UnitState unit = baseState.FindUnit(unitId)
                    ?? throw new InvalidOperationException($"선발 대원을 찾을 수 없습니다: {unitId}");
                state.AddDeployedUnit(new MissionUnitState(
                    unit,
                    getMaximumHealth(unit),
                    getTraumaResistance(unit)));
            }
            return state;
        }
    }

    public static class TraumaCalculator
    {
        public static float Calculate(
            int appliedHealthDamage,
            float traumaMultiplier,
            float fixedTrauma)
        {
            return Mathf.Max(
                0f,
                Mathf.Max(0, appliedHealthDamage) * Mathf.Max(0f, traumaMultiplier)
                + Mathf.Max(0f, fixedTrauma));
        }
    }

    public sealed class AftereffectSelectionService
    {
        private readonly List<AftereffectDefinition> candidates = new();

        public AftereffectSeverity CalculateSeverity(
            MissionUnitState unit,
            AftereffectRuleSet rules)
        {
            if (unit == null || rules == null)
                return AftereffectSeverity.None;
            if (unit.IsIncapacitated)
                return AftereffectSeverity.Severe;

            float resistanceMultiplier = Mathf.Max(
                0.1f,
                1f + unit.TraumaResistancePercent / 100f);
            float thresholdBase = unit.MaximumHealthAtLaunch * resistanceMultiplier;
            if (unit.AccumulatedTrauma >= thresholdBase * rules.SevereThresholdRatio)
                return AftereffectSeverity.Severe;
            if (unit.AccumulatedTrauma >= thresholdBase * rules.MajorThresholdRatio)
                return AftereffectSeverity.Major;
            if (unit.AccumulatedTrauma >= thresholdBase * rules.MinorThresholdRatio)
                return AftereffectSeverity.Minor;
            return AftereffectSeverity.None;
        }

        public AftereffectDefinition Select(
            MissionUnitState unit,
            AftereffectRuleSet rules,
            string missionRunId)
        {
            AftereffectSeverity severity = CalculateSeverity(unit, rules);
            if (severity == AftereffectSeverity.None)
                return null;

            candidates.Clear();
            foreach (AftereffectDefinition definition in rules.Definitions)
                if (definition != null && definition.Severity == severity)
                    candidates.Add(definition);
            if (candidates.Count == 0)
                throw new InvalidOperationException($"{severity} 단계 후유증 정의가 없습니다.");

            uint hash = CalculateStableHash($"{missionRunId}:{unit.UnitInstanceId}:{severity}");
            return candidates[(int)(hash % candidates.Count)];
        }

        private static uint CalculateStableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }
            return hash;
        }
    }

    public readonly struct UnitMissionSettlement
    {
        public readonly string UnitInstanceId;
        public readonly float AccumulatedTrauma;
        public readonly bool WasIncapacitated;
        public readonly AftereffectDefinition AppliedAftereffect;

        public UnitMissionSettlement(
            string unitInstanceId,
            float accumulatedTrauma,
            bool wasIncapacitated,
            AftereffectDefinition appliedAftereffect)
        {
            UnitInstanceId = unitInstanceId;
            AccumulatedTrauma = accumulatedTrauma;
            WasIncapacitated = wasIncapacitated;
            AppliedAftereffect = appliedAftereffect;
        }
    }

    public sealed class MissionSettlement
    {
        private readonly List<UnitMissionSettlement> units = new();

        public MissionSettlement(MissionEndReason endReason)
        {
            EndReason = endReason;
        }

        public MissionEndReason EndReason { get; }
        public IReadOnlyList<UnitMissionSettlement> Units => units;

        public void Add(UnitMissionSettlement settlement)
            => units.Add(settlement);
    }

    public sealed class MissionSettlementService
    {
        private readonly AftereffectSelectionService aftereffectSelection = new();

        public MissionSettlement Apply(
            BaseState baseState,
            ActiveMissionState mission,
            MissionEndReason endReason,
            AftereffectRuleSet rules)
        {
            if (baseState == null || mission == null)
                throw new ArgumentNullException(baseState == null ? nameof(baseState) : nameof(mission));

            HashSet<string> deployedIds = new();
            foreach (MissionUnitState missionUnit in mission.DeployedUnits)
                deployedIds.Add(missionUnit.UnitInstanceId);

            foreach (UnitState baseUnit in baseState.Units)
                if (!deployedIds.Contains(baseUnit.UnitInstanceId))
                    baseUnit.AdvanceRestMission();

            MissionSettlement settlement = new(endReason);
            foreach (MissionUnitState missionUnit in mission.DeployedUnits)
            {
                UnitState baseUnit = baseState.FindUnit(missionUnit.UnitInstanceId)
                    ?? throw new InvalidOperationException(
                        $"복귀 대원을 기지 명단에서 찾을 수 없습니다: {missionUnit.UnitInstanceId}");
                baseUnit.ReplaceMissionItems(missionUnit.Items, missionUnit.Equipment);
                AftereffectDefinition aftereffect = aftereffectSelection.Select(
                    missionUnit,
                    rules,
                    mission.MissionRunId);
                if (aftereffect != null)
                {
                    baseUnit.AddOrExtendAftereffect(
                        aftereffect.AftereffectId,
                        aftereffect.RestMissions);
                }
                settlement.Add(new UnitMissionSettlement(
                    missionUnit.UnitInstanceId,
                    missionUnit.AccumulatedTrauma,
                    missionUnit.IsIncapacitated,
                    aftereffect));
            }
            return settlement;
        }
    }

    public sealed class EquipmentRepairService
    {
        public bool TryRepair(
            BaseState baseState,
            string unitInstanceId,
            string itemInstanceId,
            GameContentCatalog catalog,
            out string failureReason)
        {
            UnitState unit = baseState?.FindUnit(unitInstanceId);
            ItemState item = unit?.FindItem(itemInstanceId);
            if (item == null)
                return Fail("수리할 장비를 찾을 수 없습니다.", out failureReason);
            if (catalog.GetRequiredItem(item.ItemDefinitionId) is not EquippableDefinition equipment)
                return Fail("수리할 수 없는 아이템입니다.", out failureReason);
            item.RepairTo(equipment.MaximumDurability);
            failureReason = string.Empty;
            return true;
        }

        private static bool Fail(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }
}
