using System;
using System.Collections.Generic;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(TacticalEntity),
        typeof(EntityHealth),
        typeof(ShootableTarget))]
    [RequireComponent(typeof(GridMovementController), typeof(RangedAttackController))]
    [RequireComponent(
        typeof(CombatantHitReactionController),
        typeof(CombatantStatusEffectController),
        typeof(CombatantFacingController))]
    public sealed class Combatant : MonoBehaviour
    {
        [Header("전투 설정")]
        [SerializeField] private Team team;
        [SerializeField] private UnitDefinition unitDefinition;
        [SerializeField] private int maximumHealth = 100;
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private CombatTuning tuning;
        [SerializeField] private UnitStatCatalog statCatalog;

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
        [SerializeField] private WeaponLoadout weaponLoadout;
        [SerializeField] private EquipmentLoadout equipmentLoadout;
        [SerializeField] private UnitInventory inventory;
        [SerializeField] private UnitItemInteractionController itemInteractionController;

        [Header("능력 컴포넌트")]
        [SerializeField] private TacticalEntity entity;
        [SerializeField] private EntityHealth health;
        [SerializeField] private ShootableTarget shootableTarget;
        [SerializeField] private GridMovementController movementController;
        [SerializeField] private RangedAttackController rangedAttackController;
        [SerializeField] private CombatantHitReactionController hitReactionController;
        [SerializeField] private CombatantStatusEffectController statusEffectController;
        [SerializeField] private CombatantFacingController facingController;

        private bool debugVisible;
        private readonly UnitRuntimeStatCollection effectiveStats = new();

        public event Action<Combatant> Died;

        public Team Team => team;
        public UnitDefinition UnitDefinition => unitDefinition;
        public string DisplayName => unitDefinition != null ? unitDefinition.DisplayName : name;
        public string RoleName => unitDefinition != null
            ? unitDefinition.RoleName
            : team == Team.Ally ? "아군" : "적군";
        public string Description => unitDefinition != null ? unitDefinition.Description : string.Empty;
        public Sprite Portrait => unitDefinition != null ? unitDefinition.Portrait : null;
        public Color AccentColor => unitDefinition != null
            ? unitDefinition.AccentColor
            : team == Team.Ally
                ? new Color(0.2f, 0.75f, 0.9f, 1f)
                : new Color(0.8f, 0.2f, 0.2f, 1f);
        public IReadOnlyList<UnitTraitDefinition> Traits => unitDefinition != null
            ? unitDefinition.Traits
            : Array.Empty<UnitTraitDefinition>();
        public IReadOnlyList<UnitRuntimeStatEntry> EffectiveStats => effectiveStats.Entries;
        public float HitChanceBonusPercent => GetCoreStatValue(
            statCatalog != null ? statCatalog.HitChanceBonusPercent : null,
            0f);
        public int EffectiveWeaponDamage => Weapon != null
            ? Mathf.Max(1, Mathf.RoundToInt(
                Weapon.AttackDamage * GetCoreStatValue(
                    statCatalog != null ? statCatalog.DamageMultiplier : null,
                    1f)))
            : 0;
        public bool IsAlive => health != null && health.IsAlive;
        public bool IsMoving => movementController != null && movementController.IsMoving;
        public bool IsSelected => entity != null && entity.IsSelected;
        public bool PeekEnabled => rangedAttackController != null && rangedAttackController.PeekEnabled;
        public int CurrentHealth => health != null ? health.CurrentHealth : 0;
        public int MaximumHealth => health != null ? health.MaximumHealth : Mathf.Max(1, maximumHealth);
        public GridCoordinate CurrentCell => entity != null ? entity.CurrentCell : default;
        public WeaponDefinition Weapon => weaponLoadout != null && weaponLoadout.ActiveDefinition != null
            ? weaponLoadout.ActiveDefinition
            : weapon;
        public WeaponLoadout WeaponLoadout => weaponLoadout;
        public EquipmentLoadout EquipmentLoadout => equipmentLoadout;
        public UnitInventory Inventory => inventory;
        public UnitItemInteractionController ItemInteractionController => itemInteractionController;
        public float CarryCapacity => GetCoreStatValue(
            statCatalog != null ? statCatalog.CarryCapacity : null,
            30f);
        public int ActiveWeaponSlotIndex => weaponLoadout != null ? weaponLoadout.ActiveSlotIndex : 0;
        public ShootableTarget CurrentTarget => rangedAttackController != null
            ? rangedAttackController.CurrentTarget
            : null;
        public ShotEvaluation CurrentShotEvaluation => rangedAttackController != null
            ? rangedAttackController.CurrentShotEvaluation
            : default;
        public FireCycleState FireState => rangedAttackController != null
            ? rangedAttackController.FireState
            : FireCycleState.WaitingForAim;
        public float FireStateRemainingSeconds => rangedAttackController != null
            ? rangedAttackController.FireStateRemainingSeconds
            : 0f;
        public Transform AimCenterTransform => aimCenter != null ? aimCenter : transform;
        public int CurrentMagazineAmmo => rangedAttackController != null
            ? rangedAttackController.CurrentMagazineAmmo
            : 0;
        public int ReserveAmmo => rangedAttackController != null
            ? rangedAttackController.ReserveAmmo
            : 0;
        public int MagazineCapacity => rangedAttackController != null
            ? rangedAttackController.MagazineCapacity
            : 1;
        public float ReloadProgress => rangedAttackController != null
            ? rangedAttackController.ReloadProgress
            : 0f;
        public float MagazineFillRatio => rangedAttackController != null
            ? rangedAttackController.MagazineFillRatio
            : 0f;
        public bool IsReloading => rangedAttackController != null && rangedAttackController.IsReloading;
        public bool IsOutOfAmmo => rangedAttackController != null && rangedAttackController.IsOutOfAmmo;
        public bool IsHitReacting => hitReactionController != null && hitReactionController.IsReacting;
        public bool IsStimActive => statusEffectController != null && statusEffectController.IsStimActive;
        public bool IsStunned => statusEffectController != null && statusEffectController.IsStunned;
        public float StimRemainingSeconds => statusEffectController != null
            ? statusEffectController.StimRemainingSeconds
            : 0f;
        public float MuzzleHeight => rangedAttackController != null
            ? rangedAttackController.MuzzleHeight
            : 1.25f;
        public Vector3 MuzzlePosition => rangedAttackController != null
            ? rangedAttackController.MuzzlePosition
            : transform.position + Vector3.up * 1.25f;
        public Vector3 CurrentAimCenter => aimCenter != null
            ? aimCenter.position
            : transform.position + Vector3.up * 1.1f;
        public GridCoordinate CurrentIndicatorShotOriginCell => rangedAttackController != null
            ? rangedAttackController.CurrentIndicatorShotOriginCell
            : CurrentCell;
        public GridCoordinate CurrentExposureCell => rangedAttackController != null
            ? rangedAttackController.CurrentExposureCell
            : CurrentCell;
        public Vector3 CurrentIndicatorShotOrigin => rangedAttackController != null
            ? rangedAttackController.CurrentIndicatorShotOrigin
            : MuzzlePosition;
        public Vector3 CurrentExposureCenter => rangedAttackController != null
            ? rangedAttackController.CurrentExposureCenter
            : CurrentAimCenter;
        public TacticalEntity Entity => entity;
        public EntityHealth Health => health;
        public ShootableTarget ShootableTarget => shootableTarget;
        public GridMap GridMap => gridMap;
        internal float CurrentMovementSpeedMultiplier => GetCoreStatValue(
                statCatalog != null ? statCatalog.MovementSpeedMultiplier : null,
                1f)
            * (statusEffectController != null ? statusEffectController.MovementSpeedMultiplier : 1f);

        public float GetStatValue(UnitStatDefinition definition)
            => effectiveStats.GetValue(definition);

        private void Awake()
        {
            EnsureAbilityComponents();
            if (weaponLoadout == null)
                weaponLoadout = GetComponent<WeaponLoadout>();
            if (animationController == null)
                animationController = GetComponentInChildren<UnitAnimationController>(true);
            if (feedbackPresenter == null)
                feedbackPresenter = GetComponentInChildren<CombatantFeedbackPresenter>(true);

            if (unitDefinition != null)
            {
                effectiveStats.Rebuild(statCatalog, unitDefinition.BaseStatValues, unitDefinition.Traits);
                if (statCatalog != null && statCatalog.MaximumHealth != null)
                {
                    maximumHealth = Mathf.Max(
                        1,
                        Mathf.RoundToInt(effectiveStats.GetValue(statCatalog.MaximumHealth)));
                }
                inventory?.ApplyUnitDefinitionDefaults(unitDefinition);
                CombatActionLoadout actionLoadout = GetComponent<CombatActionLoadout>();
                actionLoadout?.ApplyUnitDefinitionDefaults(unitDefinition);
                GetComponent<CombatActionController>()?.RefreshRuntimeActionsFromLoadout();
            }
            else
            {
                effectiveStats.Rebuild(statCatalog, null, null);
                Debug.LogWarning(
                    $"[유닛 데이터] {name}에 UnitDefinition이 없어 기존 직렬화 값을 사용합니다.",
                    this);
            }

            GridCoordinate initialCell = gridMap != null
                ? gridMap.WorldToGrid(transform.position)
                : default;
            entity.ConfigureRuntime(DisplayName, team, initialCell);
            health.Initialize(maximumHealth, true);
            shootableTarget.ConfigureRuntime(
                entity,
                health,
                this,
                AimCenterTransform,
                selectionCollider,
                false);
            movementController.Initialize(this, entity, gridMap, tuning);
            rangedAttackController.Initialize(
                this,
                shootableTarget,
                shotEvaluator,
                tuning,
                animationController,
                feedbackPresenter,
                weaponLoadout,
                muzzle);
            hitReactionController.Initialize(this, tuning, animationController, rangedAttackController);
            statusEffectController.Initialize(rangedAttackController);
            facingController.Initialize(this, movementController, tuning, visualRoot);
            equipmentLoadout.EquipmentChanged += HandleEquipmentChanged;

            health.DamageApplied += HandleDamageApplied;
            health.HealthDepleted += HandleHealthDepleted;
            entity.SelectionStateChanged += HandleSelectionStateChanged;
            animationController?.Initialize();
            worldUi?.Initialize(this);
            worldUi?.SetSelected(false);
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            hitReactionController.Tick();
            statusEffectController.Tick();
            bool movedThisFrame = movementController.TickMovement(CurrentMovementSpeedMultiplier);
            facingController.Tick(IsHitReacting);
            animationController?.SetMovementState(movedThisFrame && !IsReloading && !IsHitReacting);

            rangedAttackController.GetPresentationTarget(
                out ShootableTarget presentationTarget,
                out ShotEvaluation presentationEvaluation);
            worldUi?.Refresh(
                presentationTarget,
                presentationEvaluation,
                IsSelected,
                debugVisible);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.DamageApplied -= HandleDamageApplied;
                health.HealthDepleted -= HandleHealthDepleted;
            }
            if (entity != null)
                entity.SelectionStateChanged -= HandleSelectionStateChanged;
            if (equipmentLoadout != null)
                equipmentLoadout.EquipmentChanged -= HandleEquipmentChanged;
        }

        public void AppendRemainingPathWorldPoints(List<Vector3> points)
        {
            movementController?.AppendRemainingPathWorldPoints(points);
        }

        public bool SetMoveDestination(GridCoordinate destination)
        {
            bool accepted = movementController != null
                && movementController.SetMoveDestination(destination);
            if (!accepted)
                return false;

            rangedAttackController.SetPeekEnabled(false);
            rangedAttackController.ResetBehaviorFireCycle();
            return true;
        }

        public void SetBehaviorTarget(ShootableTarget target)
        {
            rangedAttackController?.SetBehaviorTarget(target);
        }

        public void SetPriorityTarget(ShootableTarget target)
        {
            SetBehaviorTarget(target);
        }

        public void SetPeekEnabled(bool enabled)
        {
            rangedAttackController?.SetPeekEnabled(enabled);
        }

        public void StopMovementAtCurrentCell()
        {
            movementController?.StopMovementAtCurrentCell();
            rangedAttackController?.ResetBehaviorFireCycle();
        }

        public void RequestStopMovementAfterCurrentCell()
        {
            movementController?.RequestStopMovementAfterCurrentCell();
        }

        public void PrepareForExclusiveCombatAction()
        {
            rangedAttackController?.PrepareForExclusiveCombatAction();
        }

        public bool InitializeWeaponLoadoutForBattle(out string failureReason)
        {
            equipmentLoadout?.InitializeForBattle();
            GetComponent<CombatActionController>()?.RefreshRuntimeActionsFromLoadout();
            if (rangedAttackController == null)
            {
                failureReason = string.Empty;
                return true;
            }
            return rangedAttackController.InitializeWeaponLoadoutForBattle(out failureReason);
        }

        public void ApplyStim(
            float durationSeconds,
            float movementSpeedMultiplier,
            float fireIntervalMultiplier)
        {
            statusEffectController?.ApplyStim(
                durationSeconds,
                movementSpeedMultiplier,
                fireIntervalMultiplier);
        }

        public void SetTemporaryMovementSpeedMultiplier(float multiplier)
        {
            statusEffectController?.SetTemporaryMovementSpeedMultiplier(multiplier);
        }

        public float PlayThrowAnimation()
            => animationController != null ? animationController.PlayThrowAction() : 0f;

        public float PlayUseItemAnimation()
            => animationController != null ? animationController.PlayUseItemAction() : 0f;

        public float PlayDashAnimation()
            => animationController != null ? animationController.PlayDashAction() : 0f;

        public void ApplyDamage(int damage)
        {
            ApplyDamage(new CombatDamageRequest(null, null, damage));
        }

        public CombatDamageResult ApplyDamage(CombatDamageRequest request)
        {
            int requestedDamage = Mathf.Max(0, request.Damage);
            if (requestedDamage > 0
                && equipmentLoadout != null
                && equipmentLoadout.TryBlockDamage(
                    out AdditionalEquipmentDefinition plate,
                    out int remaining,
                    out int maximum))
            {
                feedbackPresenter?.PlayBlockFeedback(CurrentAimCenter);
                Debug.Log($"[장비 방어] {DisplayName}의 {plate.DisplayName}이 피해를 방어했습니다. 충전 {remaining}/{maximum}");
                return new CombatDamageResult(requestedDamage, 0, true);
            }
            int applied = health != null ? health.ApplyDamage(requestedDamage) : 0;
            return new CombatDamageResult(requestedDamage, applied, false);
        }

        public int RestoreHealth(int amount)
        {
            return health != null ? health.RestoreHealth(amount) : 0;
        }

        public void ApplyStun(float durationSeconds)
        {
            if (!IsAlive)
                return;
            statusEffectController?.ApplyStun(durationSeconds);
            movementController?.RequestStopMovementAfterCurrentCell();
            GetComponent<UnitTacticalBehaviorController>()
                ?.InterruptSelectedCombatIntent(CombatActionInterruptReason.Stunned);
            Debug.Log($"[상태 효과] {DisplayName} 기절 {durationSeconds:0.0}초 적용");
        }

        public bool TryApplyKnockback(GridCoordinate sourceCell, int distanceCells)
        {
            if (!IsAlive || movementController == null)
                return false;
            GetComponent<UnitTacticalBehaviorController>()
                ?.InterruptSelectedCombatIntent(CombatActionInterruptReason.PlayerCommand);
            return movementController.TryApplyForcedDisplacement(sourceCell, distanceCells);
        }

        public void SetSelected(bool value)
        {
            entity?.SetSelected(value);
        }

        public void SetManualTargetHoverPreview(ShootableTarget target)
        {
            rangedAttackController?.SetManualTargetHoverPreview(target);
        }

        public void ClearManualTargetHoverPreview()
        {
            rangedAttackController?.ClearManualTargetHoverPreview();
        }

        public void SetDebugVisible(bool value)
        {
            debugVisible = value;
        }

        public void RefreshShotEvaluationForCurrentTarget()
        {
            rangedAttackController?.RefreshShotEvaluationForCurrentTarget();
        }

        public void UpdateAutomaticPeekForCurrentTarget(bool allowed)
        {
            rangedAttackController?.UpdateAutomaticPeekForCurrentTarget(allowed);
        }

        public void TickAutomaticFireCycleFromBehavior()
        {
            rangedAttackController?.TickAutomaticFireCycleFromBehavior();
        }

        public void ResetBehaviorFireCycle()
        {
            rangedAttackController?.ResetBehaviorFireCycle();
        }

        public void PlayMissFeedback()
        {
            if (IsAlive)
                feedbackPresenter?.PlayMissFeedback(CurrentAimCenter);
        }

        public void PrepareForBattleResult()
        {
            if (!IsAlive)
                return;

            movementController?.PrepareForBattleResult();
            rangedAttackController?.PrepareForBattleResult();
            hitReactionController?.ResetState();
            statusEffectController?.ResetState();
        }

        internal void SetLegacyWeaponDefinition(WeaponDefinition definition)
        {
            weapon = definition;
        }

        internal void SetActivePeekOffset(Vector3 worldOffset)
        {
            facingController?.SetPeekOffset(worldOffset);
        }

        private void EnsureAbilityComponents()
        {
            entity = entity != null ? entity : GetComponent<TacticalEntity>();
            if (entity == null)
                entity = gameObject.AddComponent<TacticalEntity>();
            health = health != null ? health : GetComponent<EntityHealth>();
            if (health == null)
                health = gameObject.AddComponent<EntityHealth>();
            shootableTarget = shootableTarget != null ? shootableTarget : GetComponent<ShootableTarget>();
            if (shootableTarget == null)
                shootableTarget = gameObject.AddComponent<ShootableTarget>();
            movementController = movementController != null
                ? movementController
                : GetComponent<GridMovementController>();
            if (movementController == null)
                movementController = gameObject.AddComponent<GridMovementController>();
            rangedAttackController = rangedAttackController != null
                ? rangedAttackController
                : GetComponent<RangedAttackController>();
            if (rangedAttackController == null)
                rangedAttackController = gameObject.AddComponent<RangedAttackController>();
            hitReactionController = hitReactionController != null
                ? hitReactionController
                : GetComponent<CombatantHitReactionController>();
            if (hitReactionController == null)
                hitReactionController = gameObject.AddComponent<CombatantHitReactionController>();
            statusEffectController = statusEffectController != null
                ? statusEffectController
                : GetComponent<CombatantStatusEffectController>();
            if (statusEffectController == null)
                statusEffectController = gameObject.AddComponent<CombatantStatusEffectController>();
            equipmentLoadout = equipmentLoadout != null
                ? equipmentLoadout
                : GetComponent<EquipmentLoadout>();
            if (equipmentLoadout == null)
                equipmentLoadout = gameObject.AddComponent<EquipmentLoadout>();
            inventory = inventory != null ? inventory : GetComponent<UnitInventory>();
            if (inventory == null)
                inventory = gameObject.AddComponent<UnitInventory>();
            itemInteractionController = itemInteractionController != null
                ? itemInteractionController
                : GetComponent<UnitItemInteractionController>();
            if (itemInteractionController == null)
                itemInteractionController = gameObject.AddComponent<UnitItemInteractionController>();
            if (GetComponent<CombatantContextCommandProvider>() == null)
                gameObject.AddComponent<CombatantContextCommandProvider>();
            if (GetComponent<CombatantItemContextCommandProvider>() == null)
                gameObject.AddComponent<CombatantItemContextCommandProvider>();
            facingController = facingController != null
                ? facingController
                : GetComponent<CombatantFacingController>();
            if (facingController == null)
                facingController = gameObject.AddComponent<CombatantFacingController>();
        }

        private void HandleDamageApplied(EntityHealth source, int appliedDamage)
        {
            worldUi?.RefreshHealth();
            feedbackPresenter?.PlayDamageFeedback(appliedDamage, CurrentAimCenter);
            hitReactionController?.TryStart(source);
        }

        private void HandleHealthDepleted(EntityHealth source)
        {
            EnterDeadState();
        }

        private void HandleSelectionStateChanged(TacticalEntity changedEntity, bool selected)
        {
            worldUi?.SetSelected(selected);
        }

        private void HandleEquipmentChanged()
        {
            weaponLoadout?.RefreshEquippedWeapon();
            rangedAttackController?.RefreshEquippedWeapon();
            GetComponent<CombatActionController>()?.RefreshRuntimeActionsFromLoadout();
        }

        private void EnterDeadState()
        {
            hitReactionController?.ResetState();
            rangedAttackController?.PrepareForEntityRemoval();
            movementController?.PrepareForEntityRemoval();
            statusEffectController?.ResetState();
            if (selectionCollider != null)
                selectionCollider.enabled = false;
            float deathAnimationDuration = animationController != null
                ? animationController.PlayDeath()
                : 0f;
            worldUi?.SetDead();
            entity?.MarkUnavailable();
            Died?.Invoke(this);
            BattleResult battleResult = director != null
                ? director.NotifyCombatantDied(this, deathAnimationDuration)
                : BattleResult.None;
            feedbackPresenter?.PlayDeathFeedback(battleResult, CurrentAimCenter);
        }

#if UNITY_EDITOR
        public void SetEditorUnitDefinition(UnitDefinition newUnitDefinition)
        {
            unitDefinition = newUnitDefinition;
        }

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
            EnsureAbilityComponents();
            health.SetEditorMaximumHealth(newMaximumHealth);
            shootableTarget.SetEditorConfiguration(newAimCenter, newSelectionCollider, false);
        }

        public void SetEditorWeaponLoadout(WeaponLoadout newWeaponLoadout)
        {
            weaponLoadout = newWeaponLoadout;
        }

        public void SetEditorStatCatalog(UnitStatCatalog newStatCatalog)
        {
            statCatalog = newStatCatalog;
        }

        public void SetEditorAbilityComponents(
            TacticalEntity newEntity,
            EntityHealth newHealth,
            ShootableTarget newShootableTarget,
            GridMovementController newMovementController,
            RangedAttackController newRangedAttackController)
        {
            entity = newEntity;
            health = newHealth;
            shootableTarget = newShootableTarget;
            movementController = newMovementController;
            rangedAttackController = newRangedAttackController;
            EnsureAbilityComponents();
        }
#endif
        private float GetCoreStatValue(UnitStatDefinition definition, float fallback)
        {
            return definition != null ? effectiveStats.GetValue(definition) : fallback;
        }
    }
}
