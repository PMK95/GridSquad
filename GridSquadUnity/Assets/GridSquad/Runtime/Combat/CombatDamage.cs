namespace GridSquad
{
    public readonly struct CombatDamageRequest
    {
        public readonly Combatant Attacker;
        public readonly WeaponDefinition Weapon;
        public readonly int Damage;
        public readonly int AttackSequence;

        public CombatDamageRequest(
            Combatant attacker,
            WeaponDefinition weapon,
            int damage,
            int attackSequence = 0)
        {
            Attacker = attacker;
            Weapon = weapon;
            Damage = damage;
            AttackSequence = attackSequence;
        }
    }

    public readonly struct CombatDamageResult
    {
        public readonly int RequestedDamage;
        public readonly int AppliedDamage;
        public readonly bool BlockedByArmor;

        public CombatDamageResult(int requestedDamage, int appliedDamage, bool blockedByArmor)
        {
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            BlockedByArmor = blockedByArmor;
        }
    }
}
