using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public enum MissionEndReason
    {
        Completed,
        Extracted,
        Defeated
    }

    public enum AftereffectSeverity
    {
        None,
        Minor,
        Major,
        Severe
    }

    [Serializable]
    public sealed class AftereffectState
    {
        [SerializeField] private string aftereffectId;
        [SerializeField, Min(1)] private int remainingRestMissions = 1;

        public AftereffectState(string aftereffectId, int remainingRestMissions)
        {
            this.aftereffectId = aftereffectId;
            this.remainingRestMissions = Mathf.Max(1, remainingRestMissions);
        }

        public string AftereffectId => aftereffectId;
        public int RemainingRestMissions => remainingRestMissions;

        public void KeepLongerDuration(int duration)
            => remainingRestMissions = Mathf.Max(remainingRestMissions, duration);

        public bool AdvanceRestMission()
        {
            remainingRestMissions = Mathf.Max(0, remainingRestMissions - 1);
            return remainingRestMissions == 0;
        }

        public AftereffectState CreateCopy()
            => new(aftereffectId, remainingRestMissions);
    }

    [Serializable]
    public sealed class ItemState
    {
        [SerializeField] private string itemInstanceId;
        [SerializeField] private string itemDefinitionId;
        [SerializeField, Min(0)] private int quantity;
        [SerializeField, Min(0)] private int durability;
        [SerializeField, Min(0)] private int magazineAmmo;
        [SerializeField, Min(0)] private int reserveAmmo;

        public ItemState(
            string itemInstanceId,
            string itemDefinitionId,
            int quantity,
            int durability,
            int magazineAmmo,
            int reserveAmmo)
        {
            this.itemInstanceId = itemInstanceId;
            this.itemDefinitionId = itemDefinitionId;
            this.quantity = Mathf.Max(0, quantity);
            this.durability = Mathf.Max(0, durability);
            this.magazineAmmo = Mathf.Max(0, magazineAmmo);
            this.reserveAmmo = Mathf.Max(0, reserveAmmo);
        }

        public string ItemInstanceId => itemInstanceId;
        public string ItemDefinitionId => itemDefinitionId;
        public int Quantity => quantity;
        public int Durability => durability;
        public int MagazineAmmo => magazineAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsDepleted => quantity <= 0;
        public bool IsBroken => durability <= 0;

        public bool TryConsume(int amount)
        {
            if (amount <= 0 || quantity < amount)
                return false;
            quantity -= amount;
            return true;
        }

        public int ApplyWear(int amount)
        {
            int previous = durability;
            durability = Mathf.Max(0, durability - Mathf.Max(0, amount));
            return previous - durability;
        }

        public void RepairTo(int maximumDurability)
            => durability = Mathf.Max(0, maximumDurability);

        public void SetAmmunition(int newMagazineAmmo, int newReserveAmmo)
        {
            magazineAmmo = Mathf.Max(0, newMagazineAmmo);
            reserveAmmo = Mathf.Max(0, newReserveAmmo);
        }

        public ItemState CreateCopy()
            => new(
                itemInstanceId,
                itemDefinitionId,
                quantity,
                durability,
                magazineAmmo,
                reserveAmmo);
    }

    [Serializable]
    public sealed class EquipmentSlotState
    {
        [SerializeField] private string slotId;
        [SerializeField] private string itemInstanceId;

        public EquipmentSlotState(string slotId, string itemInstanceId)
        {
            this.slotId = slotId;
            this.itemInstanceId = itemInstanceId;
        }

        public string SlotId => slotId;
        public string ItemInstanceId => itemInstanceId;

        public EquipmentSlotState CreateCopy()
            => new(slotId, itemInstanceId);
    }

    [Serializable]
    public sealed class UnitState
    {
        [SerializeField] private string unitInstanceId;
        [SerializeField] private string unitDefinitionId;
        [SerializeField] private List<ItemState> items = new();
        [SerializeField] private List<EquipmentSlotState> equipment = new();
        [SerializeField] private List<AftereffectState> aftereffects = new();

        public UnitState(string unitInstanceId, string unitDefinitionId)
        {
            this.unitInstanceId = unitInstanceId;
            this.unitDefinitionId = unitDefinitionId;
        }

        public string UnitInstanceId => unitInstanceId;
        public string UnitDefinitionId => unitDefinitionId;
        public IReadOnlyList<ItemState> Items => items;
        public IReadOnlyList<EquipmentSlotState> Equipment => equipment;
        public IReadOnlyList<AftereffectState> Aftereffects => aftereffects;

        public void AddItem(ItemState item)
        {
            if (item != null)
                items.Add(item);
        }

        public void AddEquipment(EquipmentSlotState slot)
        {
            if (slot != null)
                equipment.Add(slot);
        }

        public ItemState FindItem(string itemInstanceId)
            => items.Find(item => item.ItemInstanceId == itemInstanceId);

        public void ReplaceMissionItems(
            IReadOnlyList<ItemState> missionItems,
            IReadOnlyList<EquipmentSlotState> missionEquipment)
        {
            items.Clear();
            if (missionItems != null)
            {
                foreach (ItemState item in missionItems)
                    if (item != null && !item.IsDepleted)
                        items.Add(item.CreateCopy());
            }

            equipment.Clear();
            if (missionEquipment == null)
                return;
            foreach (EquipmentSlotState slot in missionEquipment)
            {
                if (slot != null && FindItem(slot.ItemInstanceId) != null)
                    equipment.Add(slot.CreateCopy());
            }
        }

        public void AdvanceRestMission()
        {
            for (int index = aftereffects.Count - 1; index >= 0; index--)
                if (aftereffects[index].AdvanceRestMission())
                    aftereffects.RemoveAt(index);
        }

        public void AddOrExtendAftereffect(string aftereffectId, int duration)
        {
            AftereffectState existing = aftereffects.Find(
                state => state.AftereffectId == aftereffectId);
            if (existing != null)
            {
                existing.KeepLongerDuration(duration);
                return;
            }
            aftereffects.Add(new AftereffectState(aftereffectId, duration));
        }

        public UnitState CreateCopy()
        {
            UnitState copy = new(unitInstanceId, unitDefinitionId);
            foreach (ItemState item in items)
                copy.items.Add(item.CreateCopy());
            foreach (EquipmentSlotState slot in equipment)
                copy.equipment.Add(slot.CreateCopy());
            foreach (AftereffectState aftereffect in aftereffects)
                copy.aftereffects.Add(aftereffect.CreateCopy());
            return copy;
        }
    }

    [Serializable]
    public sealed class BaseState
    {
        [SerializeField] private List<UnitState> units = new();

        public IReadOnlyList<UnitState> Units => units;

        public void AddUnit(UnitState unit)
        {
            if (unit != null)
                units.Add(unit);
        }

        public UnitState FindUnit(string unitInstanceId)
            => units.Find(unit => unit.UnitInstanceId == unitInstanceId);
    }

    [Serializable]
    public sealed class MissionUnitState
    {
        [SerializeField] private string unitInstanceId;
        [SerializeField] private string unitDefinitionId;
        [SerializeField, Min(1)] private int maximumHealthAtLaunch;
        [SerializeField, Min(0)] private int currentHealth;
        [SerializeField] private float traumaResistancePercent;
        [SerializeField, Min(0f)] private float accumulatedTrauma;
        [SerializeField] private bool incapacitated;
        [SerializeField] private List<ItemState> items = new();
        [SerializeField] private List<EquipmentSlotState> equipment = new();

        public MissionUnitState(
            UnitState source,
            int maximumHealthAtLaunch,
            float traumaResistancePercent)
        {
            unitInstanceId = source.UnitInstanceId;
            unitDefinitionId = source.UnitDefinitionId;
            this.maximumHealthAtLaunch = Mathf.Max(1, maximumHealthAtLaunch);
            currentHealth = this.maximumHealthAtLaunch;
            this.traumaResistancePercent = traumaResistancePercent;
            foreach (ItemState item in source.Items)
                items.Add(item.CreateCopy());
            foreach (EquipmentSlotState slot in source.Equipment)
                equipment.Add(slot.CreateCopy());
        }

        public string UnitInstanceId => unitInstanceId;
        public string UnitDefinitionId => unitDefinitionId;
        public int MaximumHealthAtLaunch => maximumHealthAtLaunch;
        public int CurrentHealth => currentHealth;
        public float TraumaResistancePercent => traumaResistancePercent;
        public float AccumulatedTrauma => accumulatedTrauma;
        public bool IsIncapacitated => incapacitated;
        public IReadOnlyList<ItemState> Items => items;
        public IReadOnlyList<EquipmentSlotState> Equipment => equipment;

        public ItemState FindItem(string itemInstanceId)
            => items.Find(item => item.ItemInstanceId == itemInstanceId);

        public void ApplyCombatResult(int resultingHealth, float gainedTrauma)
        {
            currentHealth = Mathf.Clamp(resultingHealth, 0, maximumHealthAtLaunch);
            accumulatedTrauma += Mathf.Max(0f, gainedTrauma);
            incapacitated |= currentHealth == 0;
        }

        public void SetIncapacitated()
        {
            currentHealth = 0;
            incapacitated = true;
        }

        public void ReplaceItems(
            IReadOnlyList<ItemState> newItems,
            IReadOnlyList<EquipmentSlotState> newEquipment)
        {
            items.Clear();
            if (newItems != null)
            {
                foreach (ItemState item in newItems)
                    if (item != null && !item.IsDepleted)
                        items.Add(item.CreateCopy());
            }

            equipment.Clear();
            if (newEquipment != null)
            {
                foreach (EquipmentSlotState slot in newEquipment)
                    if (slot != null && FindItem(slot.ItemInstanceId) != null)
                        equipment.Add(slot.CreateCopy());
            }
        }
    }

    [Serializable]
    public sealed class ActiveMissionState
    {
        [SerializeField] private string missionRunId;
        [SerializeField] private string missionId;
        [SerializeField, Min(0)] private int nextStageIndex;
        [SerializeField] private int randomSeed;
        [SerializeField] private List<MissionUnitState> deployedUnits = new();

        public ActiveMissionState(string missionId, int randomSeed)
        {
            missionRunId = Guid.NewGuid().ToString("N");
            this.missionId = missionId;
            this.randomSeed = randomSeed;
        }

        public string MissionRunId => missionRunId;
        public string MissionId => missionId;
        public int NextStageIndex => nextStageIndex;
        public int RandomSeed => randomSeed;
        public IReadOnlyList<MissionUnitState> DeployedUnits => deployedUnits;
        public bool HasCombatReadyUnit
            => deployedUnits.Exists(unit => !unit.IsIncapacitated);

        public void AddDeployedUnit(MissionUnitState unit)
        {
            if (unit != null)
                deployedUnits.Add(unit);
        }

        public MissionUnitState FindUnit(string unitInstanceId)
            => deployedUnits.Find(unit => unit.UnitInstanceId == unitInstanceId);

        public void AdvanceStage()
            => nextStageIndex++;
    }

    [Serializable]
    public sealed class GameSessionState
    {
        [SerializeField, Min(1)] private int dataVersion = 1;
        [SerializeField] private string sessionId;
        [SerializeField] private BaseState baseState = new();
        [SerializeField] private ActiveMissionState activeMission;

        public GameSessionState()
        {
            sessionId = Guid.NewGuid().ToString("N");
        }

        public GameSessionState(BaseState initialBaseState)
        {
            sessionId = Guid.NewGuid().ToString("N");
            baseState = initialBaseState ?? throw new ArgumentNullException(nameof(initialBaseState));
        }

        public int DataVersion => dataVersion;
        public string SessionId => sessionId;
        public BaseState BaseState => baseState;
        public ActiveMissionState ActiveMission => activeMission;

        public void StartMission(ActiveMissionState mission)
        {
            if (activeMission != null)
                throw new InvalidOperationException("이미 진행 중인 임무가 있습니다.");
            activeMission = mission ?? throw new ArgumentNullException(nameof(mission));
        }

        public void FinishMission()
            => activeMission = null;
    }
}
