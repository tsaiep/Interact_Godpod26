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
            StandbyPreparing,
            ReadyForSwitch,
            Switching,
            HiddenWarming,
            ErrorRecovery
        }

        private static readonly FlowState[] StateSequence =
        {
            FlowState.SystemInitializing,
            FlowState.ActivePreparing,
            FlowState.ActivePlaying,
            FlowState.StandbyPreparing,
            FlowState.ReadyForSwitch,
            FlowState.Switching,
            FlowState.HiddenWarming,
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
        [SerializeField, Tooltip("Invoked when a Space request is accepted. Args: current index, next index.")]
        private CabinPortraitVideoSwitchEvent onSwitchRequested = new CabinPortraitVideoSwitchEvent();

        [SerializeField, Tooltip("Invoked immediately before waiting transitionSwitchDelay.")]
        private UnityEvent onTransitionStarted = new UnityEvent();

        [SerializeField, Tooltip("Invoked after the hidden warm-up has completed and the next video texture is visible behind the mask. Use this to start revealing the mask.")]
        private CabinPortraitVideoIndexEvent onReadyToReveal = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked after the active visible video index changes.")]
        private CabinPortraitVideoIndexEvent onVideoIndexChanged = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video is ready. Active startup uses first-frame ready; standby can require hidden full pre-roll first.")]
        private CabinPortraitVideoIndexEvent onVideoPrepared = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video starts playing visibly.")]
        private CabinPortraitVideoIndexEvent onVideoStarted = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a Space request is ignored. Reason is provided for debugging.")]
        private CabinPortraitVideoMessageEvent onInputRejected = new CabinPortraitVideoMessageEvent();

        [SerializeField] private UnityEvent onInputCooldownStarted = new UnityEvent();
        [SerializeField] private UnityEvent onInputCooldownEnded = new UnityEvent();
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
            public bool PrerollBeforeReady;
            public bool IsPrerolling;
            public bool PrerollLoopReceived;
            public string RelativePath = string.Empty;
            public string FullPath = string.Empty;
            public string Url = string.Empty;
            public Coroutine PrepareTimeoutCoroutine;
            public Coroutine FirstFrameCoroutine;
        }

        private PlayerSlotState slotAState;
        private PlayerSlotState slotBState;
        private PlayerSlotState activeSlot;
        private PlayerSlotState standbySlot;
        private Coroutine switchCoroutine;
        private Coroutine standbyPrepareCoroutine;
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
            StopCoroutineIfRunning(ref switchCoroutine);
            StopCoroutineIfRunning(ref standbyPrepareCoroutine);
            StopSlotCoroutines(slotAState);
            StopSlotCoroutines(slotBState);
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

            StopCoroutineIfRunning(ref switchCoroutine);
            StopCoroutineIfRunning(ref standbyPrepareCoroutine);
            StopSlot(slotAState);
            StopSlot(slotBState);

            initialized = true;
            inputLocked = false;
            isSwitching = false;
            currentIndex = -1;
            activeSlot = slotAState;
            standbySlot = slotBState;
            currentState = FlowState.SystemInitializing;

            if (!TransitionTo(FlowState.ActivePreparing))
            {
                return;
            }

            PrepareSlot(activeSlot, sequenceConfig.StartIndex, false);
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
            StopCoroutineIfRunning(ref switchCoroutine);
            StopCoroutineIfRunning(ref standbyPrepareCoroutine);
            StopSlot(slotAState);
            StopSlot(slotBState);

            if (displayController != null)
            {
                displayController.HideAll();
            }
        }

        private IEnumerator SwitchRoutine()
        {
            float acceptedAt = Time.unscaledTime;
            inputLocked = true;
            isSwitching = true;
            onInputCooldownStarted.Invoke();

            PlayerSlotState previousActiveSlot = activeSlot;
            PlayerSlotState nextActiveSlot = standbySlot;
            int previousIndex = currentIndex;
            int nextIndex = nextActiveSlot.VideoIndex;

            onSwitchRequested.Invoke(previousIndex, nextIndex);

            if (!TransitionTo(FlowState.Switching))
            {
                UnlockInputAfterFailedSwitch();
                yield break;
            }

            onTransitionStarted.Invoke();

            float transitionDelay = sequenceConfig != null ? sequenceConfig.TransitionSwitchDelay : 0.5f;
            if (transitionDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionDelay);
            }

            if (currentState != FlowState.Switching)
            {
                UnlockInputAfterFailedSwitch();
                yield break;
            }

            if (!TransitionTo(FlowState.HiddenWarming))
            {
                UnlockInputAfterFailedSwitch();
                yield break;
            }

            if (!BeginHiddenWarmup(nextActiveSlot))
            {
                UnlockInputAfterFailedSwitch();
                yield break;
            }

            float hiddenWarmupDuration = sequenceConfig != null ? sequenceConfig.HiddenWarmupDuration : 0.5f;
            if (hiddenWarmupDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(hiddenWarmupDuration);
            }

            if (currentState != FlowState.HiddenWarming)
            {
                UnlockInputAfterFailedSwitch();
                yield break;
            }

            if (!PlayVisibleSlot(nextActiveSlot))
            {
                UnlockInputAfterFailedSwitch();
                yield break;
            }

            PauseSlot(previousActiveSlot);

            activeSlot = nextActiveSlot;
            standbySlot = previousActiveSlot;
            currentIndex = nextIndex;
            onVideoIndexChanged.Invoke(currentIndex);
            onReadyToReveal.Invoke(currentIndex);

            TransitionTo(FlowState.ActivePlaying);
            ScheduleStandbyPrepare(sequenceConfig != null ? sequenceConfig.PrepareNextDelayAfterSwitch : 0f);
            isSwitching = false;

            float inputCooldown = sequenceConfig != null ? sequenceConfig.InputCooldown : 2f;
            float remainingCooldown = Mathf.Max(0f, inputCooldown - (Time.unscaledTime - acceptedAt));
            if (remainingCooldown > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingCooldown);
            }

            inputLocked = false;
            onInputCooldownEnded.Invoke();
            switchCoroutine = null;
        }

        private void ScheduleStandbyPrepare(float delay)
        {
            StopCoroutineIfRunning(ref standbyPrepareCoroutine);
            standbyPrepareCoroutine = StartCoroutine(StandbyPrepareRoutine(delay));
        }

        private IEnumerator StandbyPrepareRoutine(float delay)
        {
            if (delay > 0f)
            {
                if (ShouldLog)
                {
                    int nextIndex = sequenceConfig != null ? sequenceConfig.GetNextIndex(currentIndex) : -1;
                    Debug.Log($"[CabinPortraits.Video] Waiting {delay:0.###} seconds before preparing standby index {nextIndex}.", this);
                }

                yield return new WaitForSecondsRealtime(delay);
            }

            standbyPrepareCoroutine = null;

            if (!initialized || currentState != FlowState.ActivePlaying || standbySlot == null || standbySlot == activeSlot)
            {
                yield break;
            }

            if (!TransitionTo(FlowState.StandbyPreparing))
            {
                yield break;
            }

            PrepareSlot(standbySlot, sequenceConfig.GetNextIndex(currentIndex), sequenceConfig.PrerollStandbyBeforeSwitch);
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
                rejectionReason = "Input is cooling down or a switch is already running.";
                return false;
            }

            if (currentState != FlowState.ReadyForSwitch)
            {
                rejectionReason = $"Flow state is {currentState}. Expected {FlowState.ReadyForSwitch}.";
                return false;
            }

            if (activeSlot == null || !activeSlot.IsPlaying)
            {
                rejectionReason = "No active video is playing yet.";
                return false;
            }

            if (standbySlot == null || standbySlot.Player == null)
            {
                rejectionReason = "Standby VideoPlayer is missing.";
                return false;
            }

            if (!standbySlot.IsReady || !standbySlot.Player.isPrepared)
            {
                rejectionReason = "Next video is not prepared yet.";
                return false;
            }

            return true;
        }

        private void PrepareSlot(PlayerSlotState state, int videoIndex, bool prerollBeforeReady)
        {
            if (state == null || state.Player == null || sequenceConfig == null)
            {
                ReportFailure("Cannot prepare video because a required reference is missing.");
                return;
            }

            if (!sequenceConfig.TryGetVideoPath(videoIndex, out string relativePath))
            {
                ReportFailure($"Missing video path at index {videoIndex}.");
                return;
            }

            if (!CabinPortraitStreamingAssetsPathUtility.TryBuildFilePath(relativePath, out string fullPath) ||
                !CabinPortraitStreamingAssetsPathUtility.TryBuildFileUri(relativePath, out string fileUri))
            {
                ReportFailure($"Invalid StreamingAssets path: {relativePath}");
                return;
            }

            if (!File.Exists(fullPath))
            {
                ReportFailure($"Video file not found.\nRelative Path: {relativePath}\nFull Path: {fullPath}\nURL: {fileUri}");
                return;
            }

            StopSlot(state);
            state.Token = ++tokenCounter;
            state.VideoIndex = sequenceConfig.WrapIndex(videoIndex);
            state.IsPreparing = true;
            state.IsReady = false;
            state.IsPlaying = false;
            state.FirstFrameReceived = false;
            state.PrerollBeforeReady = prerollBeforeReady;
            state.IsPrerolling = false;
            state.PrerollLoopReceived = false;
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
                    $"FullPreroll={prerollBeforeReady}, " +
                    $"RenderMode={state.Player.renderMode}, TargetTexture={DescribeTexture(state.Player.targetTexture)}, " +
                    $"URL={fileUri}\nFullPath={fullPath}",
                    this);
            }

            state.PrepareTimeoutCoroutine = StartCoroutine(PrepareTimeoutRoutine(state, state.Token));
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

        private bool BeginHiddenWarmup(PlayerSlotState state)
        {
            if (state == null || state.Player == null || !state.IsReady || !state.Player.isPrepared)
            {
                ReportFailure("Cannot warm up because the target video is not prepared.");
                return false;
            }

            bool muteDuringWarmup = sequenceConfig == null || sequenceConfig.MuteAudioDuringHiddenWarmup;
            SetSlotAudioMuted(state, muteDuringWarmup);
            state.Player.isLooping = true;

            if (!state.Player.isPlaying)
            {
                state.Player.Play();
            }

            state.IsPlaying = true;

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Hidden warm-up started for index {state.VideoIndex} on Player {state.Slot}. " +
                    $"Muted={muteDuringWarmup}, Time={state.Player.time:0.###}, Frame={state.Player.frame}.",
                    this);
            }

            return true;
        }

        private void PauseSlot(PlayerSlotState state)
        {
            if (state == null || state.Player == null)
            {
                return;
            }

            SetSlotAudioMuted(state, true);

            if (state.Player.isPlaying)
            {
                state.Player.Pause();
            }

            state.Player.sendFrameReadyEvents = false;
            state.IsPreparing = false;
            state.IsReady = false;
            state.IsPlaying = false;
            state.FirstFrameReceived = false;
            state.PrerollBeforeReady = false;
            state.IsPrerolling = false;
            state.PrerollLoopReceived = false;

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] Paused old active index {state.VideoIndex} on Player {state.Slot}.", this);
            }
        }

        private void ConfigurePlayer(VideoPlayer player)
        {
            player.source = VideoSource.Url;
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = true;
            player.sendFrameReadyEvents = true;
        }

        private void HandlePrepareCompleted(VideoPlayer player)
        {
            PlayerSlotState state = GetState(player);
            if (state == null || !state.IsPreparing)
            {
                return;
            }

            StopCoroutineIfRunning(ref state.PrepareTimeoutCoroutine);
            state.FirstFrameCoroutine = StartCoroutine(FirstFrameRoutine(state, state.Token));
        }

        private IEnumerator FirstFrameRoutine(PlayerSlotState state, int token)
        {
            float timeout = sequenceConfig != null ? sequenceConfig.FirstFrameTimeout : 5f;
            float start = Time.unscaledTime;
            VideoPlayer player = state.Player;

            SetSlotAudioMuted(state, true);

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

            if (state.Token != token || !state.IsPreparing)
            {
                state.FirstFrameCoroutine = null;
                yield break;
            }

            if (!state.FirstFrameReceived)
            {
                state.FirstFrameCoroutine = null;
                ReportFailure($"First frame timeout after {timeout:0.##} seconds for index {state.VideoIndex}.\n{state.FullPath}");
                yield break;
            }

            if (player != null)
            {
                player.sendFrameReadyEvents = false;
            }

            if (state.PrerollBeforeReady)
            {
                yield return FullPrerollStandbyRoutine(state, token, player);

                state.FirstFrameCoroutine = null;

                if (state.Token != token || !state.IsPreparing || currentState == FlowState.ErrorRecovery)
                {
                    yield break;
                }

                CompletePreparedSlot(
                    state,
                    player,
                    $"Standby full pre-roll ready for index {state.VideoIndex} on Player {state.Slot}. " +
                    $"Frame={player.frame}, Time={player.time:0.###}, TargetTexture={DescribeTexture(player.targetTexture)}, PlayerTexture={DescribeTexture(player.texture)}.");
                yield break;
            }

            state.FirstFrameCoroutine = null;

            if (player != null && player.isPlaying)
            {
                player.Pause();
            }

            CompletePreparedSlot(
                state,
                player,
                $"First frame ready for index {state.VideoIndex} on Player {state.Slot}. " +
                $"Frame={player.frame}, TargetTexture={DescribeTexture(player.targetTexture)}, PlayerTexture={DescribeTexture(player.texture)}.");
        }

        private IEnumerator FullPrerollStandbyRoutine(PlayerSlotState state, int token, VideoPlayer player)
        {
            if (player == null)
            {
                ReportFailure($"Cannot pre-roll index {state.VideoIndex} because the VideoPlayer is missing.");
                yield break;
            }

            state.IsPrerolling = true;
            state.PrerollLoopReceived = false;
            state.IsPlaying = true;

            SetSlotAudioMuted(state, true);
            player.sendFrameReadyEvents = false;
            player.isLooping = false;

            if (!player.isPlaying)
            {
                player.Play();
            }

            float timeout = GetStandbyPrerollTimeout(player);
            float start = Time.unscaledTime;

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Hidden full pre-roll started for index {state.VideoIndex} on Player {state.Slot}. " +
                    $"Timeout={timeout:0.###}, Length={player.length:0.###}, Time={player.time:0.###}, Frame={player.frame}.",
                    this);
            }

            while (state.Token == token &&
                   state.IsPreparing &&
                   state.IsPrerolling &&
                   !state.PrerollLoopReceived &&
                   Time.unscaledTime - start < timeout)
            {
                if (HasNaturallyReachedEnd(player))
                {
                    state.PrerollLoopReceived = true;
                    break;
                }

                yield return null;
            }

            if (state.Token != token || !state.IsPreparing)
            {
                yield break;
            }

            if (!state.PrerollLoopReceived)
            {
                state.IsPrerolling = false;
                state.IsPlaying = false;
                ReportFailure($"Hidden pre-roll timeout after {timeout:0.##} seconds for index {state.VideoIndex}.\n{state.FullPath}");
                yield break;
            }

            if (player.isPlaying)
            {
                player.Pause();
            }

            state.IsPlaying = false;
            player.isLooping = true;

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Hidden full pre-roll completed for index {state.VideoIndex} on Player {state.Slot}. " +
                    $"Rewinding to start. Time={player.time:0.###}, Frame={player.frame}.",
                    this);
            }

            yield return RewindPreparedPlayerToStart(state, token, player);

            if (state.Token == token && state.IsPreparing)
            {
                state.IsPrerolling = false;
            }
        }

        private IEnumerator RewindPreparedPlayerToStart(PlayerSlotState state, int token, VideoPlayer player)
        {
            if (player == null)
            {
                ReportFailure($"Cannot rewind index {state.VideoIndex} because the VideoPlayer is missing.");
                yield break;
            }

            if (!player.canSetTime)
            {
                ReportFailure($"Cannot rewind index {state.VideoIndex} because {player.name} does not support time seeking.");
                yield break;
            }

            SetSlotAudioMuted(state, true);
            player.time = 0d;

            if (!player.isPlaying)
            {
                player.Play();
            }

            yield return null;

            float timeout = sequenceConfig != null ? sequenceConfig.FirstFrameTimeout : 5f;
            float start = Time.unscaledTime;

            while (state.Token == token && state.IsPreparing && Time.unscaledTime - start < timeout)
            {
                if (IsAtStartWithTexture(player))
                {
                    break;
                }

                yield return null;
            }

            if (state.Token != token || !state.IsPreparing)
            {
                yield break;
            }

            if (!IsAtStartWithTexture(player))
            {
                ReportFailure($"Rewind-to-start timeout after {timeout:0.##} seconds for index {state.VideoIndex}.\n{state.FullPath}");
                yield break;
            }

            if (player.isPlaying)
            {
                player.Pause();
            }

            state.IsPlaying = false;

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Hidden pre-rolled index {state.VideoIndex} rewound and paused on Player {state.Slot}. " +
                    $"Time={player.time:0.###}, Frame={player.frame}.",
                    this);
            }
        }

        private void CompletePreparedSlot(PlayerSlotState state, VideoPlayer player, string logMessage)
        {
            if (player != null)
            {
                player.sendFrameReadyEvents = false;
            }

            state.IsPreparing = false;
            state.IsReady = true;
            state.IsPlaying = false;
            state.IsPrerolling = false;
            onVideoPrepared.Invoke(state.VideoIndex);

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] {logMessage}", this);
            }

            HandlePreparedSlotReady(state);
        }

        private void HandlePreparedSlotReady(PlayerSlotState state)
        {
            if (currentState == FlowState.ActivePreparing)
            {
                if (state != activeSlot)
                {
                    LogUnexpectedPreparedSlot(state, FlowState.ActivePreparing);
                    return;
                }

                if (!PlayVisibleSlot(activeSlot))
                {
                    return;
                }

                currentIndex = activeSlot.VideoIndex;
                onVideoIndexChanged.Invoke(currentIndex);

                if (TransitionTo(FlowState.ActivePlaying))
                {
                    ScheduleStandbyPrepare(0f);
                }

                return;
            }

            if (currentState == FlowState.StandbyPreparing)
            {
                if (state != standbySlot)
                {
                    LogUnexpectedPreparedSlot(state, FlowState.StandbyPreparing);
                    return;
                }

                TransitionTo(FlowState.ReadyForSwitch);
                return;
            }

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] Ignored prepared-slot callback while state is {currentState}.", this);
            }
        }

        private float GetStandbyPrerollTimeout(VideoPlayer player)
        {
            float configuredTimeout = sequenceConfig != null ? sequenceConfig.StandbyPrerollTimeout : 120f;

            if (player != null && IsFinitePositive(player.length))
            {
                configuredTimeout = Mathf.Max(configuredTimeout, (float)player.length + 5f);
            }

            return configuredTimeout;
        }

        private static bool HasNaturallyReachedEnd(VideoPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.isPlaying)
            {
                return false;
            }

            if (IsFinitePositive(player.length) && player.time >= Math.Max(0d, player.length - 0.1d))
            {
                return true;
            }

            ulong frameCount = player.frameCount;
            return frameCount > 0UL && player.frame >= 0 && (ulong)player.frame >= frameCount - 1UL;
        }

        private static bool IsAtStartWithTexture(VideoPlayer player)
        {
            if (player == null || player.texture == null || player.texture.width <= 0 || player.texture.height <= 0)
            {
                return false;
            }

            if (player.frame >= 0 && player.frame <= 2)
            {
                return true;
            }

            if (player.frameCount > 0UL)
            {
                return false;
            }

            return player.time >= 0d && player.time <= 0.25d;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private void HandleFrameReady(VideoPlayer player, long frameIdx)
        {
            PlayerSlotState state = GetState(player);
            if (state == null || !state.IsPreparing)
            {
                return;
            }

            state.FirstFrameReceived = true;
            player.sendFrameReadyEvents = false;

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] Frame ready for index {state.VideoIndex} on Player {state.Slot}. Frame={frameIdx}.", this);
            }
        }

        private void HandleLoopPointReached(VideoPlayer player)
        {
            PlayerSlotState state = GetState(player);
            if (state == null || player == null)
            {
                return;
            }

            if (state.IsPreparing && state.IsPrerolling && !player.isLooping)
            {
                state.PrerollLoopReceived = true;

                if (ShouldLog)
                {
                    Debug.Log(
                        $"[CabinPortraits.Video] Hidden pre-roll loop point reached for index {state.VideoIndex} on Player {state.Slot}. " +
                        $"Time={player.time:0.###}, Frame={player.frame}.",
                        this);
                }

                return;
            }

            if (!state.IsPlaying || player.isLooping)
            {
                return;
            }

            state.IsPlaying = false;
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            PlayerSlotState state = GetState(player);
            string context = state != null
                ? $"Player {state.Slot}, index {state.VideoIndex}, path {state.RelativePath}"
                : "Unknown player";

            ReportFailure($"VideoPlayer error: {message}\n{context}");
        }

        private IEnumerator PrepareTimeoutRoutine(PlayerSlotState state, int token)
        {
            float timeout = sequenceConfig != null ? sequenceConfig.PrepareTimeout : 10f;
            yield return new WaitForSecondsRealtime(timeout);

            if (state.Token != token || !state.IsPreparing)
            {
                yield break;
            }

            ReportFailure($"Prepare timeout after {timeout:0.##} seconds for index {state.VideoIndex}.\n{state.FullPath}");
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

        private void ReportFailure(string message)
        {
            Debug.LogWarning($"[CabinPortraits.Video] {message}", this);
            onVideoError.Invoke(message);

            if (currentState != FlowState.ErrorRecovery)
            {
                TransitionTo(FlowState.ErrorRecovery);
            }
        }

        private void UnlockInputAfterFailedSwitch()
        {
            isSwitching = false;
            inputLocked = false;
            onInputCooldownEnded.Invoke();
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

        private void StopSlot(PlayerSlotState state)
        {
            if (state == null)
            {
                return;
            }

            state.Token = ++tokenCounter;
            StopSlotCoroutines(state);

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
            state.PrerollBeforeReady = false;
            state.IsPrerolling = false;
            state.PrerollLoopReceived = false;
            state.VideoIndex = -1;
            state.RelativePath = string.Empty;
            state.FullPath = string.Empty;
            state.Url = string.Empty;
        }

        private void StopSlotCoroutines(PlayerSlotState state)
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
                    return to == FlowState.StandbyPreparing;
                case FlowState.StandbyPreparing:
                    return to == FlowState.ReadyForSwitch;
                case FlowState.ReadyForSwitch:
                    return to == FlowState.Switching;
                case FlowState.Switching:
                    return to == FlowState.HiddenWarming;
                case FlowState.HiddenWarming:
                    return to == FlowState.ActivePlaying;
                case FlowState.ErrorRecovery:
                    return to == FlowState.ActivePreparing;
                default:
                    return false;
            }
        }

        private void LogUnexpectedPreparedSlot(PlayerSlotState state, FlowState expectedState)
        {
            if (!ShouldLog)
            {
                return;
            }

            string slotName = state != null ? state.Slot.ToString() : "<null>";
            int videoIndex = state != null ? state.VideoIndex : -1;
            Debug.Log($"[CabinPortraits.Video] Ignored prepared slot {slotName}, index {videoIndex}. Expected active state {expectedState}.", this);
        }

        private bool ShouldLog => sequenceConfig == null || sequenceConfig.VerboseLogs;
    }
}
