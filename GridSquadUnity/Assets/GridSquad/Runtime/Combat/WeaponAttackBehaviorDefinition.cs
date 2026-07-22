using UnityEngine;

namespace GridSquad
{
    public enum WeaponAttackMode
    {
        Hitscan,
        Shotgun,
        Melee
    }

    public abstract class WeaponAttackBehaviorDefinition : ScriptableObject
    {
        public abstract WeaponAttackMode Mode { get; }
        public virtual bool UsesAmmo => true;
        public virtual int CalculateDamage(WeaponDefinition weapon) => weapon != null ? weapon.Damage : 0;
    }

    public readonly struct WeaponHitContext
    {
        public readonly Combatant Attacker;
        public readonly ShootableTarget Target;
        public readonly WeaponDefinition Weapon;

        public WeaponHitContext(Combatant attacker, ShootableTarget target, WeaponDefinition weapon)
        {
            Attacker = attacker;
            Target = target;
            Weapon = weapon;
        }
    }

    public abstract class WeaponHitEffectDefinition : ScriptableObject
    {
        public abstract void Apply(WeaponHitContext context);
    }

}
