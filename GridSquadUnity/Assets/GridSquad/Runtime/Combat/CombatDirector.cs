using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace GridSquad
{
    public sealed class CombatDirector : MonoBehaviour
    {
        [SerializeField] private Combatant[] combatants;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatHudController hud;
        [SerializeField] private MMTimeManager timeManager;
        [SerializeField] private WeaponLoadoutPanel weaponLoadoutPanel;

        private bool debugVisible;
        private bool battleFinished;
        private bool battleStarted;
        private CombatControlMode allyControlMode = CombatControlMode.PlayerMovementAutomaticActions;

        public IReadOnlyList<Combatant> Combatants => combatants;
        public bool DebugVisible => debugVisible;
        public bool BattleFinished => battleFinished;
        public bool BattleStarted => battleStarted;
        public CombatControlMode AllyControlMode => allyControlMode;
        public bool AllyFullAutoEnabled => allyControlMode == CombatControlMode.FullAutomatic;

        private void Awake()
        {
            if (timeManager == null)
                timeManager = FindFirstObjectByType<MMTimeManager>();
        }

        private void Start()
        {
            SetAllyControlMode(CombatControlMode.PlayerMovementAutomaticActions);
            if (weaponLoadoutPanel != null)
                weaponLoadoutPanel.Show();
            else
                StartBattleWithCurrentLoadouts();
        }

        public void StartBattleWithCurrentLoadouts()
        {
            if (battleStarted || battleFinished)
                return;

            foreach (Combatant combatant in combatants)
            {
                if (combatant == null)
                    continue;
                if (!combatant.InitializeWeaponLoadoutForBattle(out string failureReason))
                {
                    Debug.LogError($"[전투 준비] {combatant.name}: {failureReason}");
                    weaponLoadoutPanel?.SetStatusMessage(failureReason);
                    return;
                }
            }

            battleStarted = true;
            weaponLoadoutPanel?.Hide();
            SetAllyControlMode(allyControlMode);
            foreach (Combatant combatant in combatants)
            {
                combatant?.GetComponent<UnitTacticalBehaviorController>()
                    ?.StartBehaviorForBattle();
            }
            Debug.Log("[전투 준비] 모든 유닛의 무기 로드아웃 초기화 완료");
        }

        public Combatant FindClosestShootableEnemy(Combatant requester, bool allowPeek)
        {
            Combatant best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Combatant candidate in combatants)
            {
                if (candidate == null || !candidate.IsAlive || candidate.Team == requester.Team)
                    continue;
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

        public void ToggleAllyFullAuto()
        {
            CycleAllyControlMode();
        }

        public void SetAllyFullAutoEnabled(bool enabled)
        {
            SetAllyControlMode(enabled
                ? CombatControlMode.FullAutomatic
                : CombatControlMode.PlayerMovementAutomaticActions);
        }

        public void CycleAllyControlMode()
        {
            CombatControlMode nextMode = allyControlMode switch
            {
                CombatControlMode.FullAutomatic => CombatControlMode.PlayerMovementAutomaticActions,
                CombatControlMode.PlayerMovementAutomaticActions => CombatControlMode.PlayerMovementPlayerActions,
                _ => CombatControlMode.FullAutomatic
            };
            SetAllyControlMode(nextMode);
        }

        public void SetAllyControlMode(CombatControlMode mode)
        {
            allyControlMode = mode;
            foreach (Combatant combatant in combatants)
            {
                if (combatant == null || combatant.Team != Team.Ally)
                    continue;
                UnitTacticalBehaviorController controller =
                    combatant.GetComponent<UnitTacticalBehaviorController>();
                if (controller != null)
                    controller.SetControlMode(mode);
            }
            hud?.SetAllyControlMode(mode);
        }

        public CombatControlMode GetControlModeFor(Combatant combatant)
            => combatant != null && combatant.Team == Team.Enemy
                ? CombatControlMode.FullAutomatic
                : allyControlMode;

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

        public BattleResult NotifyCombatantDied(
            Combatant deadCombatant,
            float deathAnimationDuration)
        {
            if (!battleStarted || battleFinished)
                return BattleResult.None;

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
                return BattleResult.None;

            battleFinished = true;
            BattleResult result = alliesAlive
                ? BattleResult.Victory
                : BattleResult.Defeat;
            StopCombatBeforeResultPresentation();
            hud.SetAllyFullAutoInteractable(false);
            StartCoroutine(ShowBattleResultAfterDeath(
                result,
                Mathf.Max(0f, deathAnimationDuration)));
            return result;
        }

        private void StopCombatBeforeResultPresentation()
        {
            foreach (Combatant combatant in combatants)
                combatant?.PrepareForBattleResult();
        }

        private IEnumerator ShowBattleResultAfterDeath(
            BattleResult result,
            float deathAnimationDuration)
        {
            if (deathAnimationDuration > 0f)
                yield return new WaitForSeconds(deathAnimationDuration);

            hud.ShowResult(result == BattleResult.Victory ? "VICTORY" : "DEFEAT");
            if (timeManager != null)
                timeManager.SetTimeScaleTo(0f);
            else
                Time.timeScale = 0f;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            Combatant[] newCombatants,
            ShotEvaluator newShotEvaluator,
            CombatHudController newHud,
            MMTimeManager newTimeManager)
        {
            combatants = newCombatants;
            shotEvaluator = newShotEvaluator;
            hud = newHud;
            timeManager = newTimeManager;
        }

        public void SetEditorWeaponLoadoutPanel(WeaponLoadoutPanel newWeaponLoadoutPanel)
        {
            weaponLoadoutPanel = newWeaponLoadoutPanel;
        }
#endif
    }
}
