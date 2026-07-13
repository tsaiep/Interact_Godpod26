using System;
using System.Collections;
using RFIDBaggage.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace RFIDBaggage.Video
{
    [Serializable]
    public sealed class VideoContentTypeUnityEvent : UnityEvent<VideoContentType>
    {
    }

    public sealed class VideoPlaybackManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField, Tooltip("Global video playback settings.")]
        private VideoSystemConfig videoSystemConfig;

        [Header("Players")]
        [SerializeField, Tooltip("VideoPlayer used only for the idle loop.")]
        private VideoPlayer idleVideoPlayer;

        [SerializeField, Tooltip("VideoPlayer used for Intro, Success, and Failure videos.")]
        private VideoPlayer contentVideoPlayer;

        [Header("Unity Events")]
        [SerializeField] private VideoContentTypeUnityEvent onVideoPrepared = new VideoContentTypeUnityEvent();
        [SerializeField] private VideoContentTypeUnityEvent onFirstFrameReady = new VideoContentTypeUnityEvent();
        [SerializeField] private VideoContentTypeUnityEvent onVideoStarted = new VideoContentTypeUnityEvent();
        [SerializeField] private VideoContentTypeUnityEvent onVideoCompleted = new VideoContentTypeUnityEvent();
        [SerializeField] private UnityEvent onVideoError = new UnityEvent();

        private int operationToken;
        private int preparingToken;
        private int playingToken;
        private bool hasReportedCompletion;
        private bool firstFrameReceived;
        private VideoContentType preparingContentType = VideoContentType.None;
        private VideoContentType currentContentType = VideoContentType.None;
        private Coroutine prepareTimeoutCoroutine;
        private Coroutine firstFrameCoroutine;

        public bool IsPreparing { get; private set; }
        public bool IsPlaying { get; private set; }
        public VideoContentType CurrentContentType => currentContentType;
        public VideoContentType PreparingContentType => preparingContentType;
        public VideoContentType PreparedContentType { get; private set; } = VideoContentType.None;

        public event Action<VideoContentType> VideoPrepared;
        public event Action<VideoContentType> FirstFrameReady;
        public event Action<VideoContentType> VideoStarted;
        public event Action<VideoContentType> VideoCompleted;
        public event Action<VideoContentType, string> VideoFailed;

        private void OnEnable()
        {
            Subscribe(idleVideoPlayer);
            Subscribe(contentVideoPlayer);
        }

        private void OnDisable()
        {
            Unsubscribe(idleVideoPlayer);
            Unsubscribe(contentVideoPlayer);
        }

        public void PrepareIdle(string relativePath)
        {
            Prepare(VideoContentType.Idle, relativePath, true);
        }

        public void PrepareIntro(string relativePath)
        {
            Prepare(VideoContentType.Intro, relativePath, false);
        }

        public void PrepareSuccess(string relativePath)
        {
            Prepare(VideoContentType.Success, relativePath, false);
        }

        public void PrepareFailure(string relativePath)
        {
            Prepare(VideoContentType.Failure, relativePath, false);
        }

        public void Prepare(VideoContentType contentType, string relativePath, bool loop)
        {
            VideoPlayer player = GetPlayer(contentType);
            if (player == null)
            {
                ReportFailure(contentType, $"Missing VideoPlayer for {contentType}.");
                return;
            }

            if (!StreamingAssetsPathUtility.TryBuildFilePath(relativePath, out string fullPath) ||
                !StreamingAssetsPathUtility.TryBuildFileUri(relativePath, out string fileUri))
            {
                ReportFailure(contentType, $"Invalid StreamingAssets path: {relativePath}");
                return;
            }

            if (!System.IO.File.Exists(fullPath))
            {
                ReportFailure(contentType, $"File not found:\n{fullPath}");
                return;
            }

            operationToken++;
            preparingToken = operationToken;
            preparingContentType = contentType;
            PreparedContentType = VideoContentType.None;
            IsPreparing = true;
            firstFrameReceived = false;
            hasReportedCompletion = false;

            StopCoroutineIfRunning(ref prepareTimeoutCoroutine);
            StopCoroutineIfRunning(ref firstFrameCoroutine);

            ConfigurePlayer(player, loop);
            player.Stop();
            player.url = fileUri;

            if (ShouldLog)
            {
                Debug.Log($"[Video] Preparing {contentType}:\n{fullPath}", this);
            }

            player.Prepare();
            prepareTimeoutCoroutine = StartCoroutine(PrepareTimeoutRoutine(preparingToken, contentType, relativePath));
        }

        public bool PlayPrepared(VideoContentType expectedContentType)
        {
            if (PreparedContentType != expectedContentType)
            {
                Debug.LogWarning($"[Video] Cannot play {expectedContentType}. Prepared content is {PreparedContentType}.", this);
                return false;
            }

            VideoPlayer player = GetPlayer(expectedContentType);
            if (player == null || !player.isPrepared)
            {
                Debug.LogWarning($"[Video] Cannot play {expectedContentType}. VideoPlayer is not prepared.", this);
                return false;
            }

            operationToken++;
            playingToken = operationToken;
            currentContentType = expectedContentType;
            IsPlaying = true;
            hasReportedCompletion = false;

            player.Play();

            if (ShouldLog)
            {
                Debug.Log($"[Video] {expectedContentType} started.", this);
            }

            VideoStarted?.Invoke(expectedContentType);
            onVideoStarted.Invoke(expectedContentType);
            return true;
        }

        public bool PlayIfPlayerPrepared(VideoContentType contentType)
        {
            VideoPlayer player = GetPlayer(contentType);
            if (player == null || !player.isPrepared)
            {
                return false;
            }

            operationToken++;
            currentContentType = contentType;
            IsPlaying = true;
            hasReportedCompletion = false;
            player.Play();

            if (ShouldLog)
            {
                Debug.Log($"[Video] {contentType} started.", this);
            }

            VideoStarted?.Invoke(contentType);
            onVideoStarted.Invoke(contentType);
            return true;
        }

        public bool IsPlayerPrepared(VideoContentType contentType)
        {
            VideoPlayer player = GetPlayer(contentType);
            return player != null && player.isPrepared;
        }

        public void StopContent()
        {
            StopPlayer(contentVideoPlayer);

            if (currentContentType != VideoContentType.Idle)
            {
                IsPlaying = false;
                currentContentType = VideoContentType.None;
            }
        }

        public void PauseIdle()
        {
            if (idleVideoPlayer == null)
            {
                return;
            }

            idleVideoPlayer.Pause();
        }

        public void StopIdle()
        {
            StopPlayer(idleVideoPlayer);

            if (currentContentType == VideoContentType.Idle)
            {
                IsPlaying = false;
                currentContentType = VideoContentType.None;
            }
        }

        public void StopAll()
        {
            operationToken++;
            StopCoroutineIfRunning(ref prepareTimeoutCoroutine);
            StopCoroutineIfRunning(ref firstFrameCoroutine);
            StopPlayer(idleVideoPlayer);
            StopPlayer(contentVideoPlayer);
            IsPreparing = false;
            IsPlaying = false;
            preparingContentType = VideoContentType.None;
            currentContentType = VideoContentType.None;
            PreparedContentType = VideoContentType.None;
        }

        private void ConfigurePlayer(VideoPlayer player, bool loop)
        {
            player.source = VideoSource.Url;
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = loop;
            player.sendFrameReadyEvents = true;
        }

        private VideoPlayer GetPlayer(VideoContentType contentType)
        {
            return contentType == VideoContentType.Idle ? idleVideoPlayer : contentVideoPlayer;
        }

        private void Subscribe(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.prepareCompleted += HandlePrepareCompleted;
            player.loopPointReached += HandleLoopPointReached;
            player.errorReceived += HandleErrorReceived;
            player.frameReady += HandleFrameReady;
        }

        private void Unsubscribe(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.prepareCompleted -= HandlePrepareCompleted;
            player.loopPointReached -= HandleLoopPointReached;
            player.errorReceived -= HandleErrorReceived;
            player.frameReady -= HandleFrameReady;
        }

        private void HandlePrepareCompleted(VideoPlayer player)
        {
            int callbackToken = preparingToken;
            VideoContentType contentType = preparingContentType;

            if (!IsPreparing || callbackToken != operationToken)
            {
                Debug.Log($"[Video] Ignored stale callback. Token: {callbackToken}", this);
                return;
            }

            StopCoroutineIfRunning(ref prepareTimeoutCoroutine);

            if (ShouldLog)
            {
                Debug.Log($"[Video] {contentType} prepared.", this);
            }

            VideoPrepared?.Invoke(contentType);
            onVideoPrepared.Invoke(contentType);
            firstFrameCoroutine = StartCoroutine(FirstFrameRoutine(callbackToken, contentType, player));
        }

        private IEnumerator FirstFrameRoutine(int token, VideoContentType contentType, VideoPlayer player)
        {
            float timeout = videoSystemConfig != null ? videoSystemConfig.FirstFrameTimeout : 3f;
            float start = Time.unscaledTime;

            if (player.canStep)
            {
                player.StepForward();
            }

            while (token == operationToken && !firstFrameReceived && Time.unscaledTime - start < timeout)
            {
                if (player.texture != null && player.texture.width > 0 && player.texture.height > 0)
                {
                    firstFrameReceived = true;
                    break;
                }

                yield return null;
            }

            if (token != operationToken)
            {
                Debug.Log($"[Video] Ignored stale callback. Token: {token}", this);
                yield break;
            }

            if (!firstFrameReceived)
            {
                ReportFailure(contentType, $"First frame timeout: {contentType}");
                yield break;
            }

            IsPreparing = false;
            PreparedContentType = contentType;

            if (ShouldLog)
            {
                Debug.Log($"[Video] {contentType} first frame ready.", this);
            }

            FirstFrameReady?.Invoke(contentType);
            onFirstFrameReady.Invoke(contentType);
        }

        private void HandleFrameReady(VideoPlayer player, long frameIdx)
        {
            firstFrameReceived = true;
        }

        private void HandleLoopPointReached(VideoPlayer player)
        {
            VideoContentType completedType = GetCompletedContentType(player);

            if (completedType == VideoContentType.Idle || completedType == VideoContentType.None || hasReportedCompletion)
            {
                return;
            }

            hasReportedCompletion = true;
            IsPlaying = false;

            if (ShouldLog)
            {
                Debug.Log($"[Video] {completedType} completed.", this);
            }

            VideoCompleted?.Invoke(completedType);
            onVideoCompleted.Invoke(completedType);
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            VideoContentType failedType = player == idleVideoPlayer
                ? VideoContentType.Idle
                : preparingContentType != VideoContentType.None ? preparingContentType : currentContentType;

            ReportFailure(failedType, message);
        }

        private IEnumerator PrepareTimeoutRoutine(int token, VideoContentType contentType, string relativePath)
        {
            float timeout = videoSystemConfig != null ? videoSystemConfig.PrepareTimeout : 10f;
            yield return new WaitForSecondsRealtime(timeout);

            if (token != operationToken || !IsPreparing)
            {
                yield break;
            }

            ReportFailure(contentType, $"Prepare timeout:\n{relativePath}");
        }

        private void ReportFailure(VideoContentType contentType, string message)
        {
            operationToken++;
            IsPreparing = false;
            IsPlaying = false;
            StopCoroutineIfRunning(ref prepareTimeoutCoroutine);
            StopCoroutineIfRunning(ref firstFrameCoroutine);
            Debug.LogWarning($"[Video] {message}", this);
            VideoFailed?.Invoke(contentType, message);
            onVideoError.Invoke();
        }

        private void StopPlayer(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.Stop();
        }

        private VideoContentType GetCompletedContentType(VideoPlayer player)
        {
            if (player == idleVideoPlayer)
            {
                return VideoContentType.Idle;
            }

            if (player != contentVideoPlayer)
            {
                return VideoContentType.None;
            }

            if (currentContentType == VideoContentType.Intro ||
                currentContentType == VideoContentType.Success ||
                currentContentType == VideoContentType.Failure)
            {
                return currentContentType;
            }

            return VideoContentType.None;
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

        private bool ShouldLog => videoSystemConfig == null || videoSystemConfig.VerboseVideoLogs;
    }
}
