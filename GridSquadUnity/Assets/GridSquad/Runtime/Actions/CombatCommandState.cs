using UnityEngine;

namespace GridSquad
{
    public enum CombatPlayerCommandKind
    {
        None,
        Move,
        Action
    }

    public readonly struct CombatPlayerCommand
    {
        public readonly CombatPlayerCommandKind Kind;
        public readonly GridCoordinate MoveDestination;
        public readonly CombatActionCommand Action;
        public readonly int Sequence;

        public CombatPlayerCommand(GridCoordinate destination, int sequence)
        {
            Kind = CombatPlayerCommandKind.Move;
            MoveDestination = destination;
            Action = default;
            Sequence = sequence;
        }

        public CombatPlayerCommand(CombatActionCommand action, int sequence)
        {
            Kind = CombatPlayerCommandKind.Action;
            MoveDestination = default;
            Action = action;
            Sequence = sequence;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CombatCommandState : MonoBehaviour
    {
        private CombatPlayerCommand pendingCommand;
        private int nextCommandSequence;
        private ShootableTarget priorityTarget;
        private CombatControlMode controlMode;
        private bool automaticPeekAllowed = true;
        private int revision;

        public bool HasPendingMove => pendingCommand.Kind == CombatPlayerCommandKind.Move;
        public bool HasPendingAction => pendingCommand.Kind == CombatPlayerCommandKind.Action;
        public bool HasPendingPlayerCommand => pendingCommand.Kind != CombatPlayerCommandKind.None;
        public ShootableTarget PriorityTarget => priorityTarget;
        public CombatControlMode ControlMode => controlMode;
        public bool AutomaticPeekAllowed => automaticPeekAllowed;
        public int Revision => revision;

        public void QueueMove(GridCoordinate destination)
        {
            pendingCommand = new CombatPlayerCommand(destination, ++nextCommandSequence);
            revision++;
        }

        public bool TryPeekMove(out GridCoordinate destination)
        {
            destination = pendingCommand.MoveDestination;
            return HasPendingMove;
        }

        public void CompletePendingMove()
        {
            if (HasPendingMove)
                pendingCommand = default;
        }

        public void QueueAction(CombatActionCommand command)
        {
            pendingCommand = new CombatPlayerCommand(command, ++nextCommandSequence);
            revision++;
        }

        public bool TryTakeAction(out CombatActionCommand command)
        {
            command = pendingCommand.Action;
            if (!HasPendingAction)
                return false;
            pendingCommand = default;
            return true;
        }

        public void SetPriorityTarget(ShootableTarget target)
        {
            priorityTarget = target;
            revision++;
        }

        public void ClearInvalidPriorityTarget(Combatant owner)
        {
            if (priorityTarget == null)
                return;
            if (priorityTarget.IsAlive
                && priorityTarget != owner.ShootableTarget
                && priorityTarget.TargetTeam != owner.Team)
            {
                return;
            }
            priorityTarget = null;
            revision++;
        }

        public void SetControlMode(CombatControlMode mode)
        {
            if (controlMode == mode)
                return;
            controlMode = mode;
            revision++;
        }

        public void SetAutomaticPeekAllowed(bool allowed)
        {
            if (automaticPeekAllowed == allowed)
                return;
            automaticPeekAllowed = allowed;
            revision++;
        }

        public void ClearAllCommands()
        {
            pendingCommand = default;
            priorityTarget = null;
            revision++;
        }
    }
}
