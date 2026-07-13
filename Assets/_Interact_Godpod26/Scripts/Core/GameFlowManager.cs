using System;
using RFIDBaggage.Levels;
using UnityEngine;

namespace RFIDBaggage.Core
{
    public sealed class GameFlowManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Database used to map RFID IDs to level configs.")]
        private LevelDatabase levelDatabase;

        [SerializeField, Tooltip("When enabled, the flow automatically enters Idle on Start.")]
        private bool enterIdleOnStart = true;

        private GameState currentState = GameState.SystemInitializing;
        private LevelConfig currentLevel;
        private bool resultLocked;

        public GameState CurrentState => currentState;
        public LevelConfig CurrentLevel => currentLevel;

        public event Action<GameState, GameState> StateChanged;
        public event Action<LevelConfig> LevelStarted;
        public event Action<LevelConfig> GameplayStarted;
        public event Action<LevelConfig, bool> LevelFinished;
        public event Action LevelReset;

        private void Start()
        {
            if (!enterIdleOnStart)
            {
                return;
            }

            TransitionTo(GameState.IdlePreparing);
            TransitionTo(GameState.Idle);
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

            LevelStarted?.Invoke(level);
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
            if (NotifyExpected(GameState.GamePreparing, GameState.Gameplay, "game prepared"))
            {
                GameplayStarted?.Invoke(currentLevel);
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
            LevelFinished?.Invoke(currentLevel, success);
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
                LevelReset?.Invoke();
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
            LevelReset?.Invoke();

            TransitionTo(GameState.IdlePreparing);
            TransitionTo(GameState.Idle);
            Debug.Log("[GameFlow] Reset complete. Returned to Idle.", this);
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
            Debug.Log($"[GameFlow] {previousState} -> {nextState}", this);
            StateChanged?.Invoke(previousState, nextState);
            return true;
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
