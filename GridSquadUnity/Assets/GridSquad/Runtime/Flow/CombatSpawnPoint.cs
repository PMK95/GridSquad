using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class CombatSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnId = "spawn";
        [SerializeField] private Team team;

        public string SpawnId => spawnId;
        public Team Team => team;

#if UNITY_EDITOR
        public void SetEditorConfiguration(string newSpawnId, Team newTeam)
        {
            spawnId = newSpawnId;
            team = newTeam;
        }
#endif
    }
}
