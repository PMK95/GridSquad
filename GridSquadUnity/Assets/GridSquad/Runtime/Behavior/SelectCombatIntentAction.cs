using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

[assembly: GeneratePropertyBagsForAssembly]

namespace GridSquad
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "전투 행동 선택",
        description: "플레이어 명령을 우선 처리하고 Utility AI로 다음 전투 행동을 선택합니다.",
        story: "[Agent]가 다음 전투 행동을 선택합니다",
        category: "GridSquad",
        id: "76f5bc1b0a11452c94181796f412a1a1")]
    public partial class SelectCombatIntentAction : Unity.Behavior.Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

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
            return controller.TrySelectCombatIntentFromBehavior()
                ? Status.Success
                : Status.Running;
        }
    }
}
