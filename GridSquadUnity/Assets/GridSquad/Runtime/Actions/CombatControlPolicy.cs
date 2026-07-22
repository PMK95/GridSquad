namespace GridSquad
{
    public readonly struct CombatControlPolicy
    {
        public readonly CombatControlMode Mode;
        public readonly bool AllowsAutomaticMovement;
        public readonly bool AllowsAutomaticAbilities;
        public readonly bool AllowsAutomaticBasicAttack;

        private CombatControlPolicy(
            CombatControlMode mode,
            bool allowsAutomaticMovement,
            bool allowsAutomaticAbilities,
            bool allowsAutomaticBasicAttack)
        {
            Mode = mode;
            AllowsAutomaticMovement = allowsAutomaticMovement;
            AllowsAutomaticAbilities = allowsAutomaticAbilities;
            AllowsAutomaticBasicAttack = allowsAutomaticBasicAttack;
        }

        public static CombatControlPolicy Create(Team team, CombatControlMode requestedMode)
        {
            CombatControlMode mode = team == Team.Enemy
                ? CombatControlMode.FullAutomatic
                : requestedMode;
            return mode switch
            {
                CombatControlMode.FullAutomatic => new CombatControlPolicy(mode, true, true, true),
                CombatControlMode.PlayerMovementAutomaticActions => new CombatControlPolicy(mode, false, true, true),
                _ => new CombatControlPolicy(mode, false, false, true)
            };
        }
    }
}
