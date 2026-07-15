using System;
using System.Collections;
using System.IO;
using CabinPortraits.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace CabinPortraits.Video
{
    [Serializable]
    public sealed class CabinPortraitVideoIndexEvent : UnityEvent<int>
    {
    }

    [Serializable]
    public sealed class CabinPortraitVideoSwitchEvent : UnityEvent<int, int>
    {
    }

    [Serializable]
    public sealed class CabinPortraitVideoMessageEvent : UnityEvent<string>
    {
    }

    public sealed class CabinPortraitVideoCycleController : MonoBehaviour
    {
        public enum FlowState
        {
            SystemInitializing,
            ActivePreparing,
            ActivePlaying,
            Switching,
            CoveredPreparing,
            ErrorRecovery
        }

        private static readonly FlowState[] StateSequence =
        {
            FlowState.SystemInitializing,
            FlowState.ActivePreparing,
            FlowState.ActivePlaying,
            FlowState.Switching,
            FlowState.CoveredPreparing,
            FlowState.ErrorRecovery
        };

        [Header("Config")]
        [SerializeField, Tooltip("Video path list and timing settings.")]
        private CabinPortraitVideoSequenceConfig sequenceConfig;

        [Header("Players")]
        [SerializeField, Tooltip("First persistent VideoPlayer. Assign its own RenderTexture as Target Texture.")]
        private VideoPlayer videoPlayerA;

        [SerializeField, Tooltip("Second persistent VideoPlayer. Assign its own RenderTexture as Target Texture.")]
        private VideoPlayer videoPlayerB;

        [Header("Display")]
        [SerializeField, Tooltip("Renderer display switcher for the active player texture.")]
        private CabinPortraitVideoDisplayController displayController;

        [Header("Input")]
        [SerializeField, Tooltip("When enabled, the controller listens for the configured key in Update.")]
        private bool enableKeyboardInput = true;

        [SerializeField, Tooltip("Key used to request the next video.")]
        private KeyCode switchKey = KeyCode.Space;

        [Header("Runtime Debug")]
        [SerializeField, Tooltip("All flow states for Inspector display only. This list is not used to drive state transitions.")]
        private FlowState[] visibleStateSequence = (FlowState[])StateSequence.Clone();

        [SerializeField, Tooltip("Current flow state. Runtime display only; do not edit during Play Mode.")]
        private FlowState currentState = FlowState.SystemInitializing;

        [Header("Unity Events")]
        [SerializeField, Tooltip("Invoked when a switch request is accepted. Args: current index, next index.")]
        private CabinPortraitVideoSwitchEvent onSwitchRequested = new CabinPortraitVideoSwitchEvent();

        [SerializeField, Tooltip("Invoked before waiting Transition Cover Delay. Start the covering transition here.")]
        private UnityEvent onTransitionStarted = new UnityEvent();

        [SerializeField, Tooltip("Invoked after the next video is visible behind the fully covered transition. Reveal the mask here.")]
        private CabinPortraitVideoIndexEvent onReadyToReveal = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked after the active visible video index changes.")]
        private CabinPortraitVideoIndexEvent onVideoIndexChanged = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video has been prepared to its first frame.")]
        private CabinPortraitVideoIndexEvent onVideoPrepared = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video starts playing visibly.")]
        private CabinPortraitVideoIndexEvent onVideoStarted = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a switch request is ignored. Reason is provided for debugging.")]
        private CabinPortraitVideoMessageEvent onInputRejected = new CabinPortraitVideoMessageEvent();

        [SerializeField, Tooltip("Invoked when input is locked for startup or switching.")]
        private UnityEvent onInputLocked = new UnityEvent();

        [SerializeField, Tooltip("Invoked when input is unlocked after startup or switching.")]
        private UnityEvent onInputUnlocked = new UnityEvent();

        [SerializeField] private CabinPortraitVideoMessageEvent onVideoError = new CabinPortraitVideoMessageEvent();

        private sealed class PlayerSlotState
        {
            public PlayerSlotState(VideoPlayer player, CabinPortraitVideoSlot slot)
            {
                Player = player;
                Slot = slot;
            }

            public VideoPlayer Player;
            public CabinPortraitVideoSlot Slot;
            public int Token;
            public int VideoIndex = -1;
            public bool IsPreparing;
            public bool IsReady;
            public bool IsPlaying;
            public bool FirstFrameReceived;
            public string RelativePath = string.Empty;
            public string FullPath = string.Empty;
            public string Url = string.Empty;
        }

        private PlayerSlotState slotAState;
        private PlayerSlotState slotBState;
        private PlayerSlotState activeSlot;
        private PlayerSlotState inactiveSlot;
        private Coroutine startupCoroutine;
        private Coroutine switchCoroutine;
        private int tokenCounter;
        private int currentIndex = -1;
        private bool initialized;
        private bool inputLocked;
        private bool isSwitching;

        public int CurrentIndex => currentIndex;
        public FlowState CurrentState => currentState;
        public bool IsInitialized => initialized;
        public bool IsSwitching => isSwitching;
        public bool CanSwitch => CanAcceptSwitchRequest(out _);

        public event Action<FlowState, FlowState> StateChanged;

        private void Awake()
        {
            InitializeSlotReferences();
        }

        private void OnEnable()
        {
            InitializeSlotReferences();
            Subscribe(videoPlayerA);
            Subscribe(videoPlayerB);
        }

        private void Start()
        {
            if (sequenceConfig != null && sequenceConfig.PlayOnStart)
            {
                InitializeAndPlay();
            }
        }

        private void Update()
        {
            if (enableKeyboardInput && Input.GetKeyDown(switchKey))
            {
                RequestNextVideo();
            }
        }

        private void OnDisable()
        {
            Unsubscribe(videoPlayerA);
            Unsubscribe(videoPlayerB);
            StopCoroutineIfRunning(ref startupCoroutine);
            StopCoroutineIfRunning(ref switchCoroutine);
        }

        private void OnValidate()
        {
            visibleStateSequence = (FlowState[])StateSequence.Clone();
        }

        public void InitializeAndPlay()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            StopCoroutineIfRunning(ref startupCoroutine);
            StopCoroutineIfRunning(ref switchCoroutine);
            StopSlot(slotAState);
            StopSlot(slotBState);

            initialized = true;
            inputLocked = true;
            isSwitching = false;
            currentIndex = -1;
            activeSlot = slotAState;
            inactiveSlot = slotBState;
            currentState = FlowState.SystemInitializing;

            onInputLocked.Invoke();
            startupCoroutine = StartCoroutine(InitializeAndPlayRoutine());
        }

        public bool RequestNextVideo()
        {
            if (!CanAcceptSwitchRequest(out string rejectionReason))
            {
                if (ShouldLog)
                {
                    Debug.Log($"[CabinPortraits.Video] Ignored switch request. {rejectionReason}", this);
                }

                onInputRejected.Invoke(rejectionReason);
                return false;
            }

            switchCoroutine = StartCoroutine(SwitchRoutine());
            return true;
        }

        public void ResetToStart()
        {
            InitializeAndPlay();
        }

        public void StopAllVideos()
        {
            initialized = false;
            inputLocked = false;
            isSwitching = false;
            currentIndex = -1;
            currentState = FlowState.SystemInitializing;
            StopCoroutineIfRunning(ref startupCoroutine);
            StopCoroutineIfRunning(ref switchCoroutine);
            StopSlot(slotAState);
            StopSlot(slotBState);

            if (displayController != null)
            {
                displayController.HideAll();
            }
        }

        private IEnumerator InitializeAndPlayRoutine()
        {
            if (!TransitionTo(FlowState.ActivePreparing))
            {
                UnlockInput();
                yield break;
            }

            yield return PrepareSlotForFirstFrame(activeSlot, sequenceConfig.StartIndex);

            if (currentState == FlowState.ErrorRecovery || activeSlot == null || !activeSlot.IsReady)
            {
                UnlockInput();
                yield break;
            }

            if (!PlayVisibleSlot(activeSlot))
            {
                UnlockInput();
                yield break;
            }

            currentIndex = activeSlot.VideoIndex;
            onVideoIndexChanged.Invoke(currentIndex);
            TransitionTo(FlowState.ActivePlaying);
            UnlockInput();
            startupCoroutine = null;
        }

        private IEnumerator SwitchRoutine()
        {
            inputLocked = true;
            isSwitching = true;
            onInputLocked.Invoke();

            PlayerSlotState previousActiveSlot = activeSlot;
            PlayerSlotState nextActiveSlot = inactiveSlot;
            int previousIndex = currentIndex;
            int nextIndex = sequenceConfig.GetNextIndex(currentIndex);

            onSwitchRequested.Invoke(previousIndex, nextIndex);

            if (!TransitionTo(FlowState.Switching))
            {
                UnlockSwitchAfterFailure();
                yield break;
            }

            onTransitionStarted.Invoke();

            float transitionDelay = sequenceConfig != null ? sequenceConfig.TransitionCoverDelay : 0.5f;
            if (transitionDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionDelay);
            }

            if (currentState != FlowState.Switching)
            {
                UnlockSwitchAfterFailure();
                yield break;
            }

            if (!TransitionTo(FlowState.CoveredPreparing))
            {
                UnlockSwitchAfterFailure();
                yield break;
            }

            StopSlot(previousActiveSlot);
            yield return PrepareSlotForFirstFrame(nextActiveSlot, nextIndex);

            if (currentState == FlowState.ErrorRecovery || nextActiveSlot == null || !nextActiveSlot.IsReady)
            {
                UnlockSwitchAfterFailure();
                yield break;
            }

            if (!PlayVisibleSlot(nextActiveSlot))
            {
                UnlockSwitchAfterFailure();
                yield break;
            }

            activeSlot = nextActiveSlot;
            inactiveSlot = previousActiveSlot;
            currentIndex = nextIndex;
            onVideoIndexChanged.Invoke(currentIndex);
            onReadyToReveal.Invoke(currentIndex);

            TransitionTo(FlowState.ActivePlaying);
            isSwitching = false;
            UnlockInput();
            switchCoroutine = null;
        }

        private bool CanAcceptSwitchRequest(out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (!initialized)
            {
                rejectionReason = "System is not initialized.";
                return false;
            }

            if (sequenceConfig == null || sequenceConfig.VideoCount <= 1)
            {
                rejectionReason = "Sequence needs at least two video paths.";
                return false;
            }

            if (inputLocked || isSwitching)
            {
                rejectionReason = "Input is locked or a switch is already running.";
                return false;
            }

            if (currentState != FlowState.ActivePlaying)
            {
                rejectionReason = $"Flow state is {currentState}. Expected {FlowState.ActivePlaying}.";
                return false;
            }

            if (activeSlot == null || !activeSlot.IsPlaying)
            {
                rejectionReason = "No active video is playing yet.";
                return false;
            }

            if (inactiveSlot == null || inactiveSlot.Player == null)
            {
                rejectionReason = "Inactive VideoPlayer is missing.";
                return false;
            }

            return true;
        }

        private IEnumerator PrepareSlotForFirstFrame(PlayerSlotState state, int videoIndex)
        {
            if (!TryBeginPrepareSlot(state, videoIndex, out int token))
            {
                yield break;
            }

            VideoPlayer player = state.Player;
            float prepareWarningTimeout = sequenceConfig != null ? sequenceConfig.PrepareWarningTimeout : 10f;
            float prepareStartedAt = Time.unscaledTime;
            bool prepareWarningLogged = false;

            while (state.Token == token && state.IsPreparing && player != null && !player.isPrepared)
            {
                if (!prepareWarningLogged && Time.unscaledTime - prepareStartedAt >= prepareWarningTimeout)
                {
                    prepareWarningLogged = true;
                    Debug.LogWarning(
                        $"[CabinPortraits.Video] Prepare is still waiting after {prepareWarningTimeout:0.##} seconds for index {state.VideoIndex}. Continuing without entering ErrorRecovery.\n{state.FullPath}",
                        this);
                }

                yield return null;
            }

            if (state.Token != token || !state.IsPreparing || currentState == FlowState.ErrorRecovery)
            {
                yield break;
            }

            SetSlotAudioMuted(state, true);

            if (player.canStep)
            {
                player.StepForward();
            }

            if (!player.isPlaying)
            {
                player.Play();
            }

            float firstFrameWarningTimeout = sequenceConfig != null ? sequenceConfig.FirstFrameWarningTimeout : 5f;
            float firstFrameStartedAt = Time.unscaledTime;
            bool firstFrameWarningLogged = false;

            while (state.Token == token && state.IsPreparing && !HasFirstFrame(player))
            {
                if (!firstFrameWarningLogged && Time.unscaledTime - firstFrameStartedAt >= firstFrameWarningTimeout)
                {
                    firstFrameWarningLogged = true;
                    Debug.LogWarning(
                        $"[CabinPortraits.Video] First frame is still waiting after {firstFrameWarningTimeout:0.##} seconds for index {state.VideoIndex}. Continuing without entering ErrorRecovery.\n{state.FullPath}",
                        this);
                }

                yield return null;
            }

            if (state.Token != token || !state.IsPreparing || currentState == FlowState.ErrorRecovery)
            {
                yield break;
            }

            if (player.isPlaying)
            {
                player.Pause();
            }

            player.sendFrameReadyEvents = false;
            state.IsPreparing = false;
            state.IsReady = true;
            state.IsPlaying = false;
            state.FirstFrameReceived = true;
            onVideoPrepared.Invoke(state.VideoIndex);

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] First frame ready for index {state.VideoIndex} on Player {state.Slot}. " +
                    $"Frame={player.frame}, Time={player.time:0.###}, TargetTexture={DescribeTexture(player.targetTexture)}, PlayerTexture={DescribeTexture(player.texture)}.",
                    this);
            }
        }

        private bool TryBeginPrepareSlot(PlayerSlotState state, int videoIndex, out int token)
        {
            token = 0;

            if (state == null || state.Player == null || sequenceConfig == null)
            {
                ReportFailure("Cannot prepare video because a required reference is missing.");
                return false;
            }

            if (!sequenceConfig.TryGetVideoPath(videoIndex, out string relativePath))
            {
                ReportFailure($"Missing video path at index {videoIndex}.");
                return false;
            }

            if (!CabinPortraitStreamingAssetsPathUtility.TryBuildFilePath(relativePath, out string fullPath) ||
                !CabinPortraitStreamingAssetsPathUtility.TryBuildFileUri(relativePath, out string fileUri))
            {
                ReportFailure($"Invalid StreamingAssets path: {relativePath}");
                return false;
            }

            if (!File.Exists(fullPath))
            {
                ReportFailure($"Video file not found.\nRelative Path: {relativePath}\nFull Path: {fullPath}\nURL: {fileUri}");
                return false;
            }

            StopSlot(state);
            token = ++tokenCounter;
            state.Token = token;
            state.VideoIndex = sequenceConfig.WrapIndex(videoIndex);
            state.IsPreparing = true;
            state.IsReady = false;
            state.IsPlaying = false;
            state.FirstFrameReceived = false;
            state.RelativePath = relativePath;
            state.FullPath = fullPath;
            state.Url = fileUri;

            ConfigurePlayer(state.Player);
            SetSlotAudioMuted(state, true);
            state.Player.url = fileUri;
            state.Player.Prepare();

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Preparing index {state.VideoIndex} on Player {state.Slot}. " +
                    $"RenderMode={state.Player.renderMode}, TargetTexture={DescribeTexture(state.Player.targetTexture)}, " +
                    $"URL={fileUri}\nFullPath={fullPath}",
                    this);
            }

            return true;
        }

        private bool PlayVisibleSlot(PlayerSlotState state)
        {
            if (state == null || state.Player == null || !state.IsReady || !state.Player.isPrepared)
            {
                ReportFailure("Cannot play because the target video is not prepared.");
                return false;
            }

            SetSlotAudioMuted(state, false);
            state.Player.isLooping = true;

            if (!state.Player.isPlaying)
            {
                state.Player.Play();
            }

            state.IsPlaying = true;

            if (displayController != null)
            {
                displayController.ShowSlot(state.Slot, GetTargetTexture(state));
            }

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Playing visible index {state.VideoIndex} on Player {state.Slot}. " +
                    $"RenderMode={state.Player.renderMode}, TargetTexture={DescribeTexture(state.Player.targetTexture)}, " +
                    $"PlayerTexture={DescribeTexture(state.Player.texture)}, DisplayTexture={DescribeTexture(GetTargetTexture(state))}.",
                    this);
            }

            onVideoStarted.Invoke(state.VideoIndex);
            return true;
        }

        private void ConfigurePlayer(VideoPlayer player)
        {
            player.source = VideoSource.Url;
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = true;
            player.sendFrameReadyEvents = false;
        }

        private static bool HasFirstFrame(VideoPlayer player)
        {
            return player != null &&
                   player.frame >= 0 &&
                   player.texture != null &&
                   player.texture.width > 0 &&
                   player.texture.height > 0;
        }

        private void SetSlotAudioMuted(PlayerSlotState state, bool muted)
        {
            if (state == null || state.Player == null)
            {
                return;
            }

            VideoPlayer player = state.Player;
            int trackCount = Mathf.Max(1, (int)player.controlledAudioTrackCount);

            for (ushort trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                try
                {
                    if (player.audioOutputMode == VideoAudioOutputMode.AudioSource)
                    {
                        AudioSource audioSource = player.GetTargetAudioSource(trackIndex);
                        if (audioSource != null)
                        {
                            audioSource.mute = muted;
                        }
                    }
                    else if (player.audioOutputMode == VideoAudioOutputMode.Direct)
                    {
                        player.SetDirectAudioMute(trackIndex, muted);
                    }
                }
                catch (Exception exception)
                {
                    if (ShouldLog)
                    {
                        Debug.LogWarning($"[CabinPortraits.Video] Failed to set audio mute={muted} on Player {state.Slot}, track {trackIndex}. {exception.Message}", this);
                    }

                    break;
                }
            }
        }

        private void StopSlot(PlayerSlotState state)
        {
            if (state == null)
            {
                return;
            }

            state.Token = ++tokenCounter;

            if (state.Player != null)
            {
                SetSlotAudioMuted(state, false);
                state.Player.sendFrameReadyEvents = false;
                state.Player.Stop();
            }

            state.IsPreparing = false;
            state.IsReady = false;
            state.IsPlaying = false;
            state.FirstFrameReceived = false;
            state.VideoIndex = -1;
            state.RelativePath = string.Empty;
            state.FullPath = string.Empty;
            state.Url = string.Empty;
        }

        private void ReportFailure(string message)
        {
            Debug.LogWarning($"[CabinPortraits.Video] {message}", this);
            onVideoError.Invoke(message);

            if (currentState != FlowState.ErrorRecovery)
            {
                TransitionTo(FlowState.ErrorRecovery);
            }
        }

        private void UnlockInput()
        {
            inputLocked = false;
            onInputUnlocked.Invoke();
        }

        private void UnlockSwitchAfterFailure()
        {
            isSwitching = false;
            inputLocked = false;
            onInputUnlocked.Invoke();
            switchCoroutine = null;
        }

        private void InitializeSlotReferences()
        {
            slotAState = UpdateSlotState(slotAState, videoPlayerA, CabinPortraitVideoSlot.A);
            slotBState = UpdateSlotState(slotBState, videoPlayerB, CabinPortraitVideoSlot.B);
        }

        private static PlayerSlotState UpdateSlotState(PlayerSlotState state, VideoPlayer player, CabinPortraitVideoSlot slot)
        {
            if (player == null)
            {
                return null;
            }

            if (state == null)
            {
                return new PlayerSlotState(player, slot);
            }

            state.Player = player;
            state.Slot = slot;
            return state;
        }

        private PlayerSlotState GetState(VideoPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (slotAState != null && player == slotAState.Player)
            {
                return slotAState;
            }

            if (slotBState != null && player == slotBState.Player)
            {
                return slotBState;
            }

            return null;
        }

        private static Texture GetTargetTexture(PlayerSlotState state)
        {
            if (state == null || state.Player == null)
            {
                return null;
            }

            return state.Player.targetTexture != null ? state.Player.targetTexture : state.Player.texture;
        }

        private static string DescribeTexture(Texture texture)
        {
            return texture != null ? $"{texture.name} ({texture.width}x{texture.height})" : "<null>";
        }

        private void Subscribe(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.errorReceived -= HandleErrorReceived;
            player.errorReceived += HandleErrorReceived;
        }

        private void Unsubscribe(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.errorReceived -= HandleErrorReceived;
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            PlayerSlotState state = GetState(player);
            string context = state != null
                ? $"Player {state.Slot}, index {state.VideoIndex}, path {state.RelativePath}"
                : "Unknown player";

            ReportFailure($"VideoPlayer error: {message}\n{context}");
        }

        private bool HasRequiredReferences()
        {
            if (sequenceConfig != null &&
                videoPlayerA != null &&
                videoPlayerB != null &&
                displayController != null &&
                sequenceConfig.VideoCount > 0)
            {
                return true;
            }

            ReportFailure("Missing sequenceConfig, two VideoPlayers, displayController, or video paths.");
            return false;
        }

        private bool TransitionTo(FlowState nextState)
        {
            if (currentState == nextState)
            {
                Debug.LogWarning($"[CabinPortraits.Flow] Ignored duplicate state transition to {nextState}.", this);
                return false;
            }

            if (!IsLegalTransition(currentState, nextState))
            {
                Debug.LogWarning($"[CabinPortraits.Flow] Illegal state transition: {currentState} -> {nextState}", this);
                return false;
            }

            FlowState previousState = currentState;
            currentState = nextState;
            Debug.Log($"[CabinPortraits.Flow] {previousState} -> {nextState}", this);
            InvokeStateChanged(previousState, nextState);
            return true;
        }

        private void InvokeStateChanged(FlowState previousState, FlowState nextState)
        {
            Action<FlowState, FlowState> handlers = StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<FlowState, FlowState> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(previousState, nextState);
                }
                catch (Exception exception)
                {
                    Debug.LogException(new Exception("[CabinPortraits.Flow] StateChanged handler failed.", exception), this);
                }
            }
        }

        private static bool IsLegalTransition(FlowState from, FlowState to)
        {
            if (to == FlowState.ErrorRecovery)
            {
                return from != FlowState.ErrorRecovery;
            }

            switch (from)
            {
                case FlowState.SystemInitializing:
                    return to == FlowState.ActivePreparing;
                case FlowState.ActivePreparing:
                    return to == FlowState.ActivePlaying;
                case FlowState.ActivePlaying:
                    return to == FlowState.Switching;
                case FlowState.Switching:
                    return to == FlowState.CoveredPreparing;
                case FlowState.CoveredPreparing:
                    return to == FlowState.ActivePlaying;
                case FlowState.ErrorRecovery:
                    return to == FlowState.ActivePreparing;
                default:
                    return false;
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

        private bool ShouldLog => sequenceConfig == null || sequenceConfig.VerboseLogs;
    }
}
