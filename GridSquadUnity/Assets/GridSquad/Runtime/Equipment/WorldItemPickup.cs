using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalEntity), typeof(SphereCollider))]
    public sealed class WorldItemPickup : MonoBehaviour, IContextCommandProvider
    {
        private static readonly Dictionary<GridCoordinate, List<WorldItemPickup>> PickupsByCell = new();

        [SerializeField] private ItemInstance item;
        private TacticalEntity tacticalEntity;
        private GridCoordinate cell;
        private bool registered;

        public ItemInstance Item => item;
        public GridCoordinate Cell => cell;
        public bool IsAvailable => item != null && item.Definition != null;

        private void Awake()
        {
            tacticalEntity = GetComponent<TacticalEntity>();
            SphereCollider selectionCollider = GetComponent<SphereCollider>();
            selectionCollider.isTrigger = true;
            selectionCollider.radius = 0.35f;
        }

        private void OnDestroy() => Unregister();

        public void Initialize(ItemInstance newItem, GridMap gridMap, GridCoordinate newCell)
        {
            item = newItem;
            cell = newCell;
            if (gridMap != null)
                transform.position = gridMap.GridToWorld(cell) + Vector3.up * 0.2f;
            tacticalEntity = tacticalEntity != null ? tacticalEntity : GetComponent<TacticalEntity>();
            tacticalEntity.ConfigureRuntime(
                item != null && item.Definition != null ? item.Definition.DisplayName : "월드 아이템",
                null,
                cell);
            Register();
        }

        public bool TryMoveToInventory(UnitInventory inventory, out string failureReason)
        {
            failureReason = string.Empty;
            if (!IsAvailable)
                return Fail("아이템이 이미 사라졌습니다.", out failureReason);
            if (inventory == null || !inventory.TryAdd(item, out failureReason))
                return false;
            item = null;
            tacticalEntity?.MarkUnavailable();
            Destroy(gameObject);
            return true;
        }

        public void CollectAvailableContextCommands(
            ContextCommandQuery query,
            List<ContextCommand> commands)
        {
            Combatant actor = query.SelectedCombatant;
            commands.Add(new ContextCommand(
                $"world-item.info.{item?.InstanceId}",
                $"정보: {(item?.Definition != null ? item.Definition.DisplayName : "아이템")}",
                item?.Definition?.Icon,
                0,
                IsAvailable,
                IsAvailable ? string.Empty : "아이템 정보가 없습니다.",
                () => query.Hud?.SetActionMessage(
                    item?.Definition != null
                        ? $"{item.Definition.DisplayName} · {item.TotalWeight:0.##}kg — {item.Definition.Description}"
                        : "아이템 정보가 없습니다.")));
            string reason = "먼저 살아있는 아군 유닛을 선택하세요.";
            bool canPickup = actor != null
                && actor.IsAlive
                && actor.Inventory != null
                && actor.Inventory.CanAccept(item, out reason);
            commands.Add(new ContextCommand(
                $"world-item.pickup.{item?.InstanceId}",
                $"줍기: {(item?.Definition != null ? item.Definition.DisplayName : "아이템")}",
                item?.Definition?.Icon,
                10,
                canPickup,
                canPickup ? string.Empty : reason,
                () =>
                {
                    if (actor?.ItemInteractionController == null)
                        return;
                    if (!actor.ItemInteractionController.QueuePickup(this, out string failureReason))
                        query.Hud?.SetActionMessage(failureReason);
                }));
        }

        public static IReadOnlyList<WorldItemPickup> GetItemsAt(GridCoordinate targetCell)
        {
            if (!PickupsByCell.TryGetValue(targetCell, out List<WorldItemPickup> pickups))
                return Array.Empty<WorldItemPickup>();
            pickups.RemoveAll(pickup => pickup == null || !pickup.IsAvailable);
            return pickups;
        }

        public static WorldItemPickup CreateDroppedItem(
            ItemInstance droppedItem,
            GridMap gridMap,
            GridCoordinate targetCell)
        {
            if (droppedItem == null || droppedItem.Definition == null)
                return null;
            GameObject root = new($"WorldItem_{droppedItem.Definition.DisplayName}");
            root.AddComponent<TacticalEntity>();
            root.AddComponent<SphereCollider>();
            GameObject visual = droppedItem.Definition.WorldPresentationPrefab != null
                ? Instantiate(droppedItem.Definition.WorldPresentationPrefab, root.transform, false)
                : CreateFallbackItemVisual(root.transform);
            visual.name = "ItemVisual";
            visual.transform.localPosition += Vector3.up * 0.16f;
            foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true))
                Destroy(visualCollider);
            if (droppedItem.Definition.WorldPresentationPrefab == null
                && visual.TryGetComponent(out Renderer renderer))
            {
                MaterialPropertyBlock properties = new();
                properties.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1f, 1f));
                properties.SetColor("_Color", new Color(0.2f, 0.8f, 1f, 1f));
                renderer.SetPropertyBlock(properties);
            }
            WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
            pickup.Initialize(droppedItem, gridMap, targetCell);
            return pickup;
        }

        private static GameObject CreateFallbackItemVisual(Transform parent)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * 0.28f;
            return visual;
        }

        private void Register()
        {
            if (registered)
                return;
            if (!PickupsByCell.TryGetValue(cell, out List<WorldItemPickup> pickups))
            {
                pickups = new List<WorldItemPickup>();
                PickupsByCell.Add(cell, pickups);
            }
            if (!pickups.Contains(this))
                pickups.Add(this);
            registered = true;
        }

        private void Unregister()
        {
            if (!registered)
                return;
            if (PickupsByCell.TryGetValue(cell, out List<WorldItemPickup> pickups))
            {
                pickups.Remove(this);
                if (pickups.Count == 0)
                    PickupsByCell.Remove(cell);
            }
            registered = false;
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }
    }

}
