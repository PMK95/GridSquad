using System;
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
        [SerializeField] private CharacterWorldUiPresenter worldUi;
        [SerializeField] private UnitAnimationController animationController;
        [SerializeField] private CombatantFeedbackPresenter feedbackPresenter;

        private readonly List<GridCoordinate> movementPath = new();
        private int movementIndex;
        private GridCoordinate movementDestination;
        private bool hasMovementDestination;
        private bool hasTransitReservation;
        private float blockedMovementSeconds;
        private int currentHealth;
        private GridCoordinate currentCell;
        private Combatant currentTarget;
        private ShotEvaluation currentShotEvaluation;
        private FireCycleState fireCycleState;
        private float fireStateRemainingSeconds;
        private Combatant aimingTarget;
        private int currentMagazineAmmo;
        private int reserveAmmo;
        private int pendingReloadAmmo;
        private float reloadElapsedSeconds;
        private bool reloadAnimationStarted;
        private float hitReactionRemainingSeconds;
        private bool peekEnabled;
        private bool selected;
        private bool debugVisible;
        private Vector3 activePeekOffset;

        public event Action<Combatant> Died;

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
        public float FireStateRemainingSeconds => IsReloading && reloadAnimationStarted
            ? Mathf.Max(0f, weapon.ReloadDuration - reloadElapsedSeconds)
            : Mathf.Max(0f, fireStateRemainingSeconds);
        public Transform AimCenterTransform => aimCenter != null ? aimCenter : transform;
        public int CurrentMagazineAmmo => currentMagazineAmmo;
        public int ReserveAmmo => reserveAmmo;
        public int MagazineCapacity => weapon != null ? Mathf.Max(1, weapon.MagazineCapacity) : 1;
        public float ReloadProgress => IsReloading && reloadAnimationStarted
            ? Mathf.Clamp01(reloadElapsedSeconds / Mathf.Max(0.01f, weapon.ReloadDuration))
            : 0f;
        public float MagazineFillRatio
        {
            get
            {
                float displayedAmmo = currentMagazineAmmo;
                if (IsReloading && reloadAnimationStarted)
                    displayedAmmo += pendingReloadAmmo * ReloadProgress;
                return Mathf.Clamp01(displayedAmmo / MagazineCapacity);
            }
        }
        public bool IsReloading => fireCycleState == FireCycleState.Reloading;
        public bool IsOutOfAmmo => fireCycleState == FireCycleState.OutOfAmmo;
        public bool IsHitReacting => hitReactionRemainingSeconds > 0f;
        public float MuzzleHeight => muzzle != null ? muzzle.position.y - transform.position.y : 1.25f;
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.25f;
        public Vector3 CurrentAimCenter => aimCenter != null ? aimCenter.position : transform.position + Vector3.up * 1.1f;
        public GridCoordinate CurrentIndicatorShotOriginCell
            => peekEnabled && currentShotEvaluation.UsesPeekPosition ? currentShotEvaluation.ShotOriginCell : currentCell;
        public GridCoordinate CurrentExposureCell
            => peekEnabled && currentShotEvaluation.UsesPeekPosition ? currentShotEvaluation.ShotOriginCell : currentCell;
        public Vector3 CurrentIndicatorShotOrigin
            => peekEnabled && currentShotEvaluation.UsesPeekPosition ? currentShotEvaluation.ShotOrigin : MuzzlePosition;
        public Vector3 CurrentExposureCenter
        {
            get
            {
                Vector3 exposureCenter = gridMap.GridToWorld(CurrentExposureCell);
                exposureCenter.y = CurrentAimCenter.y;
                return exposureCenter;
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
            currentMagazineAmmo = MagazineCapacity;
            reserveAmmo = weapon != null ? Mathf.Max(0, weapon.StartingReserveAmmo) : 0;
            currentCell = gridMap.WorldToGrid(transform.position);
            transform.position = gridMap.GridToWorld(currentCell);
            gridMap.RegisterOccupant(this, currentCell);
            if (animationController == null)
                animationController = GetComponentInChildren<UnitAnimationController>(true);
            if (feedbackPresenter == null)
                feedbackPresenter = GetComponentInChildren<CombatantFeedbackPresenter>(true);
            animationController?.Initialize();
            worldUi.Initialize(this);
            worldUi.SetSelected(false);
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            UpdateHitReaction();
            bool movedThisFrame = MoveAlongPath();
            RotateTowardMovementOrTarget();
            animationController?.SetMovementState(movedThisFrame && !IsReloading && !IsHitReacting);
            worldUi.Refresh(currentTarget, currentShotEvaluation, selected, debugVisible);
        }

        private void OnDestroy()
        {
            if (gridMap != null)
                gridMap.UnregisterOccupant(this, currentCell);
        }

        public bool SetMoveDestination(GridCoordinate destination)
        {
            if (!IsAlive || IsReloading)
                return false;
            if (destination == currentCell)
            {
                movementPath.Clear();
                movementIndex = 0;
                gridMap.ReleaseReservation(this);
                gridMap.ReleaseTransitReservation(this);
                hasMovementDestination = false;
                hasTransitReservation = false;
                blockedMovementSeconds = 0f;
                ResetFireCycle();
                return true;
            }
            List<GridCoordinate> path = GridPathfinder.FindPath(gridMap, currentCell, destination, this);
            if (path == null || !gridMap.TryReserveCell(this, destination))
                return false;

            gridMap.ReleaseTransitReservation(this);
            movementPath.Clear();
            movementPath.AddRange(path);
            movementIndex = 0;
            movementDestination = destination;
            hasMovementDestination = true;
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
            peekEnabled = false;
            SetActivePeekOffset(Vector3.zero);
            ResetFireCycle();
            return true;
        }

        public void SetBehaviorTarget(Combatant target)
        {
            Combatant newTarget = target != null && target.Team != team && target.IsAlive ? target : null;
            if (currentTarget != newTarget)
                ResetFireCycle();
            currentTarget = newTarget;
        }

        public void SetPriorityTarget(Combatant target) => SetBehaviorTarget(target);

        public void SetPeekEnabled(bool enabled)
        {
            if (peekEnabled == enabled)
                return;
            peekEnabled = enabled;
            if (!enabled)
                SetActivePeekOffset(Vector3.zero);
            ResetFireCycle();
        }

        public void StopMovementAtCurrentCell()
        {
            movementPath.Clear();
            movementIndex = 0;
            gridMap.ReleaseReservation(this);
            gridMap.ReleaseTransitReservation(this);
            hasMovementDestination = false;
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
            ResetFireCycle();
        }

        public void ApplyDamage(int damage)
        {
            if (!IsAlive)
                return;
            int previousHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            int appliedDamage = previousHealth - currentHealth;
            if (appliedDamage <= 0)
                return;

            worldUi.RefreshHealth();
            feedbackPresenter?.PlayDamageFeedback(appliedDamage, CurrentAimCenter);
            if (currentHealth == 0)
            {
                EnterDeadState();
                return;
            }

            if (!ShouldInterruptCurrentActionForHit())
                return;

            float hitDuration = animationController != null
                ? animationController.PlayHitReaction()
                : 0f;
            if (hitDuration > 0f)
                hitReactionRemainingSeconds = hitDuration;
        }

        private bool ShouldInterruptCurrentActionForHit()
        {
            float chancePercent = tuning != null
                ? tuning.HitReactionInterruptChancePercent
                : 0f;
            return UnityEngine.Random.value < chancePercent / 100f;
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

        private bool MoveAlongPath()
        {
            if (movementIndex >= movementPath.Count || IsReloading || IsHitReacting)
                return false;

            GridCoordinate nextCell = movementPath[movementIndex];
            if (!hasTransitReservation)
            {
                if (!gridMap.TryReserveTransitCell(this, nextCell))
                {
                    TryRebuildPathAroundBlockingUnit();
                    return false;
                }
                hasTransitReservation = true;
            }
            Vector3 destination = gridMap.GridToWorld(nextCell);
            transform.position = Vector3.MoveTowards(transform.position, destination, tuning.MovementSpeed * Time.deltaTime);
            if ((transform.position - destination).sqrMagnitude > 0.0001f)
                return true;

            GridCoordinate previous = currentCell;
            currentCell = nextCell;
            gridMap.MoveOccupant(this, previous, currentCell);
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
            movementIndex++;
            if (movementIndex >= movementPath.Count)
            {
                movementPath.Clear();
                gridMap.ReleaseReservation(this);
                hasMovementDestination = false;
            }
            return true;
        }

        private void TryRebuildPathAroundBlockingUnit()
        {
            if (!hasMovementDestination)
                return;

            blockedMovementSeconds += Time.deltaTime;
            float retryDelay = 0.12f + Mathf.Abs(GetInstanceID() % 4) * 0.03f;
            if (blockedMovementSeconds < retryDelay)
                return;

            blockedMovementSeconds = 0f;
            List<GridCoordinate> alternatePath = GridPathfinder.FindPath(
                gridMap,
                currentCell,
                movementDestination,
                this,
                true);
            if (alternatePath == null || alternatePath.Count == 0)
                return;

            gridMap.ReleaseTransitReservation(this);
            hasTransitReservation = false;
            movementPath.Clear();
            movementPath.AddRange(alternatePath);
            movementIndex = 0;
        }

        private void RotateTowardMovementOrTarget()
        {
            if (IsHitReacting)
                return;

            Vector3 lookDirection = Vector3.zero;
            if (movementIndex < movementPath.Count)
                lookDirection = gridMap.GridToWorld(movementPath[movementIndex]) - transform.position;
            else if (currentTarget != null && currentTarget.IsAlive)
                lookDirection = currentTarget.CurrentExposureCenter - transform.position;

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

        public void RefreshShotEvaluationForCurrentTarget()
        {
            if (currentTarget != null && !currentTarget.IsAlive)
                SetBehaviorTarget(null);
            currentShotEvaluation = shotEvaluator.EvaluateShot(this, currentTarget);
            Vector3 unshiftedMuzzlePosition = transform.position + Vector3.up * MuzzleHeight;
            Vector3 desiredOffset = currentShotEvaluation.UsesPeekPosition
                ? currentShotEvaluation.ShotOrigin - unshiftedMuzzlePosition
                : Vector3.zero;
            SetActivePeekOffset(desiredOffset);
        }

        public void UpdateAutomaticPeekForCurrentTarget(bool allowed)
        {
            if (!allowed || IsMoving || currentTarget == null || !currentTarget.IsAlive)
            {
                SetPeekEnabled(false);
                return;
            }

            ShotEvaluation direct = shotEvaluator.EvaluateShotFromCell(
                this,
                currentTarget,
                currentCell,
                false);
            ShotEvaluation bestWithPeek = shotEvaluator.EvaluateShotFromCell(
                this,
                currentTarget,
                currentCell,
                true);
            bool shouldPeek = bestWithPeek.CanShoot
                && bestWithPeek.UsesPeekPosition
                && (!direct.CanShoot || bestWithPeek.HitChancePercent > direct.HitChancePercent);
            if (peekEnabled == shouldPeek)
                return;
            peekEnabled = shouldPeek;
            ResetFireCycle();
        }

        public void TickAutomaticFireCycleFromBehavior()
        {
            if (IsHitReacting)
                return;

            if (IsReloading)
            {
                TickReload();
                return;
            }

            if (IsMoving || currentTarget == null || !currentTarget.IsAlive || !currentShotEvaluation.CanShoot)
            {
                ResetFireCycle();
                return;
            }

            switch (fireCycleState)
            {
                case FireCycleState.WaitingForAim:
                    if (currentMagazineAmmo <= 0)
                    {
                        BeginReload(0f);
                        return;
                    }
                    aimingTarget = currentTarget;
                    fireCycleState = FireCycleState.Aiming;
                    fireStateRemainingSeconds = weapon.AimEnterDuration;
                    animationController?.BeginAimingAt(
                        currentTarget.AimCenterTransform,
                        weapon.AimEnterDuration);
                    return;

                case FireCycleState.Aiming:
                    if (aimingTarget != currentTarget)
                    {
                        ResetFireCycle();
                        return;
                    }
                    fireStateRemainingSeconds = Mathf.Max(
                        0f,
                        fireStateRemainingSeconds - Time.deltaTime);
                    if (fireStateRemainingSeconds > 0f)
                        return;
                    if (!IsAimAlignedWithCurrentTarget())
                        return;
                    if (animationController != null && !animationController.IsAimReady)
                        return;
                    if (!FireCurrentShot())
                    {
                        ResetFireCycle();
                        return;
                    }
                    fireCycleState = FireCycleState.AimedFiring;
                    fireStateRemainingSeconds = weapon.AimedShotInterval;
                    if (currentMagazineAmmo <= 0)
                        BeginReload(CalculatePostShotReloadDelay());
                    return;

                case FireCycleState.AimedFiring:
                    fireStateRemainingSeconds = Mathf.Max(
                        0f,
                        fireStateRemainingSeconds - Time.deltaTime);
                    if (fireStateRemainingSeconds > 0f)
                        return;
                    if (!IsAimAlignedWithCurrentTarget())
                        return;
                    if (animationController != null && !animationController.IsAimReady)
                        return;
                    if (!FireCurrentShot())
                    {
                        ResetFireCycle();
                        return;
                    }
                    fireStateRemainingSeconds = weapon.AimedShotInterval;
                    if (currentMagazineAmmo <= 0)
                        BeginReload(CalculatePostShotReloadDelay());
                    return;

                case FireCycleState.OutOfAmmo:
                    return;
            }
        }

        private bool FireCurrentShot()
        {
            if (IsMoving || currentTarget == null || !currentTarget.IsAlive || !IsAimAlignedWithCurrentTarget())
                return false;

            currentShotEvaluation = shotEvaluator.EvaluateShot(this, currentTarget);
            if (!currentShotEvaluation.CanShoot)
                return false;

            feedbackPresenter?.PlayShotFeedback(
                currentShotEvaluation.ShotOrigin,
                currentShotEvaluation.TargetCenter);

            if (UnityEngine.Random.value * 100f <= currentShotEvaluation.HitChancePercent)
                currentTarget.ApplyDamage(weapon.Damage);
            else
                currentTarget.PlayMissFeedback();
            currentMagazineAmmo = Mathf.Max(0, currentMagazineAmmo - 1);
            animationController?.PlayShot();
            return true;
        }

        private bool IsAimAlignedWithCurrentTarget()
        {
            if (currentTarget == null || !currentTarget.IsAlive)
                return false;

            Vector3 aimDirection = currentTarget.CurrentExposureCenter - transform.position;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude <= 0.0001f)
                return true;
            return Vector3.Angle(transform.forward, aimDirection) <= tuning.FireAimToleranceDegrees;
        }

        private float CalculatePostShotReloadDelay()
        {
            float shotAnimationDuration = animationController != null
                ? animationController.ShotDuration
                : 0f;
            return Mathf.Max(weapon.AimedShotInterval, shotAnimationDuration);
        }

        public void ResetBehaviorFireCycle() => ResetFireCycle(true);

        private void ResetFireCycle(bool cancelReload = false)
        {
            if (IsReloading && !cancelReload)
                return;

            fireCycleState = FireCycleState.WaitingForAim;
            fireStateRemainingSeconds = 0f;
            aimingTarget = null;
            pendingReloadAmmo = 0;
            reloadElapsedSeconds = 0f;
            reloadAnimationStarted = false;
            animationController?.StopAiming();
            if (cancelReload)
                animationController?.CompleteReload();
        }

        private void BeginReload(float shotAnimationDelay)
        {
            if (currentMagazineAmmo >= MagazineCapacity)
            {
                fireCycleState = FireCycleState.WaitingForAim;
                return;
            }
            if (reserveAmmo <= 0)
            {
                fireCycleState = FireCycleState.OutOfAmmo;
                fireStateRemainingSeconds = 0f;
                animationController?.StopAiming();
                return;
            }

            pendingReloadAmmo = Mathf.Min(MagazineCapacity - currentMagazineAmmo, reserveAmmo);
            reloadElapsedSeconds = 0f;
            reloadAnimationStarted = false;
            fireCycleState = FireCycleState.Reloading;
            fireStateRemainingSeconds = Mathf.Max(0f, shotAnimationDelay);
            if (fireStateRemainingSeconds <= 0f)
                StartReloadAnimation();
        }

        private void TickReload()
        {
            if (!reloadAnimationStarted)
            {
                fireStateRemainingSeconds = Mathf.Max(
                    0f,
                    fireStateRemainingSeconds - Time.deltaTime);
                if (fireStateRemainingSeconds > 0f)
                    return;
                StartReloadAnimation();
                return;
            }

            reloadElapsedSeconds += Time.deltaTime;
            if (reloadElapsedSeconds < weapon.ReloadDuration)
                return;

            currentMagazineAmmo += pendingReloadAmmo;
            reserveAmmo -= pendingReloadAmmo;
            pendingReloadAmmo = 0;
            reloadElapsedSeconds = 0f;
            reloadAnimationStarted = false;
            fireCycleState = currentMagazineAmmo > 0
                ? FireCycleState.WaitingForAim
                : FireCycleState.OutOfAmmo;
            animationController?.CompleteReload();
        }

        private void StartReloadAnimation()
        {
            reloadAnimationStarted = true;
            reloadElapsedSeconds = 0f;
            fireStateRemainingSeconds = 0f;
            animationController?.StopAiming();
            animationController?.BeginReload(weapon.ReloadDuration);
        }

        private void UpdateHitReaction()
        {
            if (!IsHitReacting)
                return;

            hitReactionRemainingSeconds = Mathf.Max(
                0f,
                hitReactionRemainingSeconds - Time.deltaTime);
            if (hitReactionRemainingSeconds > 0f)
                return;

            if (IsReloading && reloadAnimationStarted)
            {
                animationController?.BeginReload(weapon.ReloadDuration, ReloadProgress);
                return;
            }
            if ((fireCycleState == FireCycleState.Aiming
                    || fireCycleState == FireCycleState.AimedFiring)
                && currentTarget != null
                && currentTarget.IsAlive)
            {
                animationController?.BeginAimingAt(
                    currentTarget.AimCenterTransform,
                    weapon.AimEnterDuration);
            }
        }

        public void PlayMissFeedback()
        {
            if (IsAlive)
                feedbackPresenter?.PlayMissFeedback(CurrentAimCenter);
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
            hitReactionRemainingSeconds = 0f;
            ResetFireCycle(true);
            gridMap.UnregisterOccupant(this, currentCell);
            movementPath.Clear();
            hasMovementDestination = false;
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
            SetActivePeekOffset(Vector3.zero);
            if (selectionCollider != null)
                selectionCollider.enabled = false;
            float deathAnimationDuration = animationController != null
                ? animationController.PlayDeath()
                : 0f;
            worldUi.SetDead();
            Died?.Invoke(this);
            BattleResult battleResult = director.NotifyCombatantDied(
                this,
                deathAnimationDuration);
            feedbackPresenter?.PlayDeathFeedback(battleResult, CurrentAimCenter);
        }

        public void PrepareForBattleResult()
        {
            if (!IsAlive)
                return;

            movementPath.Clear();
            movementIndex = 0;
            gridMap.ReleaseReservation(this);
            gridMap.ReleaseTransitReservation(this);
            hasMovementDestination = false;
            hasTransitReservation = false;
            blockedMovementSeconds = 0f;
            SetBehaviorTarget(null);
            ResetFireCycle(true);
            hitReactionRemainingSeconds = 0f;
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
            CharacterWorldUiPresenter newWorldUi,
            CombatantFeedbackPresenter newFeedbackPresenter)
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
            worldUi = newWorldUi;
            feedbackPresenter = newFeedbackPresenter;
        }
#endif
    }
}
