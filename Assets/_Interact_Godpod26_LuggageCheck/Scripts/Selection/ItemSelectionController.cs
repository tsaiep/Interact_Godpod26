using System;
using System.Collections.Generic;
using RFIDBaggage.Core;
using UnityEngine;

namespace RFIDBaggage.Selection
{
    public sealed class ItemSelectionController : MonoBehaviour
    {
        [Header("Selection")]
        [SerializeField, Tooltip("Camera used to convert item world positions into screen-space navigation.")]
        private Camera selectionCamera;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum directional dot product required for a candidate.")]
        private float minimumDirectionDot = 0.25f;

        [SerializeField, Min(0f), Tooltip("Weight for angle mismatch in selection score.")]
        private float angleWeight = 2f;

        [SerializeField, Min(0f), Tooltip("Weight for distance in selection score.")]
        private float distanceWeight = 1f;

        [SerializeField, Tooltip("When enabled, logs selection movement.")]
        private bool logSelectionMoves = true;

        private GameFlowConfirmKey confirmKey = GameFlowConfirmKey.Space;

        private readonly List<SelectableItem> items = new List<SelectableItem>();
        private float navigationCooldown;
        private float confirmCooldown;
        private float nextNavigationTime;
        private float nextConfirmTime;
        private SelectableItem pendingDefaultSelection;

        public SelectableItem CurrentSelection { get; private set; }
        public bool InputEnabled { get; private set; }

        public event Action<SelectableItem> ItemConfirmed;

        private void Update()
        {
            if (!InputEnabled)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveSelection(Vector2.up);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveSelection(Vector2.down);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveSelection(Vector2.left);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveSelection(Vector2.right);
            }

            if (GameFlowManager.IsConfirmKeyDown(confirmKey))
            {
                ConfirmCurrentSelection();
            }
        }

        public void Initialize(
            IReadOnlyList<SelectableItem> selectableItems,
            SelectableItem defaultItem,
            float navigationInputCooldown,
            float confirmInputCooldown)
        {
            ResetController();

            navigationCooldown = Mathf.Max(0f, navigationInputCooldown);
            confirmCooldown = Mathf.Max(0f, confirmInputCooldown);

            if (selectableItems != null)
            {
                for (int i = 0; i < selectableItems.Count; i++)
                {
                    SelectableItem item = selectableItems[i];
                    if (IsValidCandidate(item) && !items.Contains(item))
                    {
                        items.Add(item);
                    }
                }
            }

            if (defaultItem == null || !items.Contains(defaultItem) || !IsValidCandidate(defaultItem))
            {
                defaultItem = FindFirstValidItem();
            }

            pendingDefaultSelection = defaultItem;
        }

        public void EnableInput()
        {
            InputEnabled = true;
            nextNavigationTime = Time.unscaledTime;
            nextConfirmTime = Time.unscaledTime;
        }

        public void DisableInput()
        {
            InputEnabled = false;
        }

        public void SelectInitialItem()
        {
            if (CurrentSelection != null)
            {
                return;
            }

            SelectableItem initialItem = IsValidCandidate(pendingDefaultSelection)
                ? pendingDefaultSelection
                : FindFirstValidItem();

            SetCurrentSelection(initialItem);
        }

        public void SetConfirmKey(GameFlowConfirmKey key)
        {
            confirmKey = key;
        }

        public void MoveSelection(Vector2 direction)
        {
            if (!InputEnabled || Time.unscaledTime < nextNavigationTime)
            {
                return;
            }

            if (CurrentSelection == null)
            {
                SetCurrentSelection(FindFirstValidItem());
                return;
            }

            SelectableItem next = FindDirectionalCandidate(direction.normalized);
            if (next == null)
            {
                return;
            }

            SelectableItem previous = CurrentSelection;
            SetCurrentSelection(next);
            nextNavigationTime = Time.unscaledTime + navigationCooldown;

            if (logSelectionMoves)
            {
                Debug.Log($"[Selection] {previous.ItemId} -> {next.ItemId}", this);
            }
        }

        public void ConfirmCurrentSelection()
        {
            if (!InputEnabled || Time.unscaledTime < nextConfirmTime || CurrentSelection == null)
            {
                return;
            }

            if (!IsValidCandidate(CurrentSelection))
            {
                SelectNearestFrom(GetCurrentScreenPosition());
                return;
            }

            nextConfirmTime = Time.unscaledTime + confirmCooldown;
            ItemConfirmed?.Invoke(CurrentSelection);
        }

        public void SelectNearestFrom(Vector2 screenPosition)
        {
            SelectableItem nearest = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < items.Count; i++)
            {
                SelectableItem item = items[i];
                if (!IsValidCandidate(item))
                {
                    continue;
                }

                Vector3 candidatePosition = GetScreenPosition(item);
                float distance = Vector2.Distance(screenPosition, candidatePosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = item;
                }
            }

            SetCurrentSelection(nearest);
        }

        public void ClearSelection(SelectionDeselectReason deselectReason = SelectionDeselectReason.Normal)
        {
            SetCurrentSelection(null, deselectReason);
        }

        public void ResetController()
        {
            DisableInput();
            ClearSelection();
            items.Clear();
            pendingDefaultSelection = null;
            nextNavigationTime = 0f;
            nextConfirmTime = 0f;
        }

        private SelectableItem FindDirectionalCandidate(Vector2 inputDirection)
        {
            if (selectionCamera == null)
            {
                selectionCamera = Camera.main;
            }

            if (selectionCamera == null)
            {
                Debug.LogWarning("[Selection] Selection camera is not assigned.", this);
                return null;
            }

            Vector3 currentScreen = GetScreenPosition(CurrentSelection);
            SelectableItem bestItem = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < items.Count; i++)
            {
                SelectableItem candidate = items[i];
                if (candidate == CurrentSelection || !IsValidCandidate(candidate))
                {
                    continue;
                }

                Vector3 candidateScreen = GetScreenPosition(candidate);
                if (candidateScreen.z <= 0f)
                {
                    continue;
                }

                Vector2 offset = (Vector2)(candidateScreen - currentScreen);
                float distance = offset.magnitude;
                if (distance <= Mathf.Epsilon)
                {
                    continue;
                }

                float dot = Vector2.Dot(offset.normalized, inputDirection);
                if (dot < minimumDirectionDot)
                {
                    continue;
                }

                float normalizedDistance = distance / Mathf.Max(Mathf.Max(Screen.width, Screen.height), 1);
                float score = ((1f - dot) * angleWeight) + (normalizedDistance * distanceWeight);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestItem = candidate;
                }
            }

            return bestItem;
        }

        private SelectableItem FindFirstValidItem()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (IsValidCandidate(items[i]))
                {
                    return items[i];
                }
            }

            return null;
        }

        private bool IsValidCandidate(SelectableItem item)
        {
            return item != null &&
                   item.isActiveAndEnabled &&
                   item.gameObject.activeInHierarchy &&
                   item.IsSelectable &&
                   !item.IsResolved;
        }

        private Vector3 GetScreenPosition(SelectableItem item)
        {
            Camera cameraToUse = selectionCamera != null ? selectionCamera : Camera.main;
            return cameraToUse != null
                ? cameraToUse.WorldToScreenPoint(item.SelectionPoint.position)
                : item.SelectionPoint.position;
        }

        private Vector2 GetCurrentScreenPosition()
        {
            if (CurrentSelection == null)
            {
                return Vector2.zero;
            }

            return GetScreenPosition(CurrentSelection);
        }

        private void SetCurrentSelection(
            SelectableItem item,
            SelectionDeselectReason deselectReason = SelectionDeselectReason.Normal)
        {
            if (CurrentSelection == item)
            {
                return;
            }

            if (CurrentSelection != null)
            {
                CurrentSelection.SetSelected(false, deselectReason);
            }

            CurrentSelection = item;

            if (CurrentSelection != null)
            {
                CurrentSelection.SetSelected(true);
            }
        }
    }
}
