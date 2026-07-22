using System;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class TacticalEntity : MonoBehaviour
    {
        [SerializeField] private string fallbackDisplayName = "전술 개체";
        [SerializeField] private bool selectable = true;
        [SerializeField] private GameObject selectionVisual;

        private string runtimeDisplayName;
        private Team? targetTeam;
        private GridCoordinate currentCell;
        private bool available = true;
        private bool selected;

        public event Action<TacticalEntity, bool> SelectionStateChanged;
        public event Action<TacticalEntity> BecameUnavailable;

        public string DisplayName => string.IsNullOrWhiteSpace(runtimeDisplayName)
            ? fallbackDisplayName
            : runtimeDisplayName;
        public bool IsAvailable => available;
        public bool IsSelectable => selectable;
        public bool IsSelected => selected;
        public Team? TargetTeam => targetTeam;
        public GridCoordinate CurrentCell => currentCell;
        public Combatant Combatant => GetComponent<Combatant>();
        public ShootableTarget ShootableTarget => GetComponent<ShootableTarget>();

        private void Awake()
        {
            if (selectionVisual == null)
            {
                Transform indicator = transform.Find("SelectionIndicator");
                if (indicator != null)
                    selectionVisual = indicator.gameObject;
            }
            if (selectionVisual != null)
                selectionVisual.SetActive(false);
        }

        public void ConfigureRuntime(string displayName, Team? team, GridCoordinate cell)
        {
            runtimeDisplayName = displayName;
            targetTeam = team;
            currentCell = cell;
            available = true;
        }

        public void SetCurrentCell(GridCoordinate cell)
        {
            currentCell = cell;
        }

        public void SetSelected(bool value)
        {
            bool nextSelected = value && available && selectable;
            if (selected == nextSelected)
                return;

            selected = nextSelected;
            if (selectionVisual != null)
                selectionVisual.SetActive(selected);
            SelectionStateChanged?.Invoke(this, selected);
        }

        public void MarkUnavailable()
        {
            if (!available)
                return;

            SetSelected(false);
            available = false;
            BecameUnavailable?.Invoke(this);
        }

#if UNITY_EDITOR
        public void SetEditorConfiguration(
            string newFallbackDisplayName,
            bool newSelectable,
            GameObject newSelectionVisual)
        {
            fallbackDisplayName = newFallbackDisplayName;
            selectable = newSelectable;
            selectionVisual = newSelectionVisual;
        }
#endif
    }
}
