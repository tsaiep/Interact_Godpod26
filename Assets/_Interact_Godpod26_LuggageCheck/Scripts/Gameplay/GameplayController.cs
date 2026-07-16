using System;
using System.Collections.Generic;
using System.Collections;
using RFIDBaggage.Core;
using RFIDBaggage.Levels;
using RFIDBaggage.Selection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RFIDBaggage.Gameplay
{
    public sealed class GameplayController : MonoBehaviour
    {
        [Serializable]
        public sealed class ContrabandSlot
        {
            [SerializeField] private UnityEvent onCompleted = new UnityEvent();
            [SerializeField] private UnityEvent onReset = new UnityEvent();

            public bool IsCompleted { get; private set; }

            public void SetCompleted()
            {
                if (IsCompleted)
                {
                    return;
                }

                IsCompleted = true;
                onCompleted.Invoke();
            }

            public void ResetSlot()
            {
                IsCompleted = false;
                onReset.Invoke();
            }
        }

        [Serializable]
        public sealed class LevelGameplayView
        {
            [SerializeField, Tooltip("Must match LevelConfig.LevelId.")]
            private string levelId;

            [SerializeField, Tooltip("Root object containing selectable items for this level.")]
            private GameObject levelRoot;

            [SerializeField, Tooltip("Preferred default selected item. If empty, an item marked Default Selected is used.")]
            private SelectableItem defaultItem;

            [SerializeField, Tooltip("Contraband prompt slots for this level.")]
            private ContrabandSlot[] contrabandSlots = Array.Empty<ContrabandSlot>();

            public string LevelId => levelId;
            public GameObject LevelRoot => levelRoot;
            public SelectableItem DefaultItem => defaultItem;
            public IReadOnlyList<ContrabandSlot> ContrabandSlots => contrabandSlots;
        }

        [Header("References")]
        [SerializeField, Tooltip("Main state machine.")]
        private GameFlowManager gameFlowManager;

        [SerializeField, Tooltip("Selection input controller.")]
        private ItemSelectionController selectionController;

        [Header("Countdown UI")]
        [SerializeField, Tooltip("Optional countdown text. Uses legacy UI Text to avoid requiring TextMeshPro.")]
        private Text countdownText;

        [SerializeField, Tooltip("Optional countdown radial or bar fill image.")]
        private Image countdownFill;

        [SerializeField, Tooltip("Optional countdown animator for warning/reset triggers.")]
        private Animator countdownAnimator;

        [SerializeField, Tooltip("Optional gameplay UI root. Countdown UI may live here.")]
        private GameObject gameplayUiRoot;

        [Header("Levels")]
        [SerializeField, Tooltip("Scene-level gameplay views. Use these for scene object references.")]
        private LevelGameplayView[] levelViews = Array.Empty<LevelGameplayView>();

        [Header("Unity Events")]
        [SerializeField] private UnityEvent onGameplayStarted = new UnityEvent();
        [SerializeField] private UnityEvent onCountdownWarning = new UnityEvent();
        [SerializeField] private UnityEvent onCorrectSelection = new UnityEvent();
        [SerializeField] private UnityEvent onWrongSelection = new UnityEvent();
        [SerializeField] private UnityEvent onGameSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onGameFailure = new UnityEvent();
        [SerializeField] private UnityEvent onGameplayReset = new UnityEvent();

        private readonly List<SelectableItem> currentItems = new List<SelectableItem>();
        private LevelGameplayView currentView;
        private int totalContrabandCount;
        private int foundContrabandCount;
        private float remainingTime;
        private float gameplayDuration;
        private float warningStartTime;
        private bool gameplayRunning;
        private bool resultReported;
        private bool warningTriggered;
        private Coroutine gameplayStartDelayCoroutine;

        public float RemainingTime => remainingTime;
        public int TotalContrabandCount => totalContrabandCount;
        public int FoundContrabandCount => foundContrabandCount;
        public bool GameplayRunning => gameplayRunning;

        private void OnEnable()
        {
            if (gameFlowManager != null)
            {
                gameFlowManager.StateChanged += HandleStateChanged;
            }

            if (selectionController != null)
            {
                selectionController.ItemConfirmed += HandleItemConfirmed;
            }
        }

        private void OnDisable()
        {
            if (gameFlowManager != null)
            {
                gameFlowManager.StateChanged -= HandleStateChanged;
            }

            if (selectionController != null)
            {
                selectionController.ItemConfirmed -= HandleItemConfirmed;
            }
        }

        private void Update()
        {
            if (!gameplayRunning || resultReported)
            {
                return;
            }

            remainingTime = Mathf.Max(remainingTime - Time.deltaTime, 0f);
            UpdateCountdownUi();

            if (!warningTriggered && remainingTime <= warningStartTime)
            {
                warningTriggered = true;
                Debug.Log($"[Countdown] Warning started at {warningStartTime} seconds.", this);
                onCountdownWarning.Invoke();

                if (countdownAnimator != null)
                {
                    countdownAnimator.SetTrigger("Warning");
                }
            }

            if (remainingTime <= 0f)
            {
                Debug.Log("[Gameplay] Time expired. Failure.", this);
                ReportFailure();
            }
        }

        private void HandleStateChanged(GameState previousState, GameState nextState)
        {
            switch (nextState)
            {
                case GameState.Gameplay:
                    BeginGameplay();
                    break;
                case GameState.SuccessPreparing:
                case GameState.FailurePreparing:
                    StopGameplayInput();
                    break;
                case GameState.ErrorRecovery:
                    StopGameplayInput();
                    gameplayRunning = false;
                    break;
                case GameState.Resetting:
                    ResetGameplay();
                    break;
            }
        }

        private void BeginGameplay()
        {
            LevelConfig level = gameFlowManager.CurrentLevel;
            if (level == null)
            {
                FailInitialization("Gameplay entered without CurrentLevel.");
                return;
            }

            Debug.Log($"[Gameplay] Initializing {level.LevelId}", this);

            currentView = FindView(level);
            GameObject levelRoot = currentView != null ? currentView.LevelRoot : level.LevelRoot;
            if (levelRoot == null)
            {
                FailInitialization($"No level root configured for {level.LevelId}.");
                return;
            }

            SetLevelRootsActive(level.LevelId);
            levelRoot.SetActive(true);

            currentItems.Clear();
            SelectableItem[] foundItems = levelRoot.GetComponentsInChildren<SelectableItem>(true);
            for (int i = 0; i < foundItems.Length; i++)
            {
                SelectableItem item = foundItems[i];
                if (item != null && !currentItems.Contains(item))
                {
                    item.ResetItem();
                    currentItems.Add(item);
                }
            }

            totalContrabandCount = CountContraband(currentItems);
            foundContrabandCount = 0;
            gameplayDuration = Mathf.Max(0.1f, level.GameplayDuration);
            warningStartTime = Mathf.Max(0f, level.WarningStartTime);
            remainingTime = gameplayDuration;
            resultReported = false;
            warningTriggered = false;

            ResetSlots();
            UpdateCountdownUi();

            if (gameplayUiRoot != null)
            {
                gameplayUiRoot.SetActive(true);
            }

            if (currentItems.Count == 0 || totalContrabandCount == 0)
            {
                FailInitialization($"Invalid gameplay data for {level.LevelId}. Items: {currentItems.Count}, Contraband: {totalContrabandCount}");
                return;
            }

            if (!ValidateContrabandSlots())
            {
                return;
            }

            SelectableItem defaultItem = ResolveDefaultItem(currentView);
            selectionController.SetConfirmKey(gameFlowManager.ConfirmKey);
            selectionController.Initialize(currentItems, defaultItem, level.SelectionInputCooldown, level.ConfirmInputCooldown);
            selectionController.DisableInput();

            Debug.Log($"[Gameplay] Items: {currentItems.Count}, Contraband: {totalContrabandCount}", this);

            StopCoroutineIfRunning(ref gameplayStartDelayCoroutine);
            float startDelay = Mathf.Max(0f, level.GameplayStartDelay);
            if (startDelay > 0f)
            {
                Debug.Log($"[Gameplay] Start delay: {startDelay:0.###} seconds.", this);
                gameplayStartDelayCoroutine = StartCoroutine(GameplayStartDelayRoutine(startDelay));
            }
            else
            {
                StartGameplayTimerAndInput();
            }
        }

        private IEnumerator GameplayStartDelayRoutine(float delay)
        {
            float startTime = Time.unscaledTime;

            while (gameFlowManager.CurrentState == GameState.Gameplay &&
                   !resultReported &&
                   Time.unscaledTime - startTime < delay)
            {
                yield return null;
            }

            gameplayStartDelayCoroutine = null;

            if (gameFlowManager.CurrentState != GameState.Gameplay || resultReported)
            {
                yield break;
            }

            StartGameplayTimerAndInput();
        }

        private void StartGameplayTimerAndInput()
        {
            gameplayRunning = true;

            if (selectionController != null)
            {
                selectionController.SelectInitialItem();
                selectionController.EnableInput();
            }

            onGameplayStarted.Invoke();
        }

        private void HandleItemConfirmed(SelectableItem item)
        {
            if (!gameplayRunning || resultReported || gameFlowManager.CurrentState != GameState.Gameplay)
            {
                return;
            }

            if (remainingTime <= 0f)
            {
                ReportFailure();
                return;
            }

            if (item == null || !item.IsSelectable || item.IsResolved)
            {
                return;
            }

            if (item.IsContraband)
            {
                HandleCorrectItem(item);
            }
            else
            {
                HandleWrongItem(item);
            }
        }

        private void HandleCorrectItem(SelectableItem item)
        {
            Vector2 previousPosition = GetItemScreenPosition(item);
            item.MarkCorrect();
            foundContrabandCount++;

            Debug.Log($"[Gameplay] Correct: {item.ItemId}", this);
            Debug.Log($"[Gameplay] Found: {foundContrabandCount} / {totalContrabandCount}", this);

            IReadOnlyList<ContrabandSlot> slots = GetCurrentSlots();
            if (item.ContrabandSlotIndex >= 0 && item.ContrabandSlotIndex < slots.Count)
            {
                slots[item.ContrabandSlotIndex].SetCompleted();
            }

            onCorrectSelection.Invoke();

            if (foundContrabandCount >= totalContrabandCount)
            {
                Debug.Log("[Gameplay] All contraband found. Success.", this);
                ReportSuccess();
                return;
            }

            selectionController.SelectNearestFrom(previousPosition);
        }

        private void HandleWrongItem(SelectableItem item)
        {
            LevelConfig level = gameFlowManager.CurrentLevel;
            item.PlayWrongFeedback();
            Debug.Log($"[Gameplay] Wrong: {item.ItemId}", this);
            onWrongSelection.Invoke();

            if (level != null && level.WrongSelectionDeductsTime)
            {
                remainingTime = Mathf.Max(remainingTime - level.WrongSelectionTimePenalty, 0f);
                UpdateCountdownUi();

                if (remainingTime <= 0f)
                {
                    ReportFailure();
                }
            }
        }

        private void ReportSuccess()
        {
            if (resultReported)
            {
                return;
            }

            resultReported = true;
            gameplayRunning = false;
            StopGameplayInput();
            onGameSuccess.Invoke();
            gameFlowManager.NotifyGameSuccess();
        }

        private void ReportFailure()
        {
            if (resultReported)
            {
                return;
            }

            resultReported = true;
            gameplayRunning = false;
            remainingTime = 0f;
            UpdateCountdownUi();
            StopGameplayInput();
            onGameFailure.Invoke();
            gameFlowManager.NotifyGameFailure();
        }

        private void StopGameplayInput()
        {
            if (selectionController != null)
            {
                selectionController.DisableInput();
                selectionController.ClearSelection();
            }

            StopCoroutineIfRunning(ref gameplayStartDelayCoroutine);
        }

        private void ResetGameplay()
        {
            gameplayRunning = false;
            resultReported = false;
            warningTriggered = false;
            StopCoroutineIfRunning(ref gameplayStartDelayCoroutine);
            foundContrabandCount = 0;
            totalContrabandCount = 0;
            remainingTime = 0f;

            if (selectionController != null)
            {
                selectionController.ResetController();
            }

            for (int i = 0; i < currentItems.Count; i++)
            {
                if (currentItems[i] != null)
                {
                    currentItems[i].ResetItem();
                }
            }

            ResetSlots();

            if (gameplayUiRoot != null)
            {
                gameplayUiRoot.SetActive(false);
            }

            if (countdownAnimator != null)
            {
                countdownAnimator.Rebind();
                countdownAnimator.Update(0f);
            }

            currentItems.Clear();
            currentView = null;
            SetLevelRootsActive(string.Empty);
            onGameplayReset.Invoke();
            Debug.Log("[Gameplay] Reset completed.", this);
        }

        private void StopCoroutineIfRunning(ref Coroutine coroutine)
        {
            if (coroutine == null)
            {
                return;
            }

            StopCoroutine(coroutine);
            coroutine = null;
        }

        private void UpdateCountdownUi()
        {
            if (countdownText != null)
            {
                countdownText.text = Mathf.CeilToInt(remainingTime).ToString();
            }

            if (countdownFill != null)
            {
                countdownFill.fillAmount = gameplayDuration > 0f ? remainingTime / gameplayDuration : 0f;
            }
        }

        private LevelGameplayView FindView(LevelConfig level)
        {
            if (level == null)
            {
                return null;
            }

            for (int i = 0; i < levelViews.Length; i++)
            {
                LevelGameplayView view = levelViews[i];
                if (view != null && string.Equals(view.LevelId, level.LevelId, StringComparison.Ordinal))
                {
                    return view;
                }
            }

            return null;
        }

        private void SetLevelRootsActive(string activeLevelId)
        {
            for (int i = 0; i < levelViews.Length; i++)
            {
                LevelGameplayView view = levelViews[i];
                if (view == null || view.LevelRoot == null)
                {
                    continue;
                }

                view.LevelRoot.SetActive(string.Equals(view.LevelId, activeLevelId, StringComparison.Ordinal));
            }
        }

        private SelectableItem ResolveDefaultItem(LevelGameplayView view)
        {
            if (view != null && view.DefaultItem != null && currentItems.Contains(view.DefaultItem))
            {
                return view.DefaultItem;
            }

            for (int i = 0; i < currentItems.Count; i++)
            {
                if (currentItems[i] != null && currentItems[i].DefaultSelected)
                {
                    return currentItems[i];
                }
            }

            return currentItems.Count > 0 ? currentItems[0] : null;
        }

        private IReadOnlyList<ContrabandSlot> GetCurrentSlots()
        {
            return currentView != null ? currentView.ContrabandSlots : Array.Empty<ContrabandSlot>();
        }

        private void ResetSlots()
        {
            IReadOnlyList<ContrabandSlot> slots = GetCurrentSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i]?.ResetSlot();
            }
        }

        private bool ValidateContrabandSlots()
        {
            IReadOnlyList<ContrabandSlot> slots = GetCurrentSlots();

            for (int i = 0; i < currentItems.Count; i++)
            {
                SelectableItem item = currentItems[i];
                if (item == null || !item.IsContraband)
                {
                    continue;
                }

                if (item.ContrabandSlotIndex < 0 || item.ContrabandSlotIndex >= slots.Count)
                {
                    FailInitialization($"Contraband item {item.ItemId} has invalid slot index {item.ContrabandSlotIndex}.");
                    return false;
                }
            }

            return true;
        }

        private int CountContraband(IReadOnlyList<SelectableItem> items)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].IsContraband)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector2 GetItemScreenPosition(SelectableItem item)
        {
            if (item == null || Camera.main == null)
            {
                return Vector2.zero;
            }

            return Camera.main.WorldToScreenPoint(item.SelectionPoint.position);
        }

        private void FailInitialization(string message)
        {
            Debug.LogWarning($"[Gameplay] {message}", this);
            gameplayRunning = false;
            resultReported = true;
            StopGameplayInput();
            gameFlowManager.ReportRecoverableError(message);
        }
    }
}
