using System;
using System.Collections.Generic;
using RFIDBaggage.Levels;
using UnityEngine;
using UnityEngine.Events;

namespace RFIDBaggage.Core
{
    [Serializable]
    public sealed class GameStateUnityEvent : UnityEvent<GameState>
    {
    }

    public enum GameFlowConfirmKey
    {
        Space,
        Enter
    }

    [Serializable]
    public sealed class GameFlowStateEvents
    {
        [SerializeField] private GameStateUnityEvent onStateEntered = new GameStateUnityEvent();
        [SerializeField] private UnityEvent onSystemInitializingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onIdlePreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onIdleEntered = new UnityEvent();
        [SerializeField] private UnityEvent onLevelInitializingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onIntroPreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onIntroPlayingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onGamePreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onGameplayStartPendingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onGameplayEntered = new UnityEvent();
        [SerializeField] private UnityEvent onSuccessPreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onSuccessPlayingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onFailurePreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onFailurePlayingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onResettingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onErrorRecoveryEntered = new UnityEvent();

        public void Invoke(GameState state)
        {
            InvokeSafely(() => onStateEntered.Invoke(state), $"onStateEntered({state})");

            switch (state)
            {
                case GameState.SystemInitializing:
                    InvokeSafely(onSystemInitializingEntered.Invoke, nameof(onSystemInitializingEntered));
                    break;
                case GameState.IdlePreparing:
                    InvokeSafely(onIdlePreparingEntered.Invoke, nameof(onIdlePreparingEntered));
                    break;
                case GameState.Idle:
                    InvokeSafely(onIdleEntered.Invoke, nameof(onIdleEntered));
                    break;
                case GameState.LevelInitializing:
                    InvokeSafely(onLevelInitializingEntered.Invoke, nameof(onLevelInitializingEntered));
                    break;
                case GameState.IntroPreparing:
                    InvokeSafely(onIntroPreparingEntered.Invoke, nameof(onIntroPreparingEntered));
                    break;
                case GameState.IntroPlaying:
                    InvokeSafely(onIntroPlayingEntered.Invoke, nameof(onIntroPlayingEntered));
                    break;
                case GameState.GamePreparing:
                    InvokeSafely(onGamePreparingEntered.Invoke, nameof(onGamePreparingEntered));
                    break;
                case GameState.GameplayStartPending:
                    InvokeSafely(onGameplayStartPendingEntered.Invoke, nameof(onGameplayStartPendingEntered));
                    break;
                case GameState.Gameplay:
                    InvokeSafely(onGameplayEntered.Invoke, nameof(onGameplayEntered));
                    break;
                case GameState.SuccessPreparing:
                    InvokeSafely(onSuccessPreparingEntered.Invoke, nameof(onSuccessPreparingEntered));
                    break;
                case GameState.SuccessPlaying:
                    InvokeSafely(onSuccessPlayingEntered.Invoke, nameof(onSuccessPlayingEntered));
                    break;
                case GameState.FailurePreparing:
                    InvokeSafely(onFailurePreparingEntered.Invoke, nameof(onFailurePreparingEntered));
                    break;
                case GameState.FailurePlaying:
                    InvokeSafely(onFailurePlayingEntered.Invoke, nameof(onFailurePlayingEntered));
                    break;
                case GameState.Resetting:
                    InvokeSafely(onResettingEntered.Invoke, nameof(onResettingEntered));
                    break;
                case GameState.ErrorRecovery:
                    InvokeSafely(onErrorRecoveryEntered.Invoke, nameof(onErrorRecoveryEntered));
                    break;
            }
        }

        private static void InvokeSafely(Action action, string eventName)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new Exception($"[GameFlow] UnityEvent failed: {eventName}", exception));
            }
        }
    }

    public sealed class GameFlowManager : MonoBehaviour
    {
        private static readonly GameState[] StateSequence =
        {
            GameState.SystemInitializing,
            GameState.IdlePreparing,
            GameState.Idle,
            GameState.LevelInitializing,
            GameState.IntroPreparing,
            GameState.IntroPlaying,
            GameState.GamePreparing,
            GameState.GameplayStartPending,
            GameState.Gameplay,
            GameState.SuccessPreparing,
            GameState.SuccessPlaying,
            GameState.FailurePreparing,
            GameState.FailurePlaying,
            GameState.Resetting,
            GameState.ErrorRecovery
        };

        private static readonly IReadOnlyList<GameState> ReadOnlyStateSequence = Array.AsReadOnly(StateSequence);

        [SerializeField, Tooltip("Database used to map RFID IDs to level configs.")]
        private LevelDatabase levelDatabase;

        [SerializeField, Tooltip("When enabled, the flow automatically enters Idle on Start.")]
        private bool enterIdleOnStart = true;

        [Header("Input")]
        [SerializeField, Tooltip("Key used to confirm gameplay selections. Enter accepts both Return and Keypad Enter.")]
        private GameFlowConfirmKey confirmKey = GameFlowConfirmKey.Space;

        [Header("Gameplay Start Gate")]
        [SerializeField, Tooltip("When enabled, the flow waits after gameplay content is prepared until the confirm key is held.")]
        private bool requireGameplayStartConfirmHold = true;

        [SerializeField, Min(0f), Tooltip("Seconds the confirm key must be held before Gameplay starts.")]
        private float gameplayStartConfirmHoldSeconds = 2f;

        [SerializeField, Tooltip("Invoked once when the confirm key is pressed down in GameplayStartPending.")]
        private UnityEvent onGameplayStartConfirmKeyDown = new UnityEvent();

        [SerializeField, Tooltip("Invoked once when the confirm key is released in GameplayStartPending.")]
        private UnityEvent onGameplayStartConfirmKeyUp = new UnityEvent();

        [SerializeField, Tooltip("Invoked once when the confirm key has been held long enough to enter Gameplay.")]
        private UnityEvent onGameplayStartConfirmHoldCompleted = new UnityEvent();

        [Header("Runtime Debug")]
        [SerializeField, Tooltip("All flow states for Inspector display only. This list is not used to drive state transitions.")]
        private GameState[] visibleStateSequence = (GameState[])StateSequence.Clone();

        [SerializeField, Tooltip("Current flow state. Runtime display only; do not edit during Play Mode.")]
        private GameState currentState = GameState.SystemInitializing;

        [Header("State Unity Events")]
        [SerializeField, Tooltip("Inspector events invoked after a legal state transition.")]
        private GameFlowStateEvents stateEvents = new GameFlowStateEvents();

        private LevelConfig currentLevel;
        private bool resultLocked;
        private bool logResetCompleteWhenIdle;
        private float gameplayStartConfirmHeldSeconds;
        private bool gameplayStartConfirmHoldCompleted;

        public IReadOnlyList<GameState> AllStates => ReadOnlyStateSequence;
        public GameState CurrentState => currentState;
        public LevelConfig CurrentLevel => currentLevel;
        public GameFlowConfirmKey ConfirmKey => confirmKey;
        public bool RequireGameplayStartConfirmHold => requireGameplayStartConfirmHold;
        public float GameplayStartConfirmHoldSeconds => gameplayStartConfirmHoldSeconds;
        public float GameplayStartConfirmHeldSeconds => gameplayStartConfirmHeldSeconds;
        public float GameplayStartConfirmHoldProgress => gameplayStartConfirmHoldSeconds > 0f
            ? Mathf.Clamp01(gameplayStartConfirmHeldSeconds / gameplayStartConfirmHoldSeconds)
            : 1f;

        public event Action<GameState, GameState> StateChanged;
        public event Action<LevelConfig> LevelStarted;
        public event Action<LevelConfig> GameplayStarted;
        public event Action GameplayStartConfirmKeyDown;
        public event Action GameplayStartConfirmKeyUp;
        public event Action GameplayStartConfirmHoldCompleted;
        public event Action<LevelConfig, bool> LevelFinished;
        public event Action LevelReset;

        public bool IsConfirmKeyDown()
        {
            return IsConfirmKeyDown(confirmKey);
        }

        public static bool IsConfirmKeyDown(GameFlowConfirmKey key)
        {
            switch (key)
            {
                case GameFlowConfirmKey.Enter:
                    return UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter);
                case GameFlowConfirmKey.Space:
                default:
                    return UnityEngine.Input.GetKeyDown(KeyCode.Space);
            }
        }

        public bool IsConfirmKeyUp()
        {
            return IsConfirmKeyUp(confirmKey);
        }

        public static bool IsConfirmKeyUp(GameFlowConfirmKey key)
        {
            switch (key)
            {
                case GameFlowConfirmKey.Enter:
                    return UnityEngine.Input.GetKeyUp(KeyCode.Return) || UnityEngine.Input.GetKeyUp(KeyCode.KeypadEnter);
                case GameFlowConfirmKey.Space:
                default:
                    return UnityEngine.Input.GetKeyUp(KeyCode.Space);
            }
        }

        public bool IsConfirmKeyPressed()
        {
            return IsConfirmKeyPressed(confirmKey);
        }

        public static bool IsConfirmKeyPressed(GameFlowConfirmKey key)
        {
            switch (key)
            {
                case GameFlowConfirmKey.Enter:
                    return UnityEngine.Input.GetKey(KeyCode.Return) || UnityEngine.Input.GetKey(KeyCode.KeypadEnter);
                case GameFlowConfirmKey.Space:
                default:
                    return UnityEngine.Input.GetKey(KeyCode.Space);
            }
        }

        private void OnValidate()
        {
            visibleStateSequence = (GameState[])StateSequence.Clone();
            gameplayStartConfirmHoldSeconds = Mathf.Max(0f, gameplayStartConfirmHoldSeconds);
        }

        private void Start()
        {
            if (!enterIdleOnStart)
            {
                return;
            }

            TransitionTo(GameState.IdlePreparing);
        }

        private void Update()
        {
            if (currentState == GameState.GameplayStartPending)
            {
                UpdateGameplayStartConfirmHold();
            }
        }

        public void NotifyIdlePrepared()
        {
            NotifyExpected(GameState.IdlePreparing, GameState.Idle, "idle prepared");
        }

        public bool RequestStartLevelByRfid(string rfidId)
        {
            if (levelDatabase == null)
            {
                Debug.LogWarning("[GameFlow] Cannot start level by RFID because LevelDatabase is not assigned.", this);
                return false;
            }

            if (!levelDatabase.TryGetByRfidId(rfidId, out LevelConfig level))
            {
                Debug.LogWarning($"[GameFlow] No level found for RFID: {LevelConfig.NormalizeIdentifier(rfidId)}", this);
                return false;
            }

            return RequestStartLevel(level);
        }

        public bool RequestStartLevel(LevelConfig level)
        {
            if (currentState != GameState.Idle)
            {
                Debug.LogWarning($"[GameFlow] Cannot start a new level while state is {currentState}.", this);
                return false;
            }

            if (level == null)
            {
                Debug.LogWarning("[GameFlow] Cannot start a null level.", this);
                return false;
            }

            if (!level.IsValid(out string validationMessage))
            {
                Debug.LogWarning($"[GameFlow] Cannot start invalid level. {validationMessage}", level);
                return false;
            }

            currentLevel = level;
            resultLocked = false;

            Debug.Log($"[GameFlow] Starting {level.LevelId}, RFID: {level.RfidId}", this);
            if (!TransitionTo(GameState.LevelInitializing))
            {
                currentLevel = null;
                return false;
            }

            InvokeLevelStarted(level);
            return true;
        }

        public void NotifyLevelInitialized()
        {
            NotifyExpected(GameState.LevelInitializing, GameState.IntroPreparing, "level initialized");
        }

        public void NotifyIntroPrepared()
        {
            NotifyExpected(GameState.IntroPreparing, GameState.IntroPlaying, "intro prepared");
        }

        public void NotifyIntroCompleted()
        {
            NotifyExpected(GameState.IntroPlaying, GameState.GamePreparing, "intro completed");
        }

        public void NotifyGamePrepared()
        {
            if (currentState != GameState.GamePreparing)
            {
                Debug.LogWarning($"[GameFlow] Ignored game prepared notification while state is {currentState}. Expected {GameState.GamePreparing}.", this);
                return;
            }

            if (requireGameplayStartConfirmHold)
            {
                if (TransitionTo(GameState.GameplayStartPending) && gameplayStartConfirmHoldSeconds <= 0f)
                {
                    CompleteGameplayStartConfirmHold();
                }

                return;
            }

            if (TransitionTo(GameState.Gameplay))
            {
                InvokeGameplayStarted(currentLevel);
            }
        }

        public void NotifyGameSuccess()
        {
            NotifyGameResult(true);
        }

        public void NotifyGameFailure()
        {
            NotifyGameResult(false);
        }

        public void NotifyResultPrepared()
        {
            if (currentState == GameState.SuccessPreparing)
            {
                TransitionTo(GameState.SuccessPlaying);
                return;
            }

            if (currentState == GameState.FailurePreparing)
            {
                TransitionTo(GameState.FailurePlaying);
                return;
            }

            Debug.LogWarning($"[GameFlow] Result prepared notification is not valid while state is {currentState}.", this);
        }

        public void NotifyResultCompleted()
        {
            if (currentState != GameState.SuccessPlaying && currentState != GameState.FailurePlaying)
            {
                Debug.LogWarning($"[GameFlow] Result completed notification is not valid while state is {currentState}.", this);
                return;
            }

            ResetAndReturnToIdle();
        }

        public void ReportRecoverableError(string message)
        {
            Debug.LogWarning($"[GameFlow] Recoverable error: {message}", this);

            if (currentState != GameState.ErrorRecovery)
            {
                TransitionTo(GameState.ErrorRecovery);
            }

            ResetAndReturnToIdle();
        }

        public void ReturnToIdle()
        {
            ResetAndReturnToIdle();
        }

        private void NotifyGameResult(bool success)
        {
            if (currentState != GameState.Gameplay)
            {
                Debug.LogWarning($"[GameFlow] Cannot report {(success ? "success" : "failure")} while state is {currentState}.", this);
                return;
            }

            if (resultLocked)
            {
                Debug.LogWarning("[GameFlow] Game result has already been reported.", this);
                return;
            }

            resultLocked = true;
            string result = success ? "Success" : "Failure";
            string levelId = currentLevel != null ? currentLevel.LevelId : "<none>";

            Debug.Log($"[GameFlow] {levelId} Result: {result}", this);
            InvokeLevelFinished(currentLevel, success);
            TransitionTo(success ? GameState.SuccessPreparing : GameState.FailurePreparing);
        }

        private bool NotifyExpected(GameState expectedCurrentState, GameState nextState, string notificationName)
        {
            if (currentState != expectedCurrentState)
            {
                Debug.LogWarning($"[GameFlow] Ignored {notificationName} notification while state is {currentState}. Expected {expectedCurrentState}.", this);
                return false;
            }

            return TransitionTo(nextState);
        }

        private void UpdateGameplayStartConfirmHold()
        {
            if (gameplayStartConfirmHoldCompleted)
            {
                return;
            }

            if (IsConfirmKeyDown())
            {
                InvokeGameplayStartConfirmKeyDown();
            }

            if (IsConfirmKeyUp())
            {
                InvokeGameplayStartConfirmKeyUp();
                ResetGameplayStartConfirmHold();
                return;
            }

            if (!IsConfirmKeyPressed())
            {
                ResetGameplayStartConfirmHold();
                return;
            }

            gameplayStartConfirmHeldSeconds = Mathf.Min(
                gameplayStartConfirmHeldSeconds + Time.unscaledDeltaTime,
                gameplayStartConfirmHoldSeconds);

            if (gameplayStartConfirmHeldSeconds >= gameplayStartConfirmHoldSeconds)
            {
                CompleteGameplayStartConfirmHold();
            }
        }

        private void CompleteGameplayStartConfirmHold()
        {
            if (currentState != GameState.GameplayStartPending || gameplayStartConfirmHoldCompleted)
            {
                return;
            }

            gameplayStartConfirmHoldCompleted = true;
            gameplayStartConfirmHeldSeconds = gameplayStartConfirmHoldSeconds;
            InvokeGameplayStartConfirmHoldCompleted();

            if (currentState != GameState.GameplayStartPending)
            {
                return;
            }

            if (TransitionTo(GameState.Gameplay))
            {
                InvokeGameplayStarted(currentLevel);
            }
        }

        private void ResetGameplayStartConfirmHold()
        {
            gameplayStartConfirmHeldSeconds = 0f;
        }

        private void ResetAndReturnToIdle()
        {
            if (currentState == GameState.Resetting)
            {
                Debug.LogWarning("[GameFlow] Reset is already in progress.", this);
                return;
            }

            if (currentState == GameState.Idle)
            {
                currentLevel = null;
                resultLocked = false;
                InvokeLevelReset();
                Debug.Log("[GameFlow] Reset complete. Returned to Idle.", this);
                return;
            }

            if (currentState != GameState.ErrorRecovery && !IsLegalTransition(currentState, GameState.Resetting))
            {
                TransitionTo(GameState.ErrorRecovery);
            }

            if (!TransitionTo(GameState.Resetting))
            {
                return;
            }

            currentLevel = null;
            resultLocked = false;
            InvokeLevelReset();

            logResetCompleteWhenIdle = true;
            TransitionTo(GameState.IdlePreparing);
        }

        private bool TransitionTo(GameState nextState)
        {
            if (currentState == nextState)
            {
                Debug.LogWarning($"[GameFlow] Ignored duplicate state transition to {nextState}.", this);
                return false;
            }

            if (!IsLegalTransition(currentState, nextState))
            {
                Debug.LogWarning($"[GameFlow] Illegal state transition: {currentState} -> {nextState}", this);
                return false;
            }

            GameState previousState = currentState;
            currentState = nextState;
            if (nextState == GameState.GameplayStartPending)
            {
                ResetGameplayStartConfirmHoldState();
            }
            else if (previousState == GameState.GameplayStartPending)
            {
                ResetGameplayStartConfirmHold();
            }

            Debug.Log($"[GameFlow] {previousState} -> {nextState}", this);
            InvokeStateChanged(previousState, nextState);
            stateEvents.Invoke(nextState);

            if (nextState == GameState.Idle && previousState == GameState.IdlePreparing && logResetCompleteWhenIdle)
            {
                logResetCompleteWhenIdle = false;
                Debug.Log("[GameFlow] Reset complete. Returned to Idle.", this);
            }

            return true;
        }

        private void InvokeStateChanged(GameState previousState, GameState nextState)
        {
            Action<GameState, GameState> handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<GameState, GameState> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(previousState, nextState);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[GameFlow] StateChanged handler failed.", exception), this);
                }
            }
        }

        private void ResetGameplayStartConfirmHoldState()
        {
            gameplayStartConfirmHoldCompleted = false;
            ResetGameplayStartConfirmHold();
        }

        private void InvokeLevelStarted(LevelConfig level)
        {
            Action<LevelConfig> handlers = LevelStarted;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<LevelConfig> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(level);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[GameFlow] LevelStarted handler failed.", exception), this);
                }
            }
        }

        private void InvokeGameplayStarted(LevelConfig level)
        {
            Action<LevelConfig> handlers = GameplayStarted;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<LevelConfig> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(level);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[GameFlow] GameplayStarted handler failed.", exception), this);
                }
            }
        }

        private void InvokeGameplayStartConfirmKeyDown()
        {
            Action handlers = GameplayStartConfirmKeyDown;
            if (handlers != null)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler.Invoke();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(new Exception("[GameFlow] GameplayStartConfirmKeyDown handler failed.", exception), this);
                    }
                }
            }

            try
            {
                onGameplayStartConfirmKeyDown.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new Exception("[GameFlow] UnityEvent failed: onGameplayStartConfirmKeyDown", exception), this);
            }
        }

        private void InvokeGameplayStartConfirmKeyUp()
        {
            Action handlers = GameplayStartConfirmKeyUp;
            if (handlers != null)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler.Invoke();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(new Exception("[GameFlow] GameplayStartConfirmKeyUp handler failed.", exception), this);
                    }
                }
            }

            try
            {
                onGameplayStartConfirmKeyUp.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new Exception("[GameFlow] UnityEvent failed: onGameplayStartConfirmKeyUp", exception), this);
            }
        }

        private void InvokeGameplayStartConfirmHoldCompleted()
        {
            Action handlers = GameplayStartConfirmHoldCompleted;
            if (handlers != null)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler.Invoke();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(new Exception("[GameFlow] GameplayStartConfirmHoldCompleted handler failed.", exception), this);
                    }
                }
            }

            try
            {
                onGameplayStartConfirmHoldCompleted.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new Exception("[GameFlow] UnityEvent failed: onGameplayStartConfirmHoldCompleted", exception), this);
            }
        }

        private void InvokeLevelFinished(LevelConfig level, bool success)
        {
            Action<LevelConfig, bool> handlers = LevelFinished;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<LevelConfig, bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(level, success);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[GameFlow] LevelFinished handler failed.", exception), this);
                }
            }
        }

        private void InvokeLevelReset()
        {
            Action handlers = LevelReset;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[GameFlow] LevelReset handler failed.", exception), this);
                }
            }
        }

        private static bool IsLegalTransition(GameState from, GameState to)
        {
            if (to == GameState.ErrorRecovery)
            {
                return from != GameState.ErrorRecovery;
            }

            switch (from)
            {
                case GameState.SystemInitializing:
                    return to == GameState.IdlePreparing;
                case GameState.IdlePreparing:
                    return to == GameState.Idle;
                case GameState.Idle:
                    return to == GameState.LevelInitializing;
                case GameState.LevelInitializing:
                    return to == GameState.IntroPreparing;
                case GameState.IntroPreparing:
                    return to == GameState.IntroPlaying;
                case GameState.IntroPlaying:
                    return to == GameState.GamePreparing;
                case GameState.GamePreparing:
                    return to == GameState.GameplayStartPending || to == GameState.Gameplay;
                case GameState.GameplayStartPending:
                    return to == GameState.Gameplay;
                case GameState.Gameplay:
                    return to == GameState.SuccessPreparing || to == GameState.FailurePreparing;
                case GameState.SuccessPreparing:
                    return to == GameState.SuccessPlaying;
                case GameState.SuccessPlaying:
                    return to == GameState.Resetting;
                case GameState.FailurePreparing:
                    return to == GameState.FailurePlaying;
                case GameState.FailurePlaying:
                    return to == GameState.Resetting;
                case GameState.ErrorRecovery:
                    return to == GameState.Resetting;
                case GameState.Resetting:
                    return to == GameState.IdlePreparing;
                default:
                    return false;
            }
        }
    }
}
