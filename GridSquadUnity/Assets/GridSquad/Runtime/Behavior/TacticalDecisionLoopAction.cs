using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

[assembly: GeneratePropertyBagsForAssembly]

namespace GridSquad
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "전술 판단 루프",
        description: "플레이어 명령, 자동 엄폐 이동, 표적과 빼꼼 상태를 갱신합니다.",
        story: "[Agent]가 전술 판단을 반복합니다",
        category: "GridSquad",
        id: "3bc5e23ab50c4ab69a8e180c34fdde9e")]
    public partial class TacticalDecisionLoopAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<bool> AutonomousMovementAllowed;
        [SerializeReference] public BlackboardVariable<bool> AutomaticPeekAllowed;
        [SerializeReference] public BlackboardVariable<bool> MoveCommandPending;
        [SerializeReference] public BlackboardVariable<Vector3> MoveDestination;
        [SerializeReference] public BlackboardVariable<GameObject> PriorityTarget;
        [SerializeReference] public BlackboardVariable<GameObject> CurrentTarget;

        private UnitTacticalBehaviorController controller;

        protected override Status OnStart()
        {
            controller = Agent?.Value != null
                ? Agent.Value.GetComponent<UnitTacticalBehaviorController>()
                : null;
            return controller != null ? Status.Running : Status.Failure;
        }

        protected override Status OnUpdate()
        {
            if (controller == null)
                return Status.Failure;

            controller.TickTacticalDecisionFromBehavior(
                AutonomousMovementAllowed.Value,
                AutomaticPeekAllowed.Value,
                MoveCommandPending.Value,
                MoveDestination.Value,
                PriorityTarget.Value);
            MoveCommandPending.Value = controller.MoveCommandPending;
            PriorityTarget.Value = controller.PriorityTarget != null
                ? controller.PriorityTarget.gameObject
                : null;
            CurrentTarget.Value = controller.CurrentTarget != null
                ? controller.CurrentTarget.gameObject
                : null;
            return Status.Running;
        }
    }
}
