using Unity.Behavior;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(CombatCommandState),
        typeof(CombatActionController),
        typeof(CombatDecisionCoordinator))]
    public sealed class UnitTacticalBehaviorController : MonoBehaviour
    {
        [SerializeField] private Combatant combatant;
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private TacticalPositionEvaluator positionEvaluator;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private BehaviorGraphAgent behaviorAgent;
        [SerializeField] private bool autonomousMovementDefault;
        [SerializeField] private bool automaticPeekDefault = true;
        [SerializeField] private CombatCommandState commandState;
        [SerializeField] private CombatActionController actionController;
        [SerializeField] private CombatDecisionCoordinator decisionCoordinator;
        private bool runtimeInitialized;

        public bool AutonomousMovementAllowed => CombatControlPolicy.Create(
            combatant != null ? combatant.Team : Team.Ally,
            commandState != null ? commandState.ControlMode : default).AllowsAutomaticMovement;
        public bool AutomaticPeekAllowed => commandState != null && commandState.AutomaticPeekAllowed;
        public bool MoveCommandPending => commandState != null && commandState.HasPendingMove;
        public ShootableTarget PriorityTarget => commandState != null ? commandState.PriorityTarget : null;
        public ShootableTarget CurrentTarget => combatant != null ? combatant.CurrentTarget : null;
        public BehaviorGraphAgent BehaviorAgent => behaviorAgent;
        public CombatActionController ActionController => actionController;
        public CombatControlMode ControlMode => commandState != null
            ? CombatControlPolicy.Create(combatant.Team, commandState.ControlMode).Mode
            : default;

        private void Awake()
        {
            EnsureAbilityComponents();
        }

        public void InitializeRuntime()
        {
            if (runtimeInitialized)
                return;
            EnsureAbilityComponents();
            actionController.ConfigureRuntime(
                combatant,
                gridMap,
                shotEvaluator,
                director,
                positionEvaluator,
                tuning);
            decisionCoordinator.ConfigureRuntime(
                combatant,
                director,
                tuning,
                commandState,
                actionController);
            runtimeInitialized = true;
        }

        private void OnEnable()
        {
            if (combatant != null)
                combatant.Died += HandleCombatantDied;
        }

        private void OnDisable()
        {
            if (combatant != null)
                combatant.Died -= HandleCombatantDied;
            decisionCoordinator?.InterruptCurrentIntent(CombatActionInterruptReason.CombatEnded);
        }

        public void QueueMoveCommand(GridCoordinate destination)
        {
            if (combatant == null || !combatant.IsAlive || gridMap == null)
                return;
            commandState.QueueMove(destination);
            combatant.RequestStopMovementAfterCurrentCell();
        }

        public void SetPriorityTargetCommand(ShootableTarget target)
        {
            ShootableTarget validTarget = IsValidPriorityTarget(target) ? target : null;
            commandState.SetPriorityTarget(validTarget);
            if (validTarget != null)
            {
                combatant.SetBehaviorTarget(validTarget);
                combatant.RequestStopMovementAfterCurrentCell();
            }
        }

        public void SetAutomaticPeekAllowed(bool allowed)
        {
            commandState.SetAutomaticPeekAllowed(allowed);
            if (!allowed)
                combatant.SetPeekEnabled(false);
        }

        public void SetAutonomousMovementAllowed(bool allowed)
        {
            SetControlMode(allowed
                ? CombatControlMode.FullAutomatic
                : CombatControlMode.PlayerMovementAutomaticActions);
        }

        public void SetControlMode(CombatControlMode mode)
        {
            CombatControlMode normalizedMode = CombatControlPolicy.Create(
                combatant != null ? combatant.Team : Team.Ally,
                mode).Mode;
            CombatControlMode previousMode = commandState.ControlMode;
            commandState.SetControlMode(normalizedMode);
            if (previousMode == normalizedMode)
                return;
            decisionCoordinator.InterruptCurrentIntent(CombatActionInterruptReason.ControlModeChanged);
            if (previousMode == CombatControlMode.FullAutomatic
                && normalizedMode != CombatControlMode.FullAutomatic)
            {
                combatant.RequestStopMovementAfterCurrentCell();
            }
        }

        public void CancelAutonomousMovement()
        {
            if (AutonomousMovementAllowed)
                return;
            decisionCoordinator.InterruptCurrentIntent(CombatActionInterruptReason.ControlModeChanged);
            combatant.RequestStopMovementAfterCurrentCell();
        }

        public void StopBehaviorForDeath()
        {
            decisionCoordinator?.InterruptCurrentIntent(CombatActionInterruptReason.OwnerDied);
            commandState?.ClearAllCommands();
            behaviorAgent?.End();
        }

        public void SuspendUntilBattleStart()
        {
            EnsureAbilityComponents();
            decisionCoordinator?.InterruptCurrentIntent(
                CombatActionInterruptReason.CombatEnded);
            commandState?.ClearAllCommands();
            behaviorAgent?.End();
        }

        public void StartBehaviorForBattle()
        {
            InitializeRuntime();
            SetControlMode(director != null
                ? director.GetControlModeFor(combatant)
                : autonomousMovementDefault
                    ? CombatControlMode.FullAutomatic
                    : CombatControlMode.PlayerMovementAutomaticActions);
            SetAutomaticPeekAllowed(automaticPeekDefault);
            decisionCoordinator?.ResetForBattle();
            behaviorAgent?.Restart();
        }

        public bool TrySelectCombatIntentFromBehavior()
        {
            return decisionCoordinator != null && decisionCoordinator.TrySelectNextIntent();
        }

        public bool TryBeginSelectedCombatIntentFromBehavior(out string failureReason)
        {
            if (decisionCoordinator == null)
            {
                failureReason = "전투 판단 컴포넌트가 없습니다.";
                return false;
            }
            return decisionCoordinator.TryBeginSelectedIntent(out failureReason);
        }

        public CombatActionExecutionStatus TickSelectedCombatIntentFromBehavior()
        {
            return decisionCoordinator != null
                ? decisionCoordinator.TickSelectedIntent()
                : CombatActionExecutionStatus.Failed;
        }

        public void InterruptSelectedCombatIntentFromBehavior()
        {
            decisionCoordinator?.InterruptCurrentIntent(CombatActionInterruptReason.ControlModeChanged);
        }

        public void InterruptSelectedCombatIntent(CombatActionInterruptReason reason)
        {
            decisionCoordinator?.InterruptCurrentIntent(reason);
        }

        private bool IsValidPriorityTarget(ShootableTarget target)
        {
            return target != null
                && target != combatant.ShootableTarget
                && target.IsAlive
                && target.TargetTeam != combatant.Team;
        }

        private void EnsureAbilityComponents()
        {
            if (commandState == null)
                commandState = GetComponent<CombatCommandState>();
            if (commandState == null)
                commandState = gameObject.AddComponent<CombatCommandState>();
            if (actionController == null)
                actionController = GetComponent<CombatActionController>();
            if (actionController == null)
                actionController = gameObject.AddComponent<CombatActionController>();
            if (decisionCoordinator == null)
                decisionCoordinator = GetComponent<CombatDecisionCoordinator>();
            if (decisionCoordinator == null)
                decisionCoordinator = gameObject.AddComponent<CombatDecisionCoordinator>();
        }

        private void HandleCombatantDied(Combatant deadCombatant)
        {
            StopBehaviorForDeath();
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
            EnsureAbilityComponents();
        }
#endif
    }
}
