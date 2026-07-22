using GridSquad;
using UnityEditor;
using UnityEngine;

namespace GridSquadEditor
{
    [InitializeOnLoad]
    public static class TacticalEntityPrefabConfigurator
    {
        private const string UnitBasePrefabPath = "Assets/GridSquad/Prefabs/UnitBase.prefab";

        static TacticalEntityPrefabConfigurator()
        {
            EditorApplication.delayCall += EnsureUnitBaseAbilityComponents;
        }

        private static void EnsureUnitBaseAbilityComponents()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitBasePrefabPath);
            if (prefab == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(UnitBasePrefabPath);
            try
            {
                Combatant combatant = root.GetComponent<Combatant>();
                if (combatant == null)
                    return;

                TacticalEntity entity = GetOrAddComponent<TacticalEntity>(root);
                EntityHealth health = GetOrAddComponent<EntityHealth>(root);
                ShootableTarget shootableTarget = GetOrAddComponent<ShootableTarget>(root);
                GridMovementController movementController =
                    GetOrAddComponent<GridMovementController>(root);
                RangedAttackController attackController =
                    GetOrAddComponent<RangedAttackController>(root);
                GetOrAddComponent<CombatTargetingController>(root);
                GetOrAddComponent<WeaponRuntimeController>(root);
                GetOrAddComponent<RangedFireCycleController>(root);
                GetOrAddComponent<CombatantHitReactionController>(root);
                GetOrAddComponent<CombatantStatusEffectController>(root);
                GetOrAddComponent<CombatantFacingController>(root);
                GetOrAddComponent<CombatCommandState>(root);
                GetOrAddComponent<CombatDecisionCoordinator>(root);
                Collider selectionCollider = root.GetComponent<Collider>();
                Transform aimCenter = root.transform.Find("VisualRoot/AimCenter");

                entity.SetEditorConfiguration("전투원", true, null);
                health.SetEditorMaximumHealth(100);
                shootableTarget.SetEditorConfiguration(aimCenter, selectionCollider, false);
                combatant.SetEditorAbilityComponents(
                    entity,
                    health,
                    shootableTarget,
                    movementController,
                    attackController);
                PrefabUtility.SaveAsPrefabAsset(root, UnitBasePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
