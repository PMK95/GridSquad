using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class OffHandMount : MonoBehaviour
    {
        [SerializeField] private Transform offHandSocket;
        private GameObject activePresentation;

        public void RefreshPresentation(OffHandDefinition definition)
        {
            if (activePresentation != null)
                Destroy(activePresentation);
            activePresentation = null;
            if (definition == null || definition.PresentationPrefab == null || offHandSocket == null)
                return;
            activePresentation = Instantiate(definition.PresentationPrefab, offHandSocket, false);
            activePresentation.name = definition.PresentationPrefab.name;
        }

#if UNITY_EDITOR
        public void SetEditorSocket(Transform newOffHandSocket)
        {
            offHandSocket = newOffHandSocket;
        }
#endif
    }
}
