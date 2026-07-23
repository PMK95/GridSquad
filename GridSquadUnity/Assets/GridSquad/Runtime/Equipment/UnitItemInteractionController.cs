using System;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitInventory))]
    public sealed class UnitItemInteractionController : MonoBehaviour
    {
        private Combatant owner;
        private UnitInventory inventory;
        private WorldItemPickup pendingPickup;
        private CombatHudController hud;
        private bool runtimeInitialized;

        public event Action<string> CommandMessage;
        public WorldItemPickup PendingPickup => pendingPickup;

        private void Awake()
        {
            owner = GetComponent<Combatant>();
            inventory = GetComponent<UnitInventory>();
        }

        public void InitializeRuntime(CombatHudController newHud)
        {
            owner = owner != null ? owner : GetComponent<Combatant>();
            inventory = inventory != null ? inventory : GetComponent<UnitInventory>();
            hud = newHud;
            runtimeInitialized = true;
        }

        private void Update()
        {
            if (!runtimeInitialized || pendingPickup == null)
                return;
            if (!pendingPickup.IsAvailable)
            {
                CompleteWithMessage("아이템이 사라져 줍기 명령을 취소했습니다.");
                return;
            }
            if (owner == null || !owner.IsAlive)
            {
                CompleteWithMessage("아이템을 주울 수 있는 유닛이 없습니다.");
                return;
            }
            if (owner.CurrentCell != pendingPickup.Cell || owner.IsMoving)
                return;

            WorldItemPickup target = pendingPickup;
            pendingPickup = null;
            if (target.TryMoveToInventory(inventory, out string failureReason))
                PublishCommandMessage($"{target.name} 습득 완료");
            else
                PublishCommandMessage(failureReason);
        }

        public bool QueuePickup(WorldItemPickup pickup, out string failureReason)
        {
            failureReason = string.Empty;
            if (pickup == null || !pickup.IsAvailable)
                return Fail("주울 아이템이 없습니다.", out failureReason);
            if (owner == null || !owner.IsAlive)
                return Fail("아이템을 주울 수 있는 유닛이 없습니다.", out failureReason);
            if (inventory == null || !inventory.CanAccept(pickup.Item, out failureReason))
                return false;

            GetComponent<UnitTacticalBehaviorController>()
                ?.InterruptSelectedCombatIntent(CombatActionInterruptReason.PlayerCommand);
            pendingPickup = pickup;
            if (owner.CurrentCell != pickup.Cell && !owner.SetMoveDestination(pickup.Cell))
            {
                pendingPickup = null;
                return Fail("아이템이 있는 칸으로 이동할 수 없습니다.", out failureReason);
            }
            failureReason = string.Empty;
            return true;
        }

        private void CompleteWithMessage(string message)
        {
            pendingPickup = null;
            PublishCommandMessage(message);
        }

        private void PublishCommandMessage(string message)
        {
            CommandMessage?.Invoke(message);
            hud?.SetActionMessage(message);
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }
    }
}
