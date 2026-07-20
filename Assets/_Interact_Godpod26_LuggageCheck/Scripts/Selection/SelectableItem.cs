using UnityEngine;
using UnityEngine.Events;

namespace RFIDBaggage.Selection
{
    public enum SelectionDeselectReason
    {
        Normal,
        GameplayEnded
    }

    public sealed class SelectableItem : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Unique item identifier used for logs.")]
        private string itemId;

        [SerializeField, Tooltip("Whether this item is a contraband target.")]
        private bool isContraband;

        [SerializeField, Tooltip("Contraband prompt slot index. Non-contraband items should use -1.")]
        private int contrabandSlotIndex = -1;

        [Header("Selection")]
        [SerializeField, Tooltip("Position used for screen-space navigation. Uses this transform when empty.")]
        private Transform selectionPoint;

        [SerializeField, Tooltip("Whether this item starts selectable when reset.")]
        private bool initiallySelectable = true;

        [SerializeField, Tooltip("Marks this item as the preferred default selection for its level.")]
        private bool defaultSelected;

        [Header("Unity Events")]
        [SerializeField] private UnityEvent onSelected = new UnityEvent();
        [SerializeField] private UnityEvent onDeselected = new UnityEvent();
        [SerializeField] private UnityEvent onGameplayEndedDeselected = new UnityEvent();
        [SerializeField] private UnityEvent onCorrect = new UnityEvent();
        [SerializeField] private UnityEvent onWrong = new UnityEvent();
        [SerializeField] private UnityEvent onReset = new UnityEvent();

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
        public bool IsContraband => isContraband;
        public int ContrabandSlotIndex => contrabandSlotIndex;
        public Transform SelectionPoint => selectionPoint != null ? selectionPoint : transform;
        public bool IsSelectable { get; private set; }
        public bool IsResolved { get; private set; }
        public bool IsSelected { get; private set; }
        public bool DefaultSelected => defaultSelected;

        private void Awake()
        {
            IsSelectable = initiallySelectable;
        }

        public void SetSelected(bool selected, SelectionDeselectReason deselectReason = SelectionDeselectReason.Normal)
        {
            if (IsSelected == selected)
            {
                return;
            }

            IsSelected = selected;

            if (selected)
            {
                onSelected.Invoke();
            }
            else
            {
                if (deselectReason == SelectionDeselectReason.GameplayEnded)
                {
                    onGameplayEndedDeselected.Invoke();
                }
                else
                {
                    onDeselected.Invoke();
                }
            }
        }

        public void SetSelectable(bool selectable)
        {
            IsSelectable = selectable;

            if (!selectable)
            {
                SetSelected(false);
            }
        }

        public void MarkCorrect()
        {
            if (IsResolved)
            {
                return;
            }

            IsResolved = true;
            IsSelectable = false;
            SetSelected(false);
            onCorrect.Invoke();
        }

        public void PlayWrongFeedback()
        {
            onWrong.Invoke();
        }

        public void ResetItem()
        {
            IsResolved = false;
            IsSelectable = initiallySelectable;
            SetSelected(false);
            onReset.Invoke();
        }
    }
}
