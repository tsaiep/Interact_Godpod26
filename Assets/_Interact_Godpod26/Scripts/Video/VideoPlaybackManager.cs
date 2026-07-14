using System;
using System.Collections;
using System.IO;
using RFIDBaggage.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
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

        [FormerlySerializedAs("contentVideoPlayer")]
        [SerializeField, Tooltip("First persistent content VideoPlayer. Assign RT_ContentA as Target Texture.")]
        private VideoPlayer contentVideoPlayerA;

        [SerializeField, Tooltip("Second persistent content VideoPlayer. Assign RT_ContentB as Target Texture.")]
        private VideoPlayer contentVideoPlayerB;

        [Header("Unity Events")]
        [SerializeField] private VideoContentTypeUnityEvent onVideoPrepared = new VideoContentTypeUnityEvent();
        [SerializeField] private VideoContentTypeUnityEvent onFirstFrameReady = new VideoContentTypeUnityEvent();
        [SerializeField] private VideoContentTypeUnityEvent onVideoStarted = new VideoContentTypeUnityEvent();
        [SerializeField] private VideoContentTypeUnityEvent onVideoCompleted = new VideoContentTypeUnityEvent();
        [SerializeField] private UnityEvent onVideoError = new UnityEvent();

        private sealed class PlayerState
        {
            public PlayerState(VideoPlayer player, bool isContent)
            {
                Player = player;
                IsContent = isContent;
            }

            public VideoPlayer Player;
            public bool IsContent;
            public int Token;
            public bool IsPreparing;
            public bool IsPlaying;
            public bool FirstFrameReceived;
            public bool CompletionReported;
            public bool Loop;
            public VideoContentType ContentType = VideoContentType.None;
            public string RelativePath = string.Empty;
            public string FullPath = string.Empty;
            public string Url = string.Empty;
            public Coroutine PrepareTimeoutCoroutine;
            public Coroutine FirstFrameCoroutine;
        }

        private int operationToken;
        private PlayerState idleState;
        private PlayerState contentAState;
        private PlayerState contentBState;
        private PlayerState preparedState;
        private PlayerState activeContentState;
        private PlayerState standbyContentState;

        public bool IsPreparing { get; private set; }
        public bool IsPlaying { get; private set; }
        public VideoContentType CurrentContentType { get; private set; } = VideoContentType.None;
        public VideoContentType PreparingContentType { get; private set; } = VideoContentType.None;
        public VideoContentType PreparedContentType { get; private set; } = VideoContentType.None;

        public event Action<VideoContentType> VideoPrepared;
        public event Action<VideoContentType> FirstFrameReady;
        public event Action<VideoContentType> VideoStarted;
        public event Action<VideoContentType> VideoCompleted;
        public event Action<VideoContentType, string> VideoFailed;

        private void Awake()
        {
            InitializeStates();
        }

        private void OnEnable()
        {
            InitializeStates();
            SubscribeAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
            StopAllCoroutinesForStates();
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
            InitializeStates();

            PlayerState state = GetPrepareState(contentType);
            if (state == null || state.Player == null)
            {
                ReportFailure(contentType, $"Missing VideoPlayer for {contentType}. Content videos require ContentVideoPlayerA and ContentVideoPlayerB.");
                return;
            }

            if (!StreamingAssetsPathUtility.TryBuildFilePath(relativePath, out string fullPath) ||
                !StreamingAssetsPathUtility.TryBuildFileUri(relativePath, out string fileUri))
            {
                ReportFailure(contentType, $"Invalid StreamingAssets path.\nRelative Path: {relativePath}");
                return;
            }

            if (!File.Exists(fullPath))
            {
                ReportFailure(contentType, BuildDiagnosticMessage("File not found.", relativePath, fullPath, fileUri));
                return;
            }

            operationToken++;
            ResetPreparingState(state, operationToken, contentType, relativePath, fullPath, fileUri, loop);
            PreparedContentType = VideoContentType.None;
            preparedState = null;
            PreparingContentType = contentType;
            IsPreparing = true;

            ConfigurePlayer(state.Player, loop);
            state.Player.Stop();
            state.Player.url = fileUri;

            if (ShouldLog)
            {
                Debug.Log(BuildDiagnosticMessage($"Preparing {contentType}.", relativePath, fullPath, fileUri), this);
            }

            state.Player.Prepare();
            state.PrepareTimeoutCoroutine = StartCoroutine(PrepareTimeoutRoutine(state, state.Token));
        }

        public bool PlayPrepared(VideoContentType expectedContentType)
        {
            if (PreparedContentType != expectedContentType || preparedState == null)
            {
                Debug.LogWarning($"[Video] Cannot play {expectedContentType}. Prepared content is {PreparedContentType}.", this);
                return false;
            }

            return PlayState(preparedState, expectedContentType);
        }

        public bool PlayIfPlayerPrepared(VideoContentType contentType)
        {
            PlayerState state = GetPreparedState(contentType);
            return state != null && PlayState(state, contentType);
        }

        public bool IsPlayerPrepared(VideoContentType contentType)
        {
            PlayerState state = GetPreparedState(contentType);
            return state != null && state.Player != null && state.Player.isPrepared;
        }

        public Texture GetPreparedTexture(VideoContentType contentType)
        {
            PlayerState state = PreparedContentType == contentType ? preparedState : GetPreparedState(contentType);
            return GetTargetTexture(state);
        }

        public Texture GetCurrentContentTexture()
        {
            return GetTargetTexture(activeContentState);
        }

        public void StopInactiveContent()
        {
            StopContentStateIfInactive(contentAState);
            StopContentStateIfInactive(contentBState);
        }

        public void StopContent()
        {
            PlayerState nextStandbyState = GetOtherContentState(activeContentState);
            StopPlayer(contentAState);
            StopPlayer(contentBState);
            activeContentState = null;
            standbyContentState = nextStandbyState != null ? nextStandbyState : contentAState != null ? contentAState : contentBState;

            if (CurrentContentType != VideoContentType.Idle)
            {
                IsPlaying = false;
                CurrentContentType = VideoContentType.None;
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
            StopPlayer(idleState);

            if (CurrentContentType == VideoContentType.Idle)
            {
                IsPlaying = false;
                CurrentContentType = VideoContentType.None;
            }
        }

        public void StopAll()
        {
            operationToken++;
            StopAllCoroutinesForStates();
            StopPlayer(idleState);
            StopPlayer(contentAState);
            StopPlayer(contentBState);
            IsPreparing = false;
            IsPlaying = false;
            PreparingContentType = VideoContentType.None;
            CurrentContentType = VideoContentType.None;
            PreparedContentType = VideoContentType.None;
            preparedState = null;
            activeContentState = null;
            standbyContentState = contentAState != null ? contentAState : contentBState;
        }

        private bool PlayState(PlayerState state, VideoContentType contentType)
        {
            if (state.Player == null || !state.Player.isPrepared)
            {
                Debug.LogWarning($"[Video] Cannot play {contentType}. VideoPlayer is not prepared.", this);
                return false;
            }

            operationToken++;
            state.IsPlaying = true;
            state.CompletionReported = false;
            state.ContentType = contentType;
            CurrentContentType = contentType;
            IsPlaying = true;

            if (state.IsContent)
            {
                activeContentState = state;
                standbyContentState = GetOtherContentState(state);
            }

            state.Player.Play();

            if (ShouldLog)
            {
                Debug.Log($"[Video] {contentType} started on {state.Player.name}.", this);
            }

            VideoStarted?.Invoke(contentType);
            onVideoStarted.Invoke(contentType);
            return true;
        }

        private void InitializeStates()
        {
            idleState = UpdateState(idleState, idleVideoPlayer, false);
            contentAState = UpdateState(contentAState, contentVideoPlayerA, true);
            contentBState = UpdateState(contentBState, contentVideoPlayerB, true);

            if (activeContentState != contentAState && activeContentState != contentBState)
            {
                activeContentState = null;
            }

            if (standbyContentState != contentAState && standbyContentState != contentBState)
            {
                standbyContentState = contentAState;
            }

            if (standbyContentState == null && contentBState != null)
            {
                standbyContentState = contentBState;
            }
        }

        private static PlayerState UpdateState(PlayerState state, VideoPlayer player, bool isContent)
        {
            if (player == null)
            {
                return null;
            }

            if (state == null)
            {
                return new PlayerState(player, isContent);
            }

            state.Player = player;
            state.IsContent = isContent;
            return state;
        }

        private PlayerState GetPrepareState(VideoContentType contentType)
        {
            if (contentType == VideoContentType.Idle)
            {
                return idleState;
            }

            if (contentAState == null || contentBState == null)
            {
                return null;
            }

            if (standbyContentState == null || standbyContentState == activeContentState)
            {
                standbyContentState = GetOtherContentState(activeContentState);
            }

            return standbyContentState;
        }

        private PlayerState GetPreparedState(VideoContentType contentType)
        {
            if (contentType == VideoContentType.Idle)
            {
                return idleState != null && idleState.Player != null && idleState.Player.isPrepared ? idleState : null;
            }

            if (preparedState != null && preparedState.ContentType == contentType && preparedState.Player != null && preparedState.Player.isPrepared)
            {
                return preparedState;
            }

            if (activeContentState != null && activeContentState.ContentType == contentType && activeContentState.Player != null && activeContentState.Player.isPrepared)
            {
                return activeContentState;
            }

            return null;
        }

        private PlayerState GetOtherContentState(PlayerState state)
        {
            if (state == contentAState)
            {
                return contentBState;
            }

            if (state == contentBState)
            {
                return contentAState;
            }

            return contentAState != null ? contentAState : contentBState;
        }

        private void ResetPreparingState(PlayerState state, int token, VideoContentType contentType, string relativePath, string fullPath, string url, bool loop)
        {
            StopCoroutineIfRunning(ref state.PrepareTimeoutCoroutine);
            StopCoroutineIfRunning(ref state.FirstFrameCoroutine);
            state.Token = token;
            state.IsPreparing = true;
            state.IsPlaying = false;
            state.FirstFrameReceived = false;
            state.CompletionReported = false;
            state.ContentType = contentType;
            state.RelativePath = relativePath;
            state.FullPath = fullPath;
            state.Url = url;
            state.Loop = loop;
        }

        private void ConfigurePlayer(VideoPlayer player, bool loop)
        {
            player.source = VideoSource.Url;
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = loop;
            player.sendFrameReadyEvents = true;
        }

        private void SubscribeAll()
        {
            Subscribe(idleVideoPlayer);
            if (contentVideoPlayerA != idleVideoPlayer)
            {
                Subscribe(contentVideoPlayerA);
            }

            if (contentVideoPlayerB != idleVideoPlayer && contentVideoPlayerB != contentVideoPlayerA)
            {
                Subscribe(contentVideoPlayerB);
            }
        }

        private void UnsubscribeAll()
        {
            Unsubscribe(idleVideoPlayer);
            if (contentVideoPlayerA != idleVideoPlayer)
            {
                Unsubscribe(contentVideoPlayerA);
            }

            if (contentVideoPlayerB != idleVideoPlayer && contentVideoPlayerB != contentVideoPlayerA)
            {
                Unsubscribe(contentVideoPlayerB);
            }
        }

        private void Subscribe(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.prepareCompleted -= HandlePrepareCompleted;
            player.loopPointReached -= HandleLoopPointReached;
            player.errorReceived -= HandleErrorReceived;
            player.frameReady -= HandleFrameReady;

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
            PlayerState state = GetState(player);
            if (state == null || !state.IsPreparing || state.Token != operationToken)
            {
                LogStaleCallback(state);
                return;
            }

            StopCoroutineIfRunning(ref state.PrepareTimeoutCoroutine);

            if (ShouldLog)
            {
                Debug.Log($"[Video] {state.ContentType} prepared on {player.name}.", this);
            }

            VideoPrepared?.Invoke(state.ContentType);
            onVideoPrepared.Invoke(state.ContentType);
            state.FirstFrameCoroutine = StartCoroutine(FirstFrameRoutine(state, state.Token));
        }

        private IEnumerator FirstFrameRoutine(PlayerState state, int token)
        {
            float timeout = videoSystemConfig != null ? videoSystemConfig.FirstFrameTimeout : 5f;
            float start = Time.unscaledTime;
            VideoPlayer player = state.Player;

            if (player != null && player.canStep)
            {
                player.StepForward();
            }

            if (player != null && !player.isPlaying)
            {
                player.Play();
            }

            while (state.Token == token && state.IsPreparing && !state.FirstFrameReceived && Time.unscaledTime - start < timeout)
            {
                if (player != null && player.frame >= 0 && player.texture != null && player.texture.width > 0 && player.texture.height > 0)
                {
                    state.FirstFrameReceived = true;
                    break;
                }

                yield return null;
            }

            state.FirstFrameCoroutine = null;

            if (state.Token != token || !state.IsPreparing)
            {
                LogStaleCallback(state);
                yield break;
            }

            if (!state.FirstFrameReceived)
            {
                ReportFailure(state.ContentType, BuildDiagnosticMessage($"First frame timeout after {timeout:0.##} seconds.", state));
                yield break;
            }

            if (player != null && player.isPlaying)
            {
                player.Pause();
            }

            state.IsPreparing = false;
            state.Player.sendFrameReadyEvents = false;
            IsPreparing = false;
            PreparingContentType = VideoContentType.None;
            PreparedContentType = state.ContentType;
            preparedState = state;

            if (ShouldLog)
            {
                Debug.Log($"[Video] {state.ContentType} first frame ready on {player.name}.", this);
            }

            FirstFrameReady?.Invoke(state.ContentType);
            onFirstFrameReady.Invoke(state.ContentType);
        }

        private void HandleFrameReady(VideoPlayer player, long frameIdx)
        {
            PlayerState state = GetState(player);
            if (state == null || !state.IsPreparing || state.Token != operationToken)
            {
                return;
            }

            state.FirstFrameReceived = true;
            player.sendFrameReadyEvents = false;
        }

        private void HandleLoopPointReached(VideoPlayer player)
        {
            PlayerState state = GetState(player);
            if (state == null || state.ContentType == VideoContentType.Idle || state.Loop || !state.IsPlaying || state.CompletionReported)
            {
                return;
            }

            state.CompletionReported = true;
            state.IsPlaying = false;
            IsPlaying = false;
            CurrentContentType = VideoContentType.None;

            if (ShouldLog)
            {
                Debug.Log($"[Video] {state.ContentType} completed on {player.name}.", this);
            }

            VideoCompleted?.Invoke(state.ContentType);
            onVideoCompleted.Invoke(state.ContentType);
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            PlayerState state = GetState(player);
            VideoContentType failedType = state != null && state.ContentType != VideoContentType.None
                ? state.ContentType
                : PreparingContentType != VideoContentType.None ? PreparingContentType : CurrentContentType;

            string details = state != null
                ? BuildDiagnosticMessage($"VideoPlayer error: {message}", state)
                : $"VideoPlayer error: {message}";

            ReportFailure(failedType, details);
        }

        private IEnumerator PrepareTimeoutRoutine(PlayerState state, int token)
        {
            float timeout = videoSystemConfig != null ? videoSystemConfig.PrepareTimeout : 10f;
            yield return new WaitForSecondsRealtime(timeout);

            if (state.Token != token || !state.IsPreparing)
            {
                yield break;
            }

            ReportFailure(state.ContentType, BuildDiagnosticMessage($"Prepare timeout after {timeout:0.##} seconds.", state));
        }

        private void ReportFailure(VideoContentType contentType, string message)
        {
            operationToken++;
            IsPreparing = false;
            IsPlaying = false;
            PreparingContentType = VideoContentType.None;
            PreparedContentType = VideoContentType.None;
            preparedState = null;
            StopAllCoroutinesForStates();
            StopPreparingPlayers();

            Debug.LogWarning($"[Video] {message}", this);
            VideoFailed?.Invoke(contentType, message);
            onVideoError.Invoke();
        }

        private void StopPreparingPlayers()
        {
            StopIfPreparing(idleState);
            StopIfPreparing(contentAState);
            StopIfPreparing(contentBState);
        }

        private void StopIfPreparing(PlayerState state)
        {
            if (state == null || !state.IsPreparing)
            {
                return;
            }

            StopPlayer(state);
        }

        private void StopPlayer(PlayerState state)
        {
            if (state == null || state.Player == null)
            {
                return;
            }

            state.Player.sendFrameReadyEvents = false;
            state.Player.Stop();
            state.IsPreparing = false;
            state.IsPlaying = false;
            state.FirstFrameReceived = false;
            state.CompletionReported = false;
            state.ContentType = VideoContentType.None;
        }

        private void StopContentStateIfInactive(PlayerState state)
        {
            if (state == null || state == activeContentState)
            {
                return;
            }

            StopPlayer(state);
        }

        private PlayerState GetState(VideoPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (idleState != null && player == idleState.Player)
            {
                return idleState;
            }

            if (contentAState != null && player == contentAState.Player)
            {
                return contentAState;
            }

            if (contentBState != null && player == contentBState.Player)
            {
                return contentBState;
            }

            return null;
        }

        private static Texture GetTargetTexture(PlayerState state)
        {
            if (state == null || state.Player == null)
            {
                return null;
            }

            return state.Player.targetTexture != null ? state.Player.targetTexture : state.Player.texture;
        }

        private void StopAllCoroutinesForStates()
        {
            StopCoroutinesForState(idleState);
            StopCoroutinesForState(contentAState);
            StopCoroutinesForState(contentBState);
        }

        private void StopCoroutinesForState(PlayerState state)
        {
            if (state == null)
            {
                return;
            }

            StopCoroutineIfRunning(ref state.PrepareTimeoutCoroutine);
            StopCoroutineIfRunning(ref state.FirstFrameCoroutine);
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

        private string BuildDiagnosticMessage(string reason, PlayerState state)
        {
            return BuildDiagnosticMessage(reason, state.RelativePath, state.FullPath, state.Url);
        }

        private static string BuildDiagnosticMessage(string reason, string relativePath, string fullPath, string url)
        {
            return $"{reason}\nRelative Path: {relativePath}\nFull Path: {fullPath}\nURL: {url}";
        }

        private void LogStaleCallback(PlayerState state)
        {
            if (!ShouldLog)
            {
                return;
            }

            string playerName = state != null && state.Player != null ? state.Player.name : "<unknown>";
            int token = state != null ? state.Token : -1;
            Debug.Log($"[Video] Ignored stale callback from {playerName}. Token: {token}, Current: {operationToken}", this);
        }

        private bool ShouldLog => videoSystemConfig == null || videoSystemConfig.VerboseVideoLogs;
    }
}
