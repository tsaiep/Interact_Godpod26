using System.Collections;
using RFIDBaggage.Core;
using RFIDBaggage.Levels;
using RFIDBaggage.Presentation;
using RFIDBaggage.Utilities;
using UnityEngine;

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

        [SerializeField, Tooltip("StreamingAssets image loader for final-frame backgrounds.")]
        private StreamingImageLoader streamingImageLoader;

        [SerializeField, Tooltip("Display layer switcher.")]
        private VideoTransitionController transitionController;

        private LevelConfig currentLevel;
        private Texture2D finalFrameTexture;
        private bool finalFrameLoadCompleted;
        private Coroutine gamePreparingCoroutine;

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
                    BeginGamePreparing();
                    break;
                case GameState.SuccessPreparing:
                    PrepareResult(VideoContentType.Success);
                    break;
                case GameState.FailurePreparing:
                    PrepareResult(VideoContentType.Failure);
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

            if (videoPlaybackManager.IsPlayerPrepared(VideoContentType.Idle))
            {
                transitionController.ShowIdleVideo();
                videoPlaybackManager.PlayIfPlayerPrepared(VideoContentType.Idle);
                ReleaseStaticBackgroundAfterIdleIsVisible();
                videoPlaybackManager.StopContent();
                gameFlowManager.NotifyIdlePrepared();
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
            finalFrameTexture = null;
            finalFrameLoadCompleted = false;

            StopCoroutineIfRunning(ref gamePreparingCoroutine);
            streamingImageLoader.ReleaseCurrentTexture();

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

            finalFrameLoadCompleted = false;
            streamingImageLoader.Load(
                currentLevel.FinalFrameImageRelativePath,
                texture =>
                {
                    finalFrameTexture = texture;
                    finalFrameLoadCompleted = true;
                },
                message =>
                {
                    finalFrameLoadCompleted = false;
                    gameFlowManager.ReportRecoverableError(message);
                });

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

        private void BeginGamePreparing()
        {
            StopCoroutineIfRunning(ref gamePreparingCoroutine);
            gamePreparingCoroutine = StartCoroutine(GamePreparingRoutine());
        }

        private IEnumerator GamePreparingRoutine()
        {
            float timeout = videoSystemConfig != null ? videoSystemConfig.ImageLoadTimeout : 10f;
            float start = Time.unscaledTime;

            while (!finalFrameLoadCompleted && Time.unscaledTime - start < timeout)
            {
                yield return null;
            }

            gamePreparingCoroutine = null;

            if (!finalFrameLoadCompleted || finalFrameTexture == null)
            {
                gameFlowManager.ReportRecoverableError("Final frame image did not finish loading before timeout.");
                yield break;
            }

            transitionController.ShowStaticBackground(finalFrameTexture);
            videoPlaybackManager.StopContent();
            gameFlowManager.NotifyGamePrepared();
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
            }
        }

        private void ResetVideoFlow()
        {
            StopCoroutineIfRunning(ref gamePreparingCoroutine);
            streamingImageLoader.CancelLoading();
            finalFrameTexture = null;
            finalFrameLoadCompleted = false;
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
                gameFlowManager.NotifyResultCompleted();
            }
        }

        private void HandleVideoFailed(VideoContentType contentType, string message)
        {
            gameFlowManager.ReportRecoverableError($"Video {contentType} failed. {message}");
        }

        private bool ValidateLevelMedia(LevelConfig level)
        {
            if (!ValidateFilePath(level.IntroVideoRelativePath, "Intro video") ||
                !ValidateFilePath(level.SuccessVideoRelativePath, "Success video") ||
                !ValidateFilePath(level.FailureVideoRelativePath, "Failure video") ||
                !ValidateFilePath(level.FinalFrameImageRelativePath, "Final frame image"))
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
                streamingImageLoader != null &&
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
            streamingImageLoader.ReleaseCurrentTexture();
            finalFrameTexture = null;
            finalFrameLoadCompleted = false;
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
