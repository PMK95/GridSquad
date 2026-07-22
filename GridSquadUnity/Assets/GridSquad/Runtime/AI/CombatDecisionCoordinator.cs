using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatCommandState), typeof(CombatActionController))]
    public sealed class CombatDecisionCoordinator : MonoBehaviour
    {
        [SerializeField] private Combatant combatant;
        [SerializeField] private CombatDirector director;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private CombatCommandState commandState;
        [SerializeField] private CombatActionController actionController;

        private CombatActionIntent selectedIntent;
        private bool hasSelectedIntent;
        private int selectedCommandRevision;
        private float nextEvaluationTime;

        public bool HasSelectedIntent => hasSelectedIntent;

        public void ConfigureRuntime(
            Combatant newCombatant,
            CombatDirector newDirector,
            CombatTuning newTuning,
            CombatCommandState newCommandState,
            CombatActionController newActionController)
        {
            combatant = newCombatant;
            director = newDirector;
            tuning = newTuning;
            commandState = newCommandState;
            actionController = newActionController;
        }

        public bool TrySelectNextIntent()
        {
            actionController.TickCooldowns(Time.deltaTime);
            if (!CanRunCombat())
                return false;
            if (hasSelectedIntent || actionController.HasRunningIntent)
                return hasSelectedIntent;

            commandState.ClearInvalidPriorityTarget(combatant);
            if (combatant.IsHitReacting)
                return false;
            if (combatant.IsStunned)
                return false;
            if (combatant.IsReloading)
            {
                combatant.TickAutomaticFireCycleFromBehavior();
                return false;
            }

            if (commandState.TryTakeAction(out CombatActionCommand command))
            {
                SelectIntent(new CombatActionIntent(
                    command.Candidate,
                    CombatActionSelectionSource.Player));
                return true;
            }

            if (commandState.TryPeekMove(out GridCoordinate destination))
            {
                if (actionController.TryCreatePlayerMovementIntent(destination, out CombatActionIntent moveIntent))
                {
                    SelectIntent(moveIntent);
                    return true;
                }
                commandState.CompletePendingMove();
                return false;
            }

            if (Time.time < nextEvaluationTime)
                return false;

            CombatControlPolicy policy = CombatControlPolicy.Create(
                combatant.Team,
                commandState.ControlMode);
            bool selected = policy.AllowsAutomaticAbilities
                ? actionController.TrySelectAutomaticIntent(
                    policy.Mode,
                    commandState.PriorityTarget,
                    commandState.AutomaticPeekAllowed,
                    out selectedIntent)
                : actionController.TrySelectManualBasicAttackIntent(
                    commandState.PriorityTarget,
                    commandState.AutomaticPeekAllowed,
                    out selectedIntent);
            nextEvaluationTime = Time.time + GetEvaluationInterval(policy);
            if (!selected)
                return false;
            hasSelectedIntent = true;
            selectedCommandRevision = commandState.Revision;
            return true;
        }

        public bool TryBeginSelectedIntent(out string failureReason)
        {
            if (!hasSelectedIntent)
            {
                failureReason = "선택된 전투 행동이 없습니다.";
                return false;
            }
            if (!actionController.TryBeginIntent(selectedIntent, out failureReason))
            {
                hasSelectedIntent = false;
                if (IsPlayerMovementCommand(selectedIntent)
                    && selectedIntent.Source == CombatActionSelectionSource.Player)
                {
                    commandState.CompletePendingMove();
                }
                return false;
            }
            if (IsPlayerMovementCommand(selectedIntent)
                && selectedIntent.Source == CombatActionSelectionSource.Player)
            {
                commandState.CompletePendingMove();
            }
            return true;
        }

        public CombatActionExecutionStatus TickSelectedIntent()
        {
            if (!hasSelectedIntent)
                return CombatActionExecutionStatus.Idle;
            if (ShouldInterruptRunningIntent())
            {
                actionController.InterruptCurrentIntent(GetInterruptReason());
                hasSelectedIntent = false;
                return CombatActionExecutionStatus.Interrupted;
            }

            CombatActionExecutionStatus status = actionController.TickCurrentIntent(Time.deltaTime);
            if (status != CombatActionExecutionStatus.Running)
                hasSelectedIntent = false;
            return status;
        }

        public void InterruptCurrentIntent(CombatActionInterruptReason reason)
        {
            actionController?.InterruptCurrentIntent(reason);
            hasSelectedIntent = false;
        }

        public void ResetForBattle()
        {
            hasSelectedIntent = false;
            nextEvaluationTime = 0f;
        }

        private void SelectIntent(CombatActionIntent intent)
        {
            selectedIntent = intent;
            hasSelectedIntent = true;
            selectedCommandRevision = commandState.Revision;
        }

        private bool ShouldInterruptRunningIntent()
        {
            if (!CanRunCombat())
                return true;
            if (combatant.IsStunned)
                return true;
            if (selectedIntent.Candidate.Target != null
                && !selectedIntent.Candidate.Target.IsAlive)
            {
                return true;
            }
            if (selectedIntent.Source == CombatActionSelectionSource.Automatic
                && commandState.Revision != selectedCommandRevision)
            {
                return true;
            }
            return selectedIntent.Candidate.Definition != null
                && selectedIntent.Candidate.Definition.HasCapability(
                    CombatActionCapabilityFlags.ChangesPosition)
                && commandState.Revision != selectedCommandRevision;
        }

        private static bool IsPlayerMovementCommand(CombatActionIntent intent)
        {
            return intent.Candidate.Definition != null
                && intent.Candidate.Definition.HasCapability(
                    CombatActionCapabilityFlags.MovementCommand);
        }

        private CombatActionInterruptReason GetInterruptReason()
        {
            if (combatant == null || !combatant.IsAlive)
                return CombatActionInterruptReason.OwnerDied;
            if (combatant.IsStunned)
                return CombatActionInterruptReason.Stunned;
            if (director == null || !director.BattleStarted || director.BattleFinished)
                return CombatActionInterruptReason.CombatEnded;
            if (selectedIntent.Candidate.Target != null
                && !selectedIntent.Candidate.Target.IsAlive)
            {
                return CombatActionInterruptReason.TargetInvalid;
            }
            return CombatActionInterruptReason.PlayerCommand;
        }

        private bool CanRunCombat()
        {
            return combatant != null
                && combatant.IsAlive
                && director != null
                && director.BattleStarted
                && !director.BattleFinished;
        }

        private float GetEvaluationInterval(CombatControlPolicy policy)
        {
            if (tuning == null)
                return 0.2f;
            return policy.AllowsAutomaticMovement
                ? tuning.AiEvaluationInterval
                : tuning.EvaluationRefreshInterval;
        }
    }
}
