using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class CombatDirector : MonoBehaviour
    {
        [SerializeField] private Combatant[] combatants;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatHudController hud;

        private bool debugVisible;
        private bool battleFinished;

        public IReadOnlyList<Combatant> Combatants => combatants;
        public bool DebugVisible => debugVisible;
        public bool BattleFinished => battleFinished;

        public Combatant FindClosestShootableEnemy(Combatant requester)
        {
            Combatant best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Combatant candidate in combatants)
            {
                if (candidate == null || !candidate.IsAlive || candidate.Team == requester.Team)
                    continue;
                bool allowPeek = requester.Team == Team.Ally || requester.PeekEnabled;
                ShotEvaluation evaluation = shotEvaluator.EvaluateShotFromCell(
                    requester,
                    candidate,
                    requester.CurrentCell,
                    allowPeek);
                if (!evaluation.CanShoot)
                    continue;
                float distance = (candidate.transform.position - requester.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public IEnumerable<Combatant> GetLivingEnemies(Team team)
        {
            foreach (Combatant combatant in combatants)
            {
                if (combatant != null && combatant.IsAlive && combatant.Team != team)
                    yield return combatant;
            }
        }

        public void SetDebugVisible(bool value)
        {
            debugVisible = value;
            foreach (Combatant combatant in combatants)
                combatant?.SetDebugVisible(value);
            hud.SetDebugState(value);
        }

        public void NotifyCombatantDied(Combatant deadCombatant)
        {
            if (battleFinished)
                return;

            bool alliesAlive = false;
            bool enemiesAlive = false;
            foreach (Combatant combatant in combatants)
            {
                if (combatant == null || !combatant.IsAlive)
                    continue;
                if (combatant.Team == Team.Ally)
                    alliesAlive = true;
                else
                    enemiesAlive = true;
            }

            if (alliesAlive && enemiesAlive)
                return;
            battleFinished = true;
            Time.timeScale = 0f;
            hud.ShowResult(alliesAlive ? "VICTORY" : "DEFEAT");
        }

#if UNITY_EDITOR
        public void SetEditorReferences(Combatant[] newCombatants, ShotEvaluator newShotEvaluator, CombatHudController newHud)
        {
            combatants = newCombatants;
            shotEvaluator = newShotEvaluator;
            hud = newHud;
        }
#endif
    }
}
