using System.Collections;
using RFIDBaggage.Core;
using RFIDBaggage.Levels;
using RFIDBaggage.Presentation;
using RFIDBaggage.Utilities;
using UnityEngine;
using UnityEngine.Events;

namespace RFIDBaggage.Video
{
    public sealed class VideoFlowCoordinator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Main state machine. This coordinator never writes state directly.")]
        private GameFlowManager gameFlowManager;

        [SerializeField, Tooltip("Global video system settings.")]
        private VideoSystemConfig videoSystemConfig;

        [SerializeField, Tooltip("Video preparation and playback manager.")]
        private VideoPlaybackManager videoPlaybackManager;

        [SerializeField, Tooltip("Legacy StreamingAssets image loader for final-frame backgrounds. Gameplay now uses a looping video.")]
        private StreamingImageLoader streamingImageLoader;

        [SerializeField, Tooltip("Display layer switcher.")]
        private VideoTransitionController transitionController;

        [Header("Result Performance Cues")]
        [SerializeField, Tooltip("Invoked once when the success video reaches its configured lead time before ending.")]
        private UnityEvent onSuccessPerformanceCue = new UnityEvent();

        [SerializeField, Tooltip("Invoked once when the failure video reaches its configured lead time before ending.")]
        private UnityEvent onFailurePerformanceCue = new UnityEvent();

        private LevelConfig currentLevel;
        private Coroutine resultPrepareDelayCoroutine;
        private Coroutine resultPerformanceCueCoroutine;

        private void OnEnable()
        {
            if (gameFlowManager != null)
            {
                gameFlowManager.StateChanged += HandleStateChanged;
            }

            if (videoPlaybackManager != null)
            {
                videoPlaybackManager.FirstFrameReady += HandleFirstFrameReady;
                videoPlaybackManager.VideoCompleted += HandleVideoCompleted;
                videoPlaybackManager.VideoFailed += HandleVideoFailed;
            }
        }

        private void OnDisable()
        {
            if (gameFlowManager != null)
            {
                gameFlowManager.StateChanged -= HandleStateChanged;
            }

            if (videoPlaybackManager != null)
            {
                videoPlaybackManager.FirstFrameReady -= HandleFirstFrameReady;
                videoPlaybackManager.VideoCompleted -= HandleVideoCompleted;
                videoPlaybackManager.VideoFailed -= HandleVideoFailed;
            }

            StopCoroutineIfRunning(ref resultPrepareDelayCoroutine);
            StopCoroutineIfRunning(ref resultPerformanceCueCoroutine);
        }

        private void HandleStateChanged(GameState previousState, GameState nextState)
        {
            switch (nextState)
            {
                case GameState.IdlePreparing:
                    PrepareIdle();
                    break;
                case GameState.LevelInitializing:
                    InitializeLevel();
                    break;
                case GameState.IntroPreparing:
                    PrepareIntro();
                    break;
                case GameState.IntroPlaying:
                    PlayIntro();
                    break;
                case GameState.GamePreparing:
                    PrepareGameplay();
                    break;
                case GameState.SuccessPreparing:
                    SchedulePrepareResult(VideoContentType.Success, previousState);
                    break;
                case GameState.FailurePreparing:
                    SchedulePrepareResult(VideoContentType.Failure, previousState);
                    break;
                case GameState.SuccessPlaying:
                    PlayResult(VideoContentType.Success);
                    break;
                case GameState.FailurePlaying:
                    PlayResult(VideoContentType.Failure);
                    break;
                case GameState.Resetting:
                    ResetVideoFlow();
                    break;
            }
        }

        private void PrepareIdle()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            string idlePath = videoSystemConfig.IdleVideoRelativePath;
            if (!ValidateFilePath(idlePath, "Idle video"))
            {
                gameFlowManager.ReportRecoverableError($"Invalid idle video path: {idlePath}");
                return;
            }

            videoPlaybackManager.PrepareIdle(idlePath);
        }

        private void InitializeLevel()
        {
            currentLevel = gameFlowManager.CurrentLevel;

            StopCoroutineIfRunning(ref resultPrepareDelayCoroutine);
            if (streamingImageLoader != null)
            {
                streamingImageLoader.ReleaseCurrentTexture();
            }

            if (currentLevel == null)
            {
                gameFlowManager.ReportRecoverableError("LevelInitializing entered without a current level.");
                return;
            }

            if (!ValidateLevelMedia(currentLevel))
            {
                return;
            }

            gameFlowManager.NotifyLevelInitialized();
        }

        private void PrepareIntro()
        {
            if (currentLevel == null)
            {
                gameFlowManager.ReportRecoverableError("IntroPreparing entered without a current level.");
                return;
            }

            videoPlaybackManager.PrepareIntro(currentLevel.IntroVideoRelativePath);
        }

        private void PlayIntro()
        {
            if (videoPlaybackManager.PlayPrepared(VideoContentType.Intro))
            {
                transitionController.ShowContentVideo(videoPlaybackManager.GetCurrentContentTexture());
                videoPlaybackManager.PauseIdle();
                videoPlaybackManager.StopInactiveContent();
            }
        }

        private void PrepareGameplay()
        {
            if (currentLevel == null)
            {
                gameFlowManager.ReportRecoverableError("GamePreparing entered without a current level.");
                return;
            }

            videoPlaybackManager.PrepareGameplay(currentLevel.GameplayVideoRelativePath);
        }

        private void PlayGameplay()
        {
            if (videoPlaybackManager.PlayPrepared(VideoContentType.Gameplay))
            {
                transitionController.ShowContentVideo(videoPlaybackManager.GetCurrentContentTexture());
                videoPlaybackManager.PauseIdle();
                videoPlaybackManager.StopInactiveContent();
                gameFlowManager.NotifyGamePrepared();
            }
        }

        private void SchedulePrepareResult(VideoContentType resultType, GameState previousState)
        {
            if (currentLevel == null)
            {
                gameFlowManager.ReportRecoverableError($"{resultType}Preparing entered without a current level.");
                return;
            }

            StopCoroutineIfRunning(ref resultPrepareDelayCoroutine);

            float delay = previousState == GameState.Gameplay && videoSystemConfig != null
                ? videoSystemConfig.GameplayVideoStopDelayBeforeResultPrepare
                : 0f;

            if (delay <= 0f)
            {
                PauseGameplayLoopBeforeResultPrepare();
                PrepareResult(resultType);
                return;
            }

            resultPrepareDelayCoroutine = StartCoroutine(ResultPrepareDelayRoutine(resultType, delay));
        }

        private IEnumerator ResultPrepareDelayRoutine(VideoContentType resultType, float delay)
        {
            float startTime = Time.unscaledTime;

            while (IsExpectedResultPreparing(resultType) && Time.unscaledTime - startTime < delay)
            {
                yield return null;
            }

            resultPrepareDelayCoroutine = null;

            if (!IsExpectedResultPreparing(resultType))
            {
                yield break;
            }

            PauseGameplayLoopBeforeResultPrepare();
            PrepareResult(resultType);
        }

        private void PauseGameplayLoopBeforeResultPrepare()
        {
            if (videoPlaybackManager.CurrentContentType != VideoContentType.Gameplay)
            {
                return;
            }

            videoPlaybackManager.PauseContent();
        }

        private void PrepareResult(VideoContentType resultType)
        {
            if (currentLevel == null)
            {
                gameFlowManager.ReportRecoverableError($"{resultType}Preparing entered without a current level.");
                return;
            }

            if (resultType == VideoContentType.Success)
            {
                videoPlaybackManager.PrepareSuccess(currentLevel.SuccessVideoRelativePath);
                return;
            }

            if (resultType == VideoContentType.Failure)
            {
                videoPlaybackManager.PrepareFailure(currentLevel.FailureVideoRelativePath);
            }
        }

        private void PlayResult(VideoContentType resultType)
        {
            if (videoPlaybackManager.PlayPrepared(resultType))
            {
                transitionController.ShowContentVideo(videoPlaybackManager.GetCurrentContentTexture());
                videoPlaybackManager.PauseIdle();
                videoPlaybackManager.StopInactiveContent();
                StartResultPerformanceCue(resultType);
            }
        }

        private void ResetVideoFlow()
        {
            StopCoroutineIfRunning(ref resultPrepareDelayCoroutine);
            StopCoroutineIfRunning(ref resultPerformanceCueCoroutine);
            if (streamingImageLoader != null)
            {
                streamingImageLoader.CancelLoading();
            }
            currentLevel = null;
        }

        private void HandleFirstFrameReady(VideoContentType contentType)
        {
            GameState state = gameFlowManager.CurrentState;

            if (contentType == VideoContentType.Idle && state == GameState.IdlePreparing)
            {
                transitionController.ShowIdleVideo();
                videoPlaybackManager.PlayPrepared(VideoContentType.Idle);
                ReleaseStaticBackgroundAfterIdleIsVisible();
                videoPlaybackManager.StopContent();
                gameFlowManager.NotifyIdlePrepared();
                return;
            }

            if (contentType == VideoContentType.Intro && state == GameState.IntroPreparing)
            {
                gameFlowManager.NotifyIntroPrepared();
                return;
            }

            if (contentType == VideoContentType.Gameplay && state == GameState.GamePreparing)
            {
                PlayGameplay();
                return;
            }

            if ((contentType == VideoContentType.Success && state == GameState.SuccessPreparing) ||
                (contentType == VideoContentType.Failure && state == GameState.FailurePreparing))
            {
                gameFlowManager.NotifyResultPrepared();
            }
        }

        private void HandleVideoCompleted(VideoContentType contentType)
        {
            GameState state = gameFlowManager.CurrentState;

            if (contentType == VideoContentType.Intro && state == GameState.IntroPlaying)
            {
                gameFlowManager.NotifyIntroCompleted();
                return;
            }

            if ((contentType == VideoContentType.Success && state == GameState.SuccessPlaying) ||
                (contentType == VideoContentType.Failure && state == GameState.FailurePlaying))
            {
                StopCoroutineIfRunning(ref resultPerformanceCueCoroutine);
                gameFlowManager.NotifyResultCompleted();
            }
        }

        private void HandleVideoFailed(VideoContentType contentType, string message)
        {
            StopCoroutineIfRunning(ref resultPerformanceCueCoroutine);
            gameFlowManager.ReportRecoverableError($"Video {contentType} failed. {message}");
        }

        private void StartResultPerformanceCue(VideoContentType resultType)
        {
            StopCoroutineIfRunning(ref resultPerformanceCueCoroutine);

            if (currentLevel == null)
            {
                return;
            }

            float leadTime = resultType == VideoContentType.Success
                ? currentLevel.SuccessPerformanceLeadTime
                : resultType == VideoContentType.Failure
                    ? currentLevel.FailurePerformanceLeadTime
                    : 0f;

            if (leadTime <= 0f)
            {
                return;
            }

            resultPerformanceCueCoroutine = StartCoroutine(ResultPerformanceCueRoutine(resultType, leadTime));
        }

        private IEnumerator ResultPerformanceCueRoutine(VideoContentType resultType, float leadTime)
        {
            while (IsExpectedResultPlaying(resultType))
            {
                if (videoPlaybackManager.TryGetPlaybackTiming(resultType, out double time, out double length, out double remaining) &&
                    remaining <= leadTime)
                {
                    Debug.Log($"[VideoFlow] {resultType} performance cue fired. Time: {time:0.###}/{length:0.###}, remaining: {remaining:0.###}.", this);
                    InvokeResultPerformanceCue(resultType);
                    break;
                }

                yield return null;
            }

            resultPerformanceCueCoroutine = null;
        }

        private bool IsExpectedResultPlaying(VideoContentType resultType)
        {
            return (resultType == VideoContentType.Success && gameFlowManager.CurrentState == GameState.SuccessPlaying) ||
                   (resultType == VideoContentType.Failure && gameFlowManager.CurrentState == GameState.FailurePlaying);
        }

        private bool IsExpectedResultPreparing(VideoContentType resultType)
        {
            return (resultType == VideoContentType.Success && gameFlowManager.CurrentState == GameState.SuccessPreparing) ||
                   (resultType == VideoContentType.Failure && gameFlowManager.CurrentState == GameState.FailurePreparing);
        }

        private void InvokeResultPerformanceCue(VideoContentType resultType)
        {
            if (resultType == VideoContentType.Success)
            {
                onSuccessPerformanceCue.Invoke();
                return;
            }

            if (resultType == VideoContentType.Failure)
            {
                onFailurePerformanceCue.Invoke();
            }
        }

        private bool ValidateLevelMedia(LevelConfig level)
        {
            if (!ValidateFilePath(level.IntroVideoRelativePath, "Intro video") ||
                !ValidateFilePath(level.GameplayVideoRelativePath, "Gameplay video") ||
                !ValidateFilePath(level.SuccessVideoRelativePath, "Success video") ||
                !ValidateFilePath(level.FailureVideoRelativePath, "Failure video"))
            {
                gameFlowManager.ReportRecoverableError($"Level media validation failed for {level.LevelId}.");
                return false;
            }

            return true;
        }

        private bool ValidateFilePath(string relativePath, string label)
        {
            if (!StreamingAssetsPathUtility.TryBuildFilePath(relativePath, out string fullPath))
            {
                Debug.LogWarning($"[VideoFlow] {label} path is invalid: {relativePath}", this);
                return false;
            }

            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogWarning($"[Video] File not found:\n{fullPath}", this);
                return false;
            }

            return true;
        }

        private bool HasRequiredReferences()
        {
            if (gameFlowManager != null &&
                videoSystemConfig != null &&
                videoPlaybackManager != null &&
                transitionController != null)
            {
                return true;
            }

            Debug.LogWarning("[VideoFlow] Missing one or more required references.", this);
            if (gameFlowManager != null)
            {
                gameFlowManager.ReportRecoverableError("VideoFlowCoordinator is missing required references.");
            }

            return false;
        }

        private void ReleaseStaticBackgroundAfterIdleIsVisible()
        {
            transitionController.ClearStaticBackground();
            if (streamingImageLoader != null)
            {
                streamingImageLoader.ReleaseCurrentTexture();
            }
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
    }
}
