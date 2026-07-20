using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    public sealed class Combatant : MonoBehaviour
    {
        [Header("전투 설정")]
        [SerializeField] private Team team;
        [SerializeField] private int maximumHealth = 100;
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private CombatTuning tuning;

        [Header("씬 참조")]
        [SerializeField] private GridMap gridMap;
        [SerializeField] private ShotEvaluator shotEvaluator;
        [SerializeField] private CombatDirector director;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform aimCenter;
        [SerializeField] private Collider selectionCollider;
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private ParticleSystem hitEffect;
        [SerializeField] private LineRenderer shotTracer;
        [SerializeField] private CharacterWorldUiPresenter worldUi;

        private readonly List<GridCoordinate> movementPath = new();
        private int movementIndex;
        private int currentHealth;
        private GridCoordinate currentCell;
        private Combatant priorityTarget;
        private Combatant currentTarget;
        private ShotEvaluation currentShotEvaluation;
        private float nextEvaluationTime;
        private FireCycleState fireCycleState;
        private float fireStateEndTime;
        private Combatant aimingTarget;
        private bool peekEnabled;
        private bool selected;
        private bool debugVisible;
        private Vector3 activePeekOffset;

        public Team Team => team;
        public bool IsAlive => currentHealth > 0;
        public bool IsMoving => movementIndex < movementPath.Count;
        public bool IsSelected => selected;
        public bool PeekEnabled => peekEnabled;
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        public GridCoordinate CurrentCell => currentCell;
        public WeaponDefinition Weapon => weapon;
        public Combatant CurrentTarget => currentTarget;
        public ShotEvaluation CurrentShotEvaluation => currentShotEvaluation;
        public FireCycleState FireState => fireCycleState;
        public float FireStateRemainingSeconds => Mathf.Max(0f, fireStateEndTime - Time.time);
        public float MuzzleHeight => muzzle != null ? muzzle.position.y - transform.position.y : 1.25f;
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.25f;
        public Vector3 CurrentAimCenter => aimCenter != null ? aimCenter.position : transform.position + Vector3.up * 1.1f;
        public Vector3 CurrentExposureCenter
        {
            get
            {
                if (!peekEnabled || !currentShotEvaluation.UsesPeekPosition)
                    return CurrentAimCenter;
                Vector3 virtualCenter = currentShotEvaluation.ShotOrigin;
                virtualCenter.y = CurrentAimCenter.y;
                return virtualCenter;
            }
        }

        public void AppendRemainingPathWorldPoints(List<Vector3> points)
        {
            points.Add(transform.position + Vector3.up * 0.08f);
            for (int index = movementIndex; index < movementPath.Count; index++)
                points.Add(gridMap.GridToWorld(movementPath[index]) + Vector3.up * 0.08f);
        }

        private void Awake()
        {
            currentHealth = maximumHealth;
            currentCell = gridMap.WorldToGrid(transform.position);
            transform.position = gridMap.GridToWorld(currentCell);
            gridMap.RegisterOccupant(this, currentCell);
            worldUi.Initialize(this);
            worldUi.SetSelected(false);
            if (shotTracer != null)
                shotTracer.enabled = false;
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            MoveAlongPath();
            RotateTowardMovementOrTarget();
            if (Time.time >= nextEvaluationTime)
            {
                nextEvaluationTime = Time.time + tuning.EvaluationRefreshInterval;
                RefreshTargetAndShotEvaluation();
            }
            UpdateAutomaticFireCycle();
            worldUi.Refresh(currentTarget, currentShotEvaluation, selected, debugVisible);
        }

        private void OnDestroy()
        {
            if (gridMap != null)
                gridMap.UnregisterOccupant(this, currentCell);
        }

        public bool SetMoveDestination(GridCoordinate destination)
        {
            if (!IsAlive)
                return false;
            if (destination == currentCell)
            {
                movementPath.Clear();
                movementIndex = 0;
                gridMap.ReleaseReservation(this);
                ResetFireCycle();
                return true;
            }
            List<GridCoordinate> path = GridPathfinder.FindPath(gridMap, currentCell, destination, this);
            if (path == null || !gridMap.TryReserveCell(this, destination))
                return false;

            movementPath.Clear();
            movementPath.AddRange(path);
            movementIndex = 0;
            peekEnabled = false;
            SetActivePeekOffset(Vector3.zero);
            ResetFireCycle();
            return true;
        }

        public void SetPriorityTarget(Combatant target)
        {
            Combatant newTarget = target != null && target.Team != team && target.IsAlive ? target : null;
            if (priorityTarget != newTarget)
                ResetFireCycle();
            priorityTarget = newTarget;
            currentTarget = priorityTarget;
            nextEvaluationTime = 0f;
        }

        public void SetPeekEnabled(bool enabled)
        {
            if (peekEnabled == enabled)
                return;
            peekEnabled = enabled;
            if (!enabled)
                SetActivePeekOffset(Vector3.zero);
            ResetFireCycle();
            nextEvaluationTime = 0f;
        }

        public void ApplyDamage(int damage)
        {
            if (!IsAlive)
                return;
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            if (hitEffect != null)
                hitEffect.Play(true);
            worldUi.RefreshHealth();
            if (currentHealth == 0)
                EnterDeadState();
        }

        public void SetSelected(bool value)
        {
            selected = value && IsAlive;
            worldUi.SetSelected(selected);
        }

        public void SetDebugVisible(bool value)
        {
            debugVisible = value;
        }

        private void MoveAlongPath()
        {
            if (movementIndex >= movementPath.Count)
                return;

            GridCoordinate nextCell = movementPath[movementIndex];
            if (!gridMap.IsWalkable(nextCell, this))
                return;
            Vector3 destination = gridMap.GridToWorld(nextCell);
            transform.position = Vector3.MoveTowards(transform.position, destination, tuning.MovementSpeed * Time.deltaTime);
            if ((transform.position - destination).sqrMagnitude > 0.0001f)
                return;

            GridCoordinate previous = currentCell;
            currentCell = nextCell;
            gridMap.MoveOccupant(this, previous, currentCell);
            movementIndex++;
            if (movementIndex >= movementPath.Count)
            {
                movementPath.Clear();
                gridMap.ReleaseReservation(this);
            }
        }

        private void RotateTowardMovementOrTarget()
        {
            Vector3 lookDirection = Vector3.zero;
            if (movementIndex < movementPath.Count)
                lookDirection = gridMap.GridToWorld(movementPath[movementIndex]) - transform.position;
            else if (currentTarget != null && currentTarget.IsAlive)
                lookDirection = currentTarget.CurrentAimCenter - transform.position;

            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                tuning.CharacterRotationSpeed * Time.deltaTime);

            ApplyPeekVisualLean();
        }

        private void RefreshTargetAndShotEvaluation()
        {
            if (priorityTarget != null && !priorityTarget.IsAlive)
                priorityTarget = null;

            currentTarget = priorityTarget;
            if (currentTarget == null)
                currentTarget = director.FindClosestShootableEnemy(this);

            if (team == Team.Ally && !IsMoving && currentTarget != null)
                UpdateAutomaticAllyPeek(currentTarget);
            currentShotEvaluation = shotEvaluator.EvaluateShot(this, currentTarget);
            Vector3 unshiftedMuzzlePosition = transform.position + Vector3.up * MuzzleHeight;
            Vector3 desiredOffset = currentShotEvaluation.UsesPeekPosition
                ? currentShotEvaluation.ShotOrigin - unshiftedMuzzlePosition
                : Vector3.zero;
            SetActivePeekOffset(desiredOffset);
        }

        private void UpdateAutomaticAllyPeek(Combatant target)
        {
            ShotEvaluation direct = shotEvaluator.EvaluateShotFromCell(this, target, currentCell, false);
            ShotEvaluation bestWithPeek = shotEvaluator.EvaluateShotFromCell(this, target, currentCell, true);
            bool shouldPeek = bestWithPeek.CanShoot
                && bestWithPeek.UsesPeekPosition
                && (!direct.CanShoot || bestWithPeek.HitChancePercent > direct.HitChancePercent);
            if (peekEnabled == shouldPeek)
                return;
            peekEnabled = shouldPeek;
            ResetFireCycle();
        }

        private void UpdateAutomaticFireCycle()
        {
            if (IsMoving || currentTarget == null || !currentTarget.IsAlive || !currentShotEvaluation.CanShoot)
            {
                ResetFireCycle();
                return;
            }

            switch (fireCycleState)
            {
                case FireCycleState.WaitingForAim:
                    aimingTarget = currentTarget;
                    fireCycleState = FireCycleState.Aiming;
                    fireStateEndTime = Time.time + weapon.AimDuration;
                    return;

                case FireCycleState.Aiming:
                    if (aimingTarget != currentTarget)
                    {
                        ResetFireCycle();
                        return;
                    }
                    if (Time.time < fireStateEndTime)
                        return;
                    if (!FireCurrentShot())
                    {
                        ResetFireCycle();
                        return;
                    }
                    fireCycleState = FireCycleState.Cooldown;
                    fireStateEndTime = Time.time + weapon.FireInterval;
                    aimingTarget = null;
                    return;

                case FireCycleState.Cooldown:
                    if (Time.time >= fireStateEndTime)
                    {
                        fireCycleState = FireCycleState.WaitingForAim;
                        fireStateEndTime = 0f;
                    }
                    return;
            }
        }

        private bool FireCurrentShot()
        {
            if (IsMoving || currentTarget == null || !currentTarget.IsAlive)
                return false;

            currentShotEvaluation = shotEvaluator.EvaluateShot(this, currentTarget);
            if (!currentShotEvaluation.CanShoot)
                return false;

            if (muzzleFlash != null)
                muzzleFlash.Play(true);
            StartCoroutine(ShowShotTracer(currentShotEvaluation.ShotOrigin, currentShotEvaluation.TargetCenter));

            if (Random.value * 100f <= currentShotEvaluation.HitChancePercent)
                currentTarget.ApplyDamage(weapon.Damage);
            return true;
        }

        private void ResetFireCycle()
        {
            fireCycleState = FireCycleState.WaitingForAim;
            fireStateEndTime = 0f;
            aimingTarget = null;
        }

        private IEnumerator ShowShotTracer(Vector3 start, Vector3 end)
        {
            if (shotTracer == null)
                yield break;
            shotTracer.SetPosition(0, start);
            shotTracer.SetPosition(1, end);
            shotTracer.enabled = true;
            yield return new WaitForSecondsRealtime(tuning.ShotTracerDuration);
            shotTracer.enabled = false;
        }

        private void SetActivePeekOffset(Vector3 worldOffset)
        {
            activePeekOffset = worldOffset;
            ApplyPeekVisualLean();
        }

        private void ApplyPeekVisualLean()
        {
            if (visualRoot == null)
                return;
            visualRoot.localPosition = Vector3.zero;
            Vector3 localOffset = transform.InverseTransformVector(activePeekOffset);
            localOffset.y = 0f;
            if (localOffset.sqrMagnitude < 0.0001f)
            {
                visualRoot.localRotation = Quaternion.identity;
                return;
            }
            Vector3 leanAxis = Vector3.Cross(Vector3.up, localOffset.normalized);
            visualRoot.localRotation = Quaternion.AngleAxis(tuning.PeekVisualLeanAngle, leanAxis);
        }

        private void EnterDeadState()
        {
            gridMap.UnregisterOccupant(this, currentCell);
            movementPath.Clear();
            SetActivePeekOffset(Vector3.zero);
            if (selectionCollider != null)
                selectionCollider.enabled = false;
            worldUi.SetDead();
            director.NotifyCombatantDied(this);
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            Team newTeam,
            int newMaximumHealth,
            WeaponDefinition newWeapon,
            CombatTuning newTuning,
            GridMap newGridMap,
            ShotEvaluator newShotEvaluator,
            CombatDirector newDirector,
            Transform newVisualRoot,
            Transform newMuzzle,
            Transform newAimCenter,
            Collider newSelectionCollider,
            ParticleSystem newMuzzleFlash,
            ParticleSystem newHitEffect,
            LineRenderer newShotTracer,
            CharacterWorldUiPresenter newWorldUi)
        {
            team = newTeam;
            maximumHealth = newMaximumHealth;
            weapon = newWeapon;
            tuning = newTuning;
            gridMap = newGridMap;
            shotEvaluator = newShotEvaluator;
            director = newDirector;
            visualRoot = newVisualRoot;
            muzzle = newMuzzle;
            aimCenter = newAimCenter;
            selectionCollider = newSelectionCollider;
            muzzleFlash = newMuzzleFlash;
            hitEffect = newHitEffect;
            shotTracer = newShotTracer;
            worldUi = newWorldUi;
        }
#endif
    }
}
