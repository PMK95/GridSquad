using UnityEngine;

namespace GridSquad
{
    [CreateAssetMenu(menuName = "GridSquad/Equipment/Off-Hand Definition", fileName = "OffHandDefinition")]
    public sealed class OffHandDefinition : EquippableDefinition
    {
        [SerializeField] private GameObject presentationPrefab;

        public override EquipmentCategory Category => EquipmentCategory.OffHand;
        public GameObject PresentationPrefab => presentationPrefab;

#if UNITY_EDITOR
        public void SetEditorPresentationPrefab(GameObject newPresentationPrefab)
            => presentationPrefab = newPresentationPrefab;
#endif
    }
}
