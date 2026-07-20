using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace GridSquad
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "전투 실행 루프",
        description: "정지 여부, 조준 시간, 방향 정렬, 발사와 쿨다운을 처리합니다.",
        story: "[Agent]가 [CurrentTarget]과 전투를 반복합니다",
        category: "GridSquad",
        id: "a2c49bb8ce7c4a619d53e3a6ff6e7357")]
    public partial class CombatExecutionLoopAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
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

            controller.TickCombatExecutionFromBehavior(CurrentTarget.Value);
            return Status.Running;
        }
    }
}
