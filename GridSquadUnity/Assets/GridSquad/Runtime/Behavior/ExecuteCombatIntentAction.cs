using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace GridSquad
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "전투 행동 실행",
        description: "선택된 전투 행동을 실행하고 완료·실패·중단 상태를 BT에 반환합니다.",
        story: "[Agent]가 선택된 전투 행동을 실행합니다",
        category: "GridSquad",
        id: "bbf839b3270a45d787475de3399b7d16")]
    public partial class ExecuteCombatIntentAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        private UnitTacticalBehaviorController controller;

        protected override Status OnStart()
        {
            controller = Agent?.Value != null
                ? Agent.Value.GetComponent<UnitTacticalBehaviorController>()
                : null;
            if (controller == null)
                return Status.Failure;
            if (!controller.TryBeginSelectedCombatIntentFromBehavior(out string failureReason))
            {
                if (!string.IsNullOrEmpty(failureReason))
                    Debug.LogWarning($"[전투 행동] {Agent.Value.name}: {failureReason}");
                return Status.Failure;
            }
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (controller == null)
                return Status.Failure;
            CombatActionExecutionStatus status = controller.TickSelectedCombatIntentFromBehavior();
            return status switch
            {
                CombatActionExecutionStatus.Running => Status.Running,
                CombatActionExecutionStatus.Completed => Status.Success,
                CombatActionExecutionStatus.Interrupted => Status.Success,
                _ => Status.Failure
            };
        }
    }
}
