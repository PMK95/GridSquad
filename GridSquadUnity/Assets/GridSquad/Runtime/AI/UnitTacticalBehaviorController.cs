using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

namespace GridSquad
{
    public sealed class UnitTacticalBehaviorController : MonoBehaviour
    {
        private enum MovementIntent
        {
            None,
            PlayerCommand,
            Autonomous
        }

        [SerializeField] private Combatant combatant;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private TacticalPositionEvaluator positionEvaluator;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private BehaviorGraphAgent behaviorAgent;
        [SerializeField] private bool autonomousMovementDefault;
        [SerializeField] private bool automaticPeekDefault = true;

        private readonly List<TacticalCellChoice> debugChoices = new();
        private MovementIntent movementIntent;
        private bool autonomousMovementAllowed;
        private bool automaticPeekAllowed;
        private bool moveCommandPending;
        private Vector3 moveDestinationWorld;
        private Combatant priorityTarget;
        private float nextTacticalEvaluationTime;
        private float nextMovementTime;
        private float nextShotEvaluationTime;

        public bool AutonomousMovementAllowed => autonomousMovementAllowed;
        public bool AutomaticPeekAllowed => automaticPeekAllowed;
        public bool MoveCommandPending => moveCommandPending;
        public Vector3 MoveDestinationWorld => moveDestinationWorld;
        public Combatant PriorityTarget => priorityTarget;
        public Combatant CurrentTarget => combatant != null ? combatant.CurrentTarget : null;
        public BehaviorGraphAgent BehaviorAgent => behaviorAgent;

        private void OnEnable()
        {
            if (combatant != null)
                combatant.Died += HandleCombatantDied;
        }

        private void Start()
        {
            SetAutonomousMovementAllowed(autonomousMovementDefault);
            SetAutomaticPeekAllowed(automaticPeekDefault);
            WriteBlackboardValue("MoveCommandPending", false);
            WriteBlackboardValue<GameObject>("PriorityTarget", null);
            WriteBlackboardValue<GameObject>("CurrentTarget", null);
        }

        private void OnDisable()
        {
            if (combatant != null)
                combatant.Died -= HandleCombatantDied;
        }

        public void QueueMoveCommand(GridCoordinate destination)
        {
            if (combatant == null || !combatant.IsAlive || gridMap == null)
                return;

            moveDestinationWorld = gridMap.GridToWorld(destination);
            moveCommandPending = true;
            WriteBlackboardValue("MoveDestination", moveDestinationWorld);
            WriteBlackboardValue("MoveCommandPending", true);
        }

        public void SetPriorityTargetCommand(Combatant target)
        {
            priorityTarget = IsValidEnemy(target) ? target : null;
            combatant.SetBehaviorTarget(priorityTarget);
            WriteBlackboardValue(
                "PriorityTarget",
                priorityTarget != null ? priorityTarget.gameObject : null);
        }

        public void SetAutomaticPeekAllowed(bool allowed)
        {
            automaticPeekAllowed = allowed;
            WriteBlackboardValue("AutomaticPeekAllowed", allowed);
            if (!allowed)
                combatant.SetPeekEnabled(false);
            nextTacticalEvaluationTime = 0f;
        }

        public void SetAutonomousMovementAllowed(bool allowed)
        {
            autonomousMovementAllowed = allowed;
            WriteBlackboardValue("AutonomousMovementAllowed", allowed);
            if (!allowed)
                CancelAutonomousMovement();
            nextTacticalEvaluationTime = 0f;
        }

        public void CancelAutonomousMovement()
        {
            if (movementIntent != MovementIntent.Autonomous)
                return;

            if (combatant.IsMoving)
                combatant.StopMovementAtCurrentCell();
            movementIntent = MovementIntent.None;
        }

        public void StopBehaviorForDeath()
        {
            behaviorAgent?.End();
            debugChoices.Clear();
            moveCommandPending = false;
            movementIntent = MovementIntent.None;
        }

        public void TickTacticalDecisionFromBehavior(
            bool requestedAutonomousMovement,
            bool requestedAutomaticPeek,
            bool requestedMovePending,
            Vector3 requestedMoveDestination,
            GameObject requestedPriorityTarget)
        {
            if (combatant == null || !combatant.IsAlive || director == null || director.BattleFinished)
                return;

            autonomousMovementAllowed = requestedAutonomousMovement;
            automaticPeekAllowed = requestedAutomaticPeek;
            moveCommandPending |= requestedMovePending;
            if (requestedMovePending)
                moveDestinationWorld = requestedMoveDestination;

            Combatant requestedTarget = requestedPriorityTarget != null
                ? requestedPriorityTarget.GetComponent<Combatant>()
                : null;
            priorityTarget = IsValidEnemy(requestedTarget) ? requestedTarget : null;

            if (moveCommandPending)
            {
                ProcessPendingMoveCommand();
                return;
            }

            if (movementIntent == MovementIntent.PlayerCommand)
            {
                if (combatant.IsMoving)
                    return;
                movementIntent = MovementIntent.None;
            }
            else if (movementIntent == MovementIntent.Autonomous && !combatant.IsMoving)
            {
                movementIntent = MovementIntent.None;
            }

            if (combatant.IsMoving || Time.time < nextTacticalEvaluationTime)
                return;

            nextTacticalEvaluationTime = Time.time + (
                autonomousMovementAllowed
                    ? tuning.AiEvaluationInterval
                    : tuning.EvaluationRefreshInterval);

            if (autonomousMovementAllowed)
                ChooseAutonomousCombatIntent();
            else
                RefreshCommandModeCombatIntent();
        }

        public void TickCombatExecutionFromBehavior(GameObject requestedCurrentTarget)
        {
            if (combatant == null || !combatant.IsAlive || director == null || director.BattleFinished)
            {
                combatant?.ResetBehaviorFireCycle();
                return;
            }

            Combatant blackboardTarget = requestedCurrentTarget != null
                ? requestedCurrentTarget.GetComponent<Combatant>()
                : null;
            if (blackboardTarget != null
                && blackboardTarget.IsAlive
                && blackboardTarget.Team != combatant.Team
                && blackboardTarget != combatant.CurrentTarget)
            {
                combatant.SetBehaviorTarget(blackboardTarget);
            }

            if (Time.time >= nextShotEvaluationTime)
            {
                nextShotEvaluationTime = Time.time + tuning.EvaluationRefreshInterval;
                combatant.RefreshShotEvaluationForCurrentTarget();
            }
            combatant.TickAutomaticFireCycleFromBehavior();
        }

        private void ProcessPendingMoveCommand()
        {
            GridCoordinate destination = gridMap.WorldToGrid(moveDestinationWorld);
            if (combatant.SetMoveDestination(destination))
            {
                movementIntent = MovementIntent.PlayerCommand;
                combatant.SetPeekEnabled(false);
            }

            moveCommandPending = false;
            WriteBlackboardValue("MoveCommandPending", false);
            nextTacticalEvaluationTime = 0f;
        }

        private void RefreshCommandModeCombatIntent()
        {
            List<TacticalCellChoice> choices = positionEvaluator.EvaluateCurrentCellFiringChoices(
                combatant,
                automaticPeekAllowed,
                priorityTarget);
            SetDebugChoices(choices);

            Combatant target = priorityTarget;
            if (target == null)
                target = director.FindClosestShootableEnemy(combatant, automaticPeekAllowed);

            combatant.SetBehaviorTarget(target);
            combatant.UpdateAutomaticPeekForCurrentTarget(automaticPeekAllowed);
            combatant.RefreshShotEvaluationForCurrentTarget();
            WriteCurrentTargetToBlackboard();
        }

        private void ChooseAutonomousCombatIntent()
        {
            List<TacticalCellChoice> allChoices = positionEvaluator.EvaluateReachableFiringCells(
                combatant,
                automaticPeekAllowed,
                priorityTarget);
            if (allChoices.Count == 0)
            {
                debugChoices.Clear();
                combatant.SetBehaviorTarget(priorityTarget);
                combatant.SetPeekEnabled(false);
                combatant.RefreshShotEvaluationForCurrentTarget();
                WriteCurrentTargetToBlackboard();
                return;
            }

            bool hasCoveredFiringPosition =
                allChoices.Exists(choice => choice.IsCoveredFiringPosition);
            List<TacticalCellChoice> movementChoices = hasCoveredFiringPosition
                ? allChoices.FindAll(choice => choice.IsCoveredFiringPosition)
                : allChoices;
            SetDebugChoices(movementChoices);

            TacticalCellChoice? currentMovementChoice =
                FindBestCurrentCellChoice(movementChoices);
            TacticalCellChoice? currentFiringChoice =
                FindBestCurrentCellChoice(allChoices);
            bool canMove = !combatant.IsMoving && Time.time >= nextMovementTime;
            if (canMove)
            {
                bool enteringCoverTier = hasCoveredFiringPosition
                    && (!currentMovementChoice.HasValue
                        || !currentMovementChoice.Value.IsCoveredFiringPosition);
                foreach (TacticalCellChoice choice in movementChoices)
                {
                    if (choice.Cell == combatant.CurrentCell)
                        continue;
                    if (!enteringCoverTier
                        && currentMovementChoice.HasValue
                        && choice.Score
                            < currentMovementChoice.Value.Score + tuning.AiMinimumImprovement)
                    {
                        continue;
                    }
                    if (!combatant.SetMoveDestination(choice.Cell))
                        continue;

                    combatant.SetBehaviorTarget(choice.Target);
                    combatant.SetPeekEnabled(false);
                    movementIntent = MovementIntent.Autonomous;
                    nextMovementTime = Time.time + tuning.AiMovementCooldown;
                    WriteCurrentTargetToBlackboard();
                    return;
                }
            }

            if (currentFiringChoice.HasValue)
            {
                combatant.SetBehaviorTarget(currentFiringChoice.Value.Target);
                combatant.SetPeekEnabled(
                    automaticPeekAllowed
                    && currentFiringChoice.Value.Evaluation.UsesPeekPosition);
            }
            else
            {
                combatant.SetBehaviorTarget(priorityTarget);
                combatant.SetPeekEnabled(false);
            }
            combatant.RefreshShotEvaluationForCurrentTarget();
            WriteCurrentTargetToBlackboard();
        }

        private TacticalCellChoice? FindBestCurrentCellChoice(
            List<TacticalCellChoice> choices)
        {
            foreach (TacticalCellChoice choice in choices)
            {
                if (choice.Cell == combatant.CurrentCell)
                    return choice;
            }
            return null;
        }

        private void SetDebugChoices(List<TacticalCellChoice> choices)
        {
            debugChoices.Clear();
            for (int index = 0; index < Mathf.Min(3, choices.Count); index++)
                debugChoices.Add(choices[index]);
        }

        private bool IsValidEnemy(Combatant target)
            => target != null && target.IsAlive && target.Team != combatant.Team;

        private void WriteCurrentTargetToBlackboard()
        {
            Combatant target = combatant.CurrentTarget;
            WriteBlackboardValue(
                "CurrentTarget",
                target != null && target.IsAlive ? target.gameObject : null);
        }

        private void WriteBlackboardValue<TValue>(string variableName, TValue value)
        {
            if (behaviorAgent != null)
                behaviorAgent.SetVariableValue(variableName, value);
        }

        private void HandleCombatantDied(Combatant deadCombatant)
        {
            StopBehaviorForDeath();
        }

        private void OnDrawGizmos()
        {
            if (director == null || gridMap == null || !director.DebugVisible)
                return;

            for (int index = 0; index < debugChoices.Count; index++)
            {
                TacticalCellChoice choice = debugChoices[index];
                Gizmos.color = index == 0
                    ? Color.magenta
                    : new Color(1f, 0.45f, 1f, 0.65f);
                Vector3 position = gridMap.GridToWorld(choice.Cell)
                    + Vector3.up * (0.1f + index * 0.08f);
                Gizmos.DrawWireCube(
                    position,
                    new Vector3(
                        gridMap.CellSize * 0.8f,
                        0.08f,
                        gridMap.CellSize * 0.8f));
#if UNITY_EDITOR
                string positionType =
                    choice.IsCoveredFiringPosition ? "COVER" : "EXPOSED";
                string shotType =
                    choice.Evaluation.UsesPeekPosition ? "PEEK" : "DIRECT";
                string coverAngle = choice.IncomingCover.AngleDegrees >= 0f
                    ? $"{choice.IncomingCover.AngleDegrees:0}deg"
                    : "-";
                UnityEditor.Handles.Label(
                    position + Vector3.up * 0.25f,
                    $"#{index + 1} {positionType} {choice.Target.name} {shotType} ANG {coverAngle} HIT {choice.Evaluation.HitChancePercent:0}% SCORE {choice.Score:0}");
#endif
            }
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Combatant newCombatant,
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatDirector newDirector,
            TacticalPositionEvaluator newPositionEvaluator,
            CombatTuning newTuning,
            BehaviorGraphAgent newBehaviorAgent,
            bool newAutonomousMovementDefault,
            bool newAutomaticPeekDefault)
        {
            combatant = newCombatant;
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            director = newDirector;
            positionEvaluator = newPositionEvaluator;
            tuning = newTuning;
            behaviorAgent = newBehaviorAgent;
            autonomousMovementDefault = newAutonomousMovementDefault;
            automaticPeekDefault = newAutomaticPeekDefault;
        }
#endif
    }
}
