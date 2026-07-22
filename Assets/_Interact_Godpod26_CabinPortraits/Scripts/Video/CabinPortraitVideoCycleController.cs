using System;
using System.Collections;
using System.IO;
using CabinPortraits.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
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

    [Serializable]
    public sealed class CabinPortraitFlowStateEvent : UnityEvent<CabinPortraitVideoCycleController.FlowState>
    {
    }

    [Serializable]
    public sealed class CabinPortraitFlowStateEvents
    {
        [SerializeField] private CabinPortraitFlowStateEvent onStateEntered = new CabinPortraitFlowStateEvent();
        [SerializeField] private UnityEvent onSystemInitializingEntered = new UnityEvent();

        [Header("Current Flow")]
        [SerializeField] private UnityEvent onInitialPreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onInitialEntered = new UnityEvent();
        [SerializeField] private UnityEvent onManualVideoPreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onManualVideoPlayingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onTimerVideoPreparingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onTimerVideoPlayingEntered = new UnityEvent();
        [SerializeField] private UnityEvent onReturningToInitialEntered = new UnityEvent();
        [SerializeField] private UnityEvent onManualReturningToInitialEntered = new UnityEvent();
        [SerializeField] private UnityEvent onTimerReturningToInitialEntered = new UnityEvent();
        [SerializeField] private UnityEvent onCoveredRestoringInitialEntered = new UnityEvent();
        [SerializeField] private UnityEvent onManualCoveredRestoringInitialEntered = new UnityEvent();
        [SerializeField] private UnityEvent onTimerCoveredRestoringInitialEntered = new UnityEvent();

        [SerializeField] private UnityEvent onErrorRecoveryEntered = new UnityEvent();

        public void Invoke(CabinPortraitVideoCycleController.FlowState state, CabinPortraitSwitchRequestSource source, UnityEngine.Object context)
        {
            InvokeSafely(() => onStateEntered.Invoke(state), $"onStateEntered({state})", context);

            switch (state)
            {
                case CabinPortraitVideoCycleController.FlowState.SystemInitializing:
                    InvokeSafely(onSystemInitializingEntered.Invoke, nameof(onSystemInitializingEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.InitialPreparing:
                    InvokeSafely(onInitialPreparingEntered.Invoke, nameof(onInitialPreparingEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.Initial:
                    InvokeSafely(onInitialEntered.Invoke, nameof(onInitialEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.ManualVideoPreparing:
                    InvokeSafely(onManualVideoPreparingEntered.Invoke, nameof(onManualVideoPreparingEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.ManualVideoPlaying:
                    InvokeSafely(onManualVideoPlayingEntered.Invoke, nameof(onManualVideoPlayingEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.TimerVideoPreparing:
                    InvokeSafely(onTimerVideoPreparingEntered.Invoke, nameof(onTimerVideoPreparingEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.TimerVideoPlaying:
                    InvokeSafely(onTimerVideoPlayingEntered.Invoke, nameof(onTimerVideoPlayingEntered), context);
                    break;
                case CabinPortraitVideoCycleController.FlowState.ReturningToInitial:
                    InvokeSafely(onReturningToInitialEntered.Invoke, nameof(onReturningToInitialEntered), context);
                    if (source == CabinPortraitSwitchRequestSource.Auto)
                    {
                        InvokeSafely(onTimerReturningToInitialEntered.Invoke, nameof(onTimerReturningToInitialEntered), context);
                    }
                    else
                    {
                        InvokeSafely(onManualReturningToInitialEntered.Invoke, nameof(onManualReturningToInitialEntered), context);
                    }
                    break;
                case CabinPortraitVideoCycleController.FlowState.CoveredRestoringInitial:
                    InvokeSafely(onCoveredRestoringInitialEntered.Invoke, nameof(onCoveredRestoringInitialEntered), context);
                    if (source == CabinPortraitSwitchRequestSource.Auto)
                    {
                        InvokeSafely(onTimerCoveredRestoringInitialEntered.Invoke, nameof(onTimerCoveredRestoringInitialEntered), context);
                    }
                    else
                    {
                        InvokeSafely(onManualCoveredRestoringInitialEntered.Invoke, nameof(onManualCoveredRestoringInitialEntered), context);
                    }
                    break;
                case CabinPortraitVideoCycleController.FlowState.ErrorRecovery:
                    InvokeSafely(onErrorRecoveryEntered.Invoke, nameof(onErrorRecoveryEntered), context);
                    break;
            }
        }

        private static void InvokeSafely(Action action, string eventName, UnityEngine.Object context)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(new Exception($"[CabinPortraits.Flow] UnityEvent failed: {eventName}", exception), context);
            }
        }
    }

    public enum CabinPortraitSwitchKey
    {
        Space,
        Enter
    }

    public enum CabinPortraitSwitchRequestSource
    {
        ManualInput,
        Auto
    }

    public sealed class CabinPortraitVideoCycleController : MonoBehaviour
    {
        public enum FlowState
        {
            SystemInitializing,
            InitialPreparing,
            Initial,
            ManualVideoPreparing,
            ManualVideoPlaying,
            TimerVideoPreparing,
            TimerVideoPlaying,
            ReturningToInitial,
            CoveredRestoringInitial,
            ErrorRecovery
        }

        private static readonly FlowState[] StateSequence =
        {
            FlowState.SystemInitializing,
            FlowState.InitialPreparing,
            FlowState.Initial,
            FlowState.ManualVideoPreparing,
            FlowState.ManualVideoPlaying,
            FlowState.TimerVideoPreparing,
            FlowState.TimerVideoPlaying,
            FlowState.ReturningToInitial,
            FlowState.CoveredRestoringInitial,
            FlowState.ErrorRecovery
        };

        [Header("Config")]
        [SerializeField, Tooltip("Video path lists and timing settings.")]
        private CabinPortraitVideoSequenceConfig sequenceConfig;

        [Header("Manual Players")]
        [SerializeField, Tooltip("First persistent manual VideoPlayer. Assign its own RenderTexture as Target Texture.")]
        private VideoPlayer videoPlayerA;

        [SerializeField, Tooltip("Second persistent manual VideoPlayer. Assign its own RenderTexture as Target Texture.")]
        private VideoPlayer videoPlayerB;

        [Header("Timer Players")]
        [SerializeField, Tooltip("Optional first persistent timer VideoPlayer. Leave empty to fall back to the manual player pipeline.")]
        private VideoPlayer timerVideoPlayerA;

        [SerializeField, Tooltip("Optional second persistent timer VideoPlayer. Leave empty to fall back to the manual player pipeline.")]
        private VideoPlayer timerVideoPlayerB;

        [Header("Display")]
        [SerializeField, Tooltip("Renderer display switcher for the active video texture and the initial static Quad.")]
        private CabinPortraitVideoDisplayController displayController;

        [Header("Input")]
        [SerializeField, Tooltip("When enabled, the controller listens for the configured key in Update.")]
        private bool enableKeyboardInput = true;

        [SerializeField, Tooltip("Key used to request the next manual video. Enter accepts both Return and Keypad Enter.")]
        private CabinPortraitSwitchKey switchKey = CabinPortraitSwitchKey.Space;

        [Header("Runtime Debug")]
        [SerializeField, Tooltip("All flow states for Inspector display only. This list is not used to drive state transitions.")]
        private FlowState[] visibleStateSequence = (FlowState[])StateSequence.Clone();

        [SerializeField, Tooltip("Current flow state. Runtime display only; do not edit during Play Mode.")]
        private FlowState currentState = FlowState.SystemInitializing;

        [Header("Unity Events")]
        [SerializeField, Tooltip("Invoked when a manual video request is accepted. Args: previous manual index, next manual index.")]
        private CabinPortraitVideoSwitchEvent onSwitchRequested = new CabinPortraitVideoSwitchEvent();

        [SerializeField, Tooltip("Invoked when a timer video request is accepted. Args: previous timer index, next timer index.")]
        [FormerlySerializedAs("onAutoSwitchRequested")]
        private CabinPortraitVideoSwitchEvent onTimerVideoRequested = new CabinPortraitVideoSwitchEvent();

        [SerializeField, Tooltip("Invoked when a manual button video reaches the configured remaining seconds before end.")]
        [FormerlySerializedAs("onTransitionStarted")]
        private UnityEvent onManualTransitionStarted = new UnityEvent();

        [SerializeField, Tooltip("Invoked when a timer video reaches the configured remaining seconds before end.")]
        private UnityEvent onTimerTransitionStarted = new UnityEvent();

        [SerializeField, Tooltip("Invoked after the initial static display is restored behind a fully covered manual transition. Arg: completed manual video index.")]
        [FormerlySerializedAs("onReadyToReveal")]
        private CabinPortraitVideoIndexEvent onManualReadyToReveal = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked after the initial static display is restored behind a fully covered timer transition. Arg: completed timer video index.")]
        private CabinPortraitVideoIndexEvent onTimerReadyToReveal = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked after the initial static display is restored behind a fully covered manual transition.")]
        [FormerlySerializedAs("onInitialReadyToReveal")]
        private UnityEvent onManualInitialReadyToReveal = new UnityEvent();

        [SerializeField, Tooltip("Invoked after the initial static display is restored behind a fully covered timer transition.")]
        private UnityEvent onTimerInitialReadyToReveal = new UnityEvent();

        [SerializeField, Tooltip("Invoked after the active visible video index changes. Timer and manual sequences have independent index ranges.")]
        private CabinPortraitVideoIndexEvent onVideoIndexChanged = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video has been prepared to its first frame.")]
        private CabinPortraitVideoIndexEvent onVideoPrepared = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video starts playing visibly.")]
        private CabinPortraitVideoIndexEvent onVideoStarted = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a manual button video starts playing visibly. Arg: manual video index.")]
        private CabinPortraitVideoIndexEvent onManualVideoStarted = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a timer video starts playing visibly. Arg: timer video index.")]
        private CabinPortraitVideoIndexEvent onTimerVideoStarted = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a switch request is ignored. Reason is provided for debugging.")]
        private CabinPortraitVideoMessageEvent onInputRejected = new CabinPortraitVideoMessageEvent();

        [SerializeField, Tooltip("Invoked when input is locked for startup, playback, or transition.")]
        private UnityEvent onInputLocked = new UnityEvent();

        [SerializeField, Tooltip("Invoked when input is unlocked after returning to the initial screen.")]
        private UnityEvent onInputUnlocked = new UnityEvent();

        [SerializeField] private CabinPortraitVideoMessageEvent onVideoError = new CabinPortraitVideoMessageEvent();

        [SerializeField, Tooltip("Invoked after entering a flow state. Includes one generic state event and one event per state.")]
        private CabinPortraitFlowStateEvents stateEvents = new CabinPortraitFlowStateEvents();

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
            public bool PlaybackCompleted;
            public bool FirstFrameReceived;
            public bool PrimingAudioMuted;
            public bool PrimingDirectAudioWasMuted;
            public VideoAudioOutputMode PrimingAudioOutputMode;
            public AudioSource PrimingAudioSource;
            public bool PrimingAudioSourceWasMuted;
            public long FirstFrameIndex = -1;
            public string RelativePath = string.Empty;
            public string FullPath = string.Empty;
            public string Url = string.Empty;
        }

        private PlayerSlotState slotAState;
        private PlayerSlotState slotBState;
        private PlayerSlotState timerSlotAState;
        private PlayerSlotState timerSlotBState;
        private PlayerSlotState activeSlot;
        private PlayerSlotState inactiveSlot;
        private Coroutine startupCoroutine;
        private Coroutine switchCoroutine;
        private int tokenCounter;
        private int currentIndex = -1;
        private int nextManualIndex;
        private int nextTimerIndex;
        private int lastManualIndex = -1;
        private int lastTimerIndex = -1;
        private bool initialized;
        private bool inputLocked;
        private bool isSwitching;
        private float nextTimerVideoAt = -1f;
        private float lastTimerVideoDelay = -1f;
        private CabinPortraitSwitchRequestSource activeRequestSource = CabinPortraitSwitchRequestSource.ManualInput;

        public int CurrentIndex => currentIndex;
        public int NextManualIndex => nextManualIndex;
        public int NextTimerIndex => nextTimerIndex;
        public int LastManualIndex => lastManualIndex;
        public int LastTimerIndex => lastTimerIndex;
        public FlowState CurrentState => currentState;
        public bool IsInitialized => initialized;
        public bool IsSwitching => isSwitching;
        public bool IsPlaybackActive => currentState == FlowState.ManualVideoPlaying || currentState == FlowState.TimerVideoPlaying;
        public bool CanSwitch => CanAcceptSwitchRequest(CabinPortraitSwitchRequestSource.ManualInput, out _);

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
            Subscribe(timerVideoPlayerA);
            Subscribe(timerVideoPlayerB);
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
            if (enableKeyboardInput && IsSwitchKeyDown())
            {
                RequestNextVideo();
            }

            UpdateTimerVideoRequest();
        }

        private bool IsSwitchKeyDown()
        {
            switch (switchKey)
            {
                case CabinPortraitSwitchKey.Enter:
                    return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                case CabinPortraitSwitchKey.Space:
                default:
                    return Input.GetKeyDown(KeyCode.Space);
            }
        }

        private void OnDisable()
        {
            Unsubscribe(videoPlayerA);
            Unsubscribe(videoPlayerB);
            Unsubscribe(timerVideoPlayerA);
            Unsubscribe(timerVideoPlayerB);
            StopCoroutineIfRunning(ref startupCoroutine);
            StopCoroutineIfRunning(ref switchCoroutine);
            RestorePrimingAudioForSlots();
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
            StopAllSlots();

            initialized = true;
            isSwitching = false;
            activeRequestSource = CabinPortraitSwitchRequestSource.ManualInput;
            currentIndex = -1;
            nextManualIndex = sequenceConfig.StartIndex;
            nextTimerIndex = sequenceConfig.TimerStartIndex;
            lastManualIndex = -1;
            lastTimerIndex = -1;
            activeSlot = GetFirstAvailableSlot(CabinPortraitSwitchRequestSource.ManualInput);
            inactiveSlot = GetOtherSlot(CabinPortraitSwitchRequestSource.ManualInput, activeSlot);
            ClearTimerVideoSchedule();
            EnterStateDirectly(FlowState.SystemInitializing);

            SetInputLocked(true);
            startupCoroutine = StartCoroutine(InitializeAndShowInitialRoutine());
        }

        public bool RequestNextVideo()
        {
            return RequestNextVideo(CabinPortraitSwitchRequestSource.ManualInput);
        }

        public void ResetToStart()
        {
            InitializeAndPlay();
        }

        public void StopAllVideos()
        {
            initialized = false;
            isSwitching = false;
            activeRequestSource = CabinPortraitSwitchRequestSource.ManualInput;
            currentIndex = -1;
            ClearTimerVideoSchedule();
            EnterStateDirectly(FlowState.SystemInitializing);
            StopCoroutineIfRunning(ref startupCoroutine);
            StopCoroutineIfRunning(ref switchCoroutine);
            StopAllSlots();
            SetInputLocked(false);

            if (displayController != null)
            {
                displayController.HideAll();
            }
        }

        private IEnumerator InitializeAndShowInitialRoutine()
        {
            if (!TransitionTo(FlowState.InitialPreparing))
            {
                SetInputLocked(false);
                yield break;
            }

            ShowInitialDisplay();

            if (!TransitionTo(FlowState.Initial))
            {
                SetInputLocked(false);
                yield break;
            }

            ScheduleNextTimerVideo();
            SetInputLocked(false);
            startupCoroutine = null;
        }

        private bool RequestNextVideo(CabinPortraitSwitchRequestSource source)
        {
            if (!CanAcceptSwitchRequest(source, out string rejectionReason))
            {
                if (ShouldLog)
                {
                    Debug.Log($"[CabinPortraits.Video] Ignored {DescribeSwitchRequestSource(source)} request. {rejectionReason}", this);
                }

                onInputRejected.Invoke(rejectionReason);
                return false;
            }

            int previousIndex = GetLastIndex(source);
            int nextIndex = GetNextIndex(source);
            InvokeSwitchRequested(source, previousIndex, nextIndex);
            ClearTimerVideoSchedule();
            switchCoroutine = StartCoroutine(PlaySingleVideoRoutine(source, nextIndex));
            return true;
        }

        private IEnumerator PlaySingleVideoRoutine(CabinPortraitSwitchRequestSource source, int requestedIndex)
        {
            SetInputLocked(true);
            isSwitching = true;
            activeRequestSource = source;

            PlayerSlotState playbackSlot = GetFirstAvailableSlot(source);
            PlayerSlotState standbySlot = GetOtherSlot(source, playbackSlot);
            FlowState preparingState = GetPreparingState(source);
            FlowState playingState = GetPlayingState(source);

            StopSlot(playbackSlot);
            StopSlot(standbySlot);

            if (!TransitionTo(preparingState))
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            yield return PrepareSlotForFirstFrame(playbackSlot, source, requestedIndex);

            if (currentState == FlowState.ErrorRecovery || playbackSlot == null || !playbackSlot.IsReady)
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            if (!PlayVisibleSlot(playbackSlot, source))
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            activeSlot = playbackSlot;
            inactiveSlot = standbySlot;
            currentIndex = playbackSlot.VideoIndex;
            SetLastIndex(source, currentIndex);
            AdvanceNextIndex(source, currentIndex);
            onVideoIndexChanged.Invoke(currentIndex);

            if (!TransitionTo(playingState))
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            int playbackToken = playbackSlot.Token;
            yield return WaitForReturnTransitionTrigger(playbackSlot, playbackToken);

            if (currentState == FlowState.ErrorRecovery || playbackSlot == null || playbackSlot.Token != playbackToken)
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            int completedIndex = currentIndex;
            if (!TransitionTo(FlowState.ReturningToInitial))
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            InvokeTransitionStarted(source);

            float transitionDelay = sequenceConfig != null ? sequenceConfig.TransitionCoverDelay : 0.5f;
            if (transitionDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionDelay);
            }

            if (currentState != FlowState.ReturningToInitial)
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            if (!TransitionTo(FlowState.CoveredRestoringInitial))
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            StopSlot(playbackSlot);
            ShowInitialDisplay();
            currentIndex = -1;
            InvokeReadyToReveal(source, completedIndex);

            if (!TransitionTo(FlowState.Initial))
            {
                ReturnToInitialAfterFailure();
                yield break;
            }

            isSwitching = false;
            switchCoroutine = null;
            ScheduleNextTimerVideo();
            SetInputLocked(false);
        }

        private IEnumerator WaitForReturnTransitionTrigger(PlayerSlotState state, int token)
        {
            while (state != null &&
                   state.Token == token &&
                   currentState != FlowState.ErrorRecovery &&
                   !ShouldStartReturnTransition(state))
            {
                yield return null;
            }
        }

        private bool ShouldStartReturnTransition(PlayerSlotState state)
        {
            if (state == null || state.Player == null)
            {
                return true;
            }

            if (state.PlaybackCompleted)
            {
                return true;
            }

            VideoPlayer player = state.Player;
            float leadSeconds = sequenceConfig != null ? sequenceConfig.TransitionTriggerBeforeVideoEnd : 0f;
            if (leadSeconds > 0f && TryGetRemainingSeconds(player, out double remainingSeconds))
            {
                return remainingSeconds <= leadSeconds;
            }

            return !player.isPlaying && player.time > 0d;
        }

        private static bool TryGetRemainingSeconds(VideoPlayer player, out double remainingSeconds)
        {
            remainingSeconds = double.PositiveInfinity;

            if (player == null)
            {
                return false;
            }

            double length = player.length;
            if (length > 0d && !double.IsNaN(length) && !double.IsInfinity(length))
            {
                remainingSeconds = Math.Max(0d, length - player.time);
                return true;
            }

            if (player.frameCount > 0UL && player.frameRate > 0d && player.frame >= 0L)
            {
                remainingSeconds = Math.Max(0d, ((double)player.frameCount - player.frame) / player.frameRate);
                return true;
            }

            return false;
        }

        private void UpdateTimerVideoRequest()
        {
            if (sequenceConfig == null || sequenceConfig.TimerVideoDelay <= 0f || sequenceConfig.TimerVideoCount <= 0)
            {
                ClearTimerVideoSchedule();
                return;
            }

            if (!initialized || currentState != FlowState.Initial || inputLocked || isSwitching)
            {
                return;
            }

            if (!Mathf.Approximately(lastTimerVideoDelay, sequenceConfig.TimerVideoDelay) || nextTimerVideoAt < 0f)
            {
                ScheduleNextTimerVideo();
                return;
            }

            if (Time.unscaledTime < nextTimerVideoAt)
            {
                return;
            }

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] Idle timer reached after {sequenceConfig.TimerVideoDelay:0.##} seconds.", this);
            }

            if (!RequestNextVideo(CabinPortraitSwitchRequestSource.Auto))
            {
                ScheduleNextTimerVideo();
            }
        }

        private void ScheduleNextTimerVideo()
        {
            if (sequenceConfig == null ||
                sequenceConfig.TimerVideoDelay <= 0f ||
                sequenceConfig.TimerVideoCount <= 0 ||
                currentState != FlowState.Initial)
            {
                ClearTimerVideoSchedule();
                return;
            }

            lastTimerVideoDelay = sequenceConfig.TimerVideoDelay;
            nextTimerVideoAt = Time.unscaledTime + sequenceConfig.TimerVideoDelay;
        }

        private void ClearTimerVideoSchedule()
        {
            nextTimerVideoAt = -1f;
            lastTimerVideoDelay = -1f;
        }

        private bool CanAcceptSwitchRequest(CabinPortraitSwitchRequestSource source, out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (!initialized)
            {
                rejectionReason = "System is not initialized.";
                return false;
            }

            if (sequenceConfig == null)
            {
                rejectionReason = "Sequence config is missing.";
                return false;
            }

            CabinPortraitVideoSequenceKind sequenceKind = GetSequenceKind(source);
            if (sequenceConfig.GetVideoCount(sequenceKind) <= 0)
            {
                rejectionReason = $"{DescribeSwitchRequestSource(source)} sequence has no video paths.";
                return false;
            }

            if (inputLocked || isSwitching)
            {
                rejectionReason = "Input is locked or playback is already running.";
                return false;
            }

            if (currentState != FlowState.Initial)
            {
                rejectionReason = $"Flow state is {currentState}. Expected {FlowState.Initial}.";
                return false;
            }

            if (GetFirstAvailableSlot(source) == null)
            {
                rejectionReason = "No VideoPlayer is assigned.";
                return false;
            }

            return true;
        }

        private IEnumerator PrepareSlotForFirstFrame(PlayerSlotState state, CabinPortraitSwitchRequestSource source, int videoIndex)
        {
            if (!TryBeginPrepareSlot(state, source, videoIndex, out int token))
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
                        $"[CabinPortraits.Video] Prepare is still waiting after {prepareWarningTimeout:0.##} seconds for {DescribeSwitchRequestSource(source)} index {state.VideoIndex}. Continuing without entering ErrorRecovery.\n{state.FullPath}",
                        this);
                }

                yield return null;
            }

            if (state.Token != token || !state.IsPreparing || currentState == FlowState.ErrorRecovery)
            {
                yield break;
            }

            if (player != null)
            {
                ApplyPrimingAudioMute(state);
                player.sendFrameReadyEvents = true;

                if (!player.isPlaying)
                {
                    player.Play();
                }
            }

            float firstFrameWarningTimeout = sequenceConfig != null ? sequenceConfig.FirstFrameWarningTimeout : 5f;
            float firstFrameStartedAt = Time.unscaledTime;
            bool firstFrameWarningLogged = false;

            while (state.Token == token && state.IsPreparing && !state.FirstFrameReceived)
            {
                if (!firstFrameWarningLogged && Time.unscaledTime - firstFrameStartedAt >= firstFrameWarningTimeout)
                {
                    firstFrameWarningLogged = true;
                    Debug.LogWarning(
                        $"[CabinPortraits.Video] First frame is still waiting after {firstFrameWarningTimeout:0.##} seconds for {DescribeSwitchRequestSource(source)} index {state.VideoIndex}. Continuing without entering ErrorRecovery.\n{state.FullPath}",
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
            state.PlaybackCompleted = false;
            onVideoPrepared.Invoke(state.VideoIndex);

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] First frame ready for {DescribeSwitchRequestSource(source)} index {state.VideoIndex} on Player {state.Slot}. " +
                    $"FrameReady={state.FirstFrameIndex}, PlayerFrame={player.frame}, Time={player.time:0.###}, TargetTexture={DescribeTexture(player.targetTexture)}, PlayerTexture={DescribeTexture(player.texture)}.",
                    this);
            }
        }

        private bool TryBeginPrepareSlot(PlayerSlotState state, CabinPortraitSwitchRequestSource source, int videoIndex, out int token)
        {
            token = 0;

            if (state == null || state.Player == null || sequenceConfig == null)
            {
                ReportFailure("Cannot prepare video because a required reference is missing.");
                return false;
            }

            CabinPortraitVideoSequenceKind sequenceKind = GetSequenceKind(source);
            if (!sequenceConfig.TryGetVideoPath(sequenceKind, videoIndex, out string relativePath))
            {
                ReportFailure($"Missing {DescribeSwitchRequestSource(source)} video path at index {videoIndex}.");
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
            state.VideoIndex = sequenceConfig.WrapIndex(sequenceKind, videoIndex);
            state.IsPreparing = true;
            state.IsReady = false;
            state.IsPlaying = false;
            state.PlaybackCompleted = false;
            state.FirstFrameReceived = false;
            state.FirstFrameIndex = -1;
            state.RelativePath = relativePath;
            state.FullPath = fullPath;
            state.Url = fileUri;

            ConfigurePlayer(state.Player);
            RestorePrimingAudio(state);
            state.Player.url = fileUri;
            state.Player.Prepare();

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Preparing {DescribeSwitchRequestSource(source)} index {state.VideoIndex} on Player {state.Slot}. " +
                    $"RenderMode={state.Player.renderMode}, TargetTexture={DescribeTexture(state.Player.targetTexture)}, " +
                    $"URL={fileUri}\nFullPath={fullPath}",
                    this);
            }

            return true;
        }

        private bool PlayVisibleSlot(PlayerSlotState state, CabinPortraitSwitchRequestSource source)
        {
            if (state == null || state.Player == null || !state.IsReady || !state.Player.isPrepared)
            {
                ReportFailure("Cannot play because the target video is not prepared.");
                return false;
            }

            RestorePrimingAudio(state);
            state.Player.isLooping = false;
            state.Player.sendFrameReadyEvents = false;
            state.PlaybackCompleted = false;

            if (!state.Player.isPlaying)
            {
                state.Player.Play();
            }

            state.IsPlaying = true;

            if (displayController != null)
            {
                displayController.ShowSlot(GetSequenceKind(source), state.Slot, GetTargetTexture(state));
            }

            if (ShouldLog)
            {
                Debug.Log(
                    $"[CabinPortraits.Video] Playing visible {DescribeSwitchRequestSource(source)} index {state.VideoIndex} once on Player {state.Slot}. " +
                    $"RenderMode={state.Player.renderMode}, TargetTexture={DescribeTexture(state.Player.targetTexture)}, " +
                    $"PlayerTexture={DescribeTexture(state.Player.texture)}, DisplayTexture={DescribeTexture(GetTargetTexture(state))}.",
                    this);
            }

            InvokeVideoStarted(source, state.VideoIndex);
            return true;
        }

        private void ConfigurePlayer(VideoPlayer player)
        {
            player.source = VideoSource.Url;
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = false;
            player.skipOnDrop = false;
            player.sendFrameReadyEvents = true;
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
                RestorePrimingAudio(state);
                state.Player.sendFrameReadyEvents = false;
                state.Player.Stop();
            }

            state.IsPreparing = false;
            state.IsReady = false;
            state.IsPlaying = false;
            state.PlaybackCompleted = false;
            state.FirstFrameReceived = false;
            state.FirstFrameIndex = -1;
            state.VideoIndex = -1;
            state.RelativePath = string.Empty;
            state.FullPath = string.Empty;
            state.Url = string.Empty;
        }

        private void StopAllSlots()
        {
            StopSlot(slotAState);
            StopSlot(slotBState);
            StopSlot(timerSlotAState);
            StopSlot(timerSlotBState);
        }

        private void ApplyPrimingAudioMute(PlayerSlotState state)
        {
            if (state == null || state.Player == null || state.PrimingAudioMuted)
            {
                return;
            }

            VideoPlayer player = state.Player;
            state.PrimingAudioOutputMode = player.audioOutputMode;

            try
            {
                if (player.audioOutputMode == VideoAudioOutputMode.Direct)
                {
                    state.PrimingDirectAudioWasMuted = player.GetDirectAudioMute(0);
                    player.SetDirectAudioMute(0, true);
                    state.PrimingAudioMuted = true;
                    return;
                }

                if (player.audioOutputMode == VideoAudioOutputMode.AudioSource && player.controlledAudioTrackCount > 0)
                {
                    AudioSource audioSource = player.GetTargetAudioSource(0);
                    if (audioSource == null)
                    {
                        return;
                    }

                    state.PrimingAudioSource = audioSource;
                    state.PrimingAudioSourceWasMuted = audioSource.mute;
                    audioSource.mute = true;
                    state.PrimingAudioMuted = true;
                }
            }
            catch (Exception exception)
            {
                if (ShouldLog)
                {
                    Debug.LogWarning($"[CabinPortraits.Video] Could not mute priming audio for Player {state.Slot}, index {state.VideoIndex}: {exception.Message}", this);
                }
            }
        }

        private void RestorePrimingAudio(PlayerSlotState state)
        {
            if (state == null || !state.PrimingAudioMuted)
            {
                return;
            }

            VideoPlayer player = state.Player;

            try
            {
                if (player != null && state.PrimingAudioOutputMode == VideoAudioOutputMode.Direct)
                {
                    player.SetDirectAudioMute(0, state.PrimingDirectAudioWasMuted);
                }

                if (state.PrimingAudioSource != null)
                {
                    state.PrimingAudioSource.mute = state.PrimingAudioSourceWasMuted;
                }
            }
            catch (Exception exception)
            {
                if (ShouldLog)
                {
                    Debug.LogWarning($"[CabinPortraits.Video] Could not restore priming audio for Player {state.Slot}, index {state.VideoIndex}: {exception.Message}", this);
                }
            }
            finally
            {
                state.PrimingAudioMuted = false;
                state.PrimingDirectAudioWasMuted = false;
                state.PrimingAudioSource = null;
                state.PrimingAudioSourceWasMuted = false;
            }
        }

        private void RestorePrimingAudioForSlots()
        {
            RestorePrimingAudio(slotAState);
            RestorePrimingAudio(slotBState);
            RestorePrimingAudio(timerSlotAState);
            RestorePrimingAudio(timerSlotBState);
        }

        private void ReturnToInitialAfterFailure()
        {
            StopAllSlots();
            ShowInitialDisplay();
            currentIndex = -1;
            isSwitching = false;
            switchCoroutine = null;

            if (currentState == FlowState.ErrorRecovery && TransitionTo(FlowState.InitialPreparing))
            {
                TransitionTo(FlowState.Initial);
            }
            else if (currentState != FlowState.Initial)
            {
                EnterStateDirectly(FlowState.Initial);
            }

            ScheduleNextTimerVideo();
            SetInputLocked(false);
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

        private void ShowInitialDisplay()
        {
            if (displayController != null)
            {
                displayController.ShowInitial();
            }
        }

        private void SetInputLocked(bool locked)
        {
            if (inputLocked == locked)
            {
                return;
            }

            inputLocked = locked;
            if (locked)
            {
                onInputLocked.Invoke();
            }
            else
            {
                onInputUnlocked.Invoke();
            }
        }

        private void InitializeSlotReferences()
        {
            slotAState = UpdateSlotState(slotAState, videoPlayerA, CabinPortraitVideoSlot.A);
            slotBState = UpdateSlotState(slotBState, videoPlayerB, CabinPortraitVideoSlot.B);
            timerSlotAState = UpdateSlotState(timerSlotAState, timerVideoPlayerA, CabinPortraitVideoSlot.A);
            timerSlotBState = UpdateSlotState(timerSlotBState, timerVideoPlayerB, CabinPortraitVideoSlot.B);
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

        private PlayerSlotState GetFirstAvailableSlot(CabinPortraitSwitchRequestSource source)
        {
            if (source == CabinPortraitSwitchRequestSource.Auto)
            {
                PlayerSlotState timerSlot = GetFirstAvailableTimerSlot();
                if (timerSlot != null)
                {
                    return timerSlot;
                }
            }

            return GetFirstAvailableManualSlot();
        }

        private PlayerSlotState GetFirstAvailableManualSlot()
        {
            if (activeSlot != null && activeSlot.Player != null &&
                (activeSlot == slotAState || activeSlot == slotBState))
            {
                return activeSlot;
            }

            if (slotAState != null && slotAState.Player != null)
            {
                return slotAState;
            }

            if (slotBState != null && slotBState.Player != null)
            {
                return slotBState;
            }

            return null;
        }

        private PlayerSlotState GetFirstAvailableTimerSlot()
        {
            if (activeSlot != null && activeSlot.Player != null &&
                (activeSlot == timerSlotAState || activeSlot == timerSlotBState))
            {
                return activeSlot;
            }

            if (timerSlotAState != null && timerSlotAState.Player != null)
            {
                return timerSlotAState;
            }

            if (timerSlotBState != null && timerSlotBState.Player != null)
            {
                return timerSlotBState;
            }

            return null;
        }

        private PlayerSlotState GetOtherSlot(CabinPortraitSwitchRequestSource source, PlayerSlotState slot)
        {
            if (source == CabinPortraitSwitchRequestSource.Auto && (timerSlotAState != null || timerSlotBState != null))
            {
                if (slot == timerSlotAState)
                {
                    return timerSlotBState;
                }

                if (slot == timerSlotBState)
                {
                    return timerSlotAState;
                }

                return timerSlotAState != null && timerSlotAState.Player != null ? timerSlotAState : timerSlotBState;
            }

            if (slot == slotAState)
            {
                return slotBState;
            }

            if (slot == slotBState)
            {
                return slotAState;
            }

            return slotAState != null && slotAState.Player != null ? slotAState : slotBState;
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

            if (timerSlotAState != null && player == timerSlotAState.Player)
            {
                return timerSlotAState;
            }

            if (timerSlotBState != null && player == timerSlotBState.Player)
            {
                return timerSlotBState;
            }

            return null;
        }

        private int GetNextIndex(CabinPortraitSwitchRequestSource source)
        {
            return source == CabinPortraitSwitchRequestSource.Auto ? nextTimerIndex : nextManualIndex;
        }

        private int GetLastIndex(CabinPortraitSwitchRequestSource source)
        {
            return source == CabinPortraitSwitchRequestSource.Auto ? lastTimerIndex : lastManualIndex;
        }

        private void SetLastIndex(CabinPortraitSwitchRequestSource source, int index)
        {
            if (source == CabinPortraitSwitchRequestSource.Auto)
            {
                lastTimerIndex = index;
                return;
            }

            lastManualIndex = index;
        }

        private void AdvanceNextIndex(CabinPortraitSwitchRequestSource source, int currentPlaybackIndex)
        {
            CabinPortraitVideoSequenceKind sequenceKind = GetSequenceKind(source);
            int nextIndex = sequenceConfig != null ? sequenceConfig.GetNextIndex(sequenceKind, currentPlaybackIndex) : 0;

            if (source == CabinPortraitSwitchRequestSource.Auto)
            {
                nextTimerIndex = nextIndex;
                return;
            }

            nextManualIndex = nextIndex;
        }

        private static CabinPortraitVideoSequenceKind GetSequenceKind(CabinPortraitSwitchRequestSource source)
        {
            return source == CabinPortraitSwitchRequestSource.Auto
                ? CabinPortraitVideoSequenceKind.Timer
                : CabinPortraitVideoSequenceKind.ManualInput;
        }

        private static FlowState GetPreparingState(CabinPortraitSwitchRequestSource source)
        {
            return source == CabinPortraitSwitchRequestSource.Auto
                ? FlowState.TimerVideoPreparing
                : FlowState.ManualVideoPreparing;
        }

        private static FlowState GetPlayingState(CabinPortraitSwitchRequestSource source)
        {
            return source == CabinPortraitSwitchRequestSource.Auto
                ? FlowState.TimerVideoPlaying
                : FlowState.ManualVideoPlaying;
        }

        private void InvokeSwitchRequested(CabinPortraitSwitchRequestSource source, int previousIndex, int nextIndex)
        {
            switch (source)
            {
                case CabinPortraitSwitchRequestSource.Auto:
                    onTimerVideoRequested.Invoke(previousIndex, nextIndex);
                    break;
                case CabinPortraitSwitchRequestSource.ManualInput:
                default:
                    onSwitchRequested.Invoke(previousIndex, nextIndex);
                    break;
            }
        }

        private void InvokeTransitionStarted(CabinPortraitSwitchRequestSource source)
        {
            switch (source)
            {
                case CabinPortraitSwitchRequestSource.Auto:
                    onTimerTransitionStarted.Invoke();
                    break;
                case CabinPortraitSwitchRequestSource.ManualInput:
                default:
                    onManualTransitionStarted.Invoke();
                    break;
            }
        }

        private void InvokeVideoStarted(CabinPortraitSwitchRequestSource source, int videoIndex)
        {
            onVideoStarted.Invoke(videoIndex);

            switch (source)
            {
                case CabinPortraitSwitchRequestSource.Auto:
                    onTimerVideoStarted.Invoke(videoIndex);
                    break;
                case CabinPortraitSwitchRequestSource.ManualInput:
                default:
                    onManualVideoStarted.Invoke(videoIndex);
                    break;
            }
        }

        private void InvokeReadyToReveal(CabinPortraitSwitchRequestSource source, int completedIndex)
        {
            switch (source)
            {
                case CabinPortraitSwitchRequestSource.Auto:
                    onTimerReadyToReveal.Invoke(completedIndex);
                    onTimerInitialReadyToReveal.Invoke();
                    break;
                case CabinPortraitSwitchRequestSource.ManualInput:
                default:
                    onManualReadyToReveal.Invoke(completedIndex);
                    onManualInitialReadyToReveal.Invoke();
                    break;
            }
        }

        private static string DescribeSwitchRequestSource(CabinPortraitSwitchRequestSource source)
        {
            switch (source)
            {
                case CabinPortraitSwitchRequestSource.Auto:
                    return "timer";
                case CabinPortraitSwitchRequestSource.ManualInput:
                default:
                    return "manual";
            }
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
            player.frameReady -= HandleFrameReady;
            player.loopPointReached -= HandleLoopPointReached;

            player.errorReceived += HandleErrorReceived;
            player.frameReady += HandleFrameReady;
            player.loopPointReached += HandleLoopPointReached;
        }

        private void Unsubscribe(VideoPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.errorReceived -= HandleErrorReceived;
            player.frameReady -= HandleFrameReady;
            player.loopPointReached -= HandleLoopPointReached;
        }

        private void HandleErrorReceived(VideoPlayer player, string message)
        {
            PlayerSlotState state = GetState(player);
            string context = state != null
                ? $"Player {state.Slot}, index {state.VideoIndex}, path {state.RelativePath}"
                : "Unknown player";

            ReportFailure($"VideoPlayer error: {message}\n{context}");
        }

        private void HandleFrameReady(VideoPlayer player, long frameIndex)
        {
            PlayerSlotState state = GetState(player);
            if (state == null || !state.IsPreparing || state.FirstFrameReceived)
            {
                return;
            }

            state.FirstFrameReceived = true;
            state.FirstFrameIndex = frameIndex;

            if (player != null && player.isPlaying)
            {
                player.Pause();
            }
        }

        private void HandleLoopPointReached(VideoPlayer player)
        {
            PlayerSlotState state = GetState(player);
            if (state == null)
            {
                return;
            }

            state.PlaybackCompleted = true;
            state.IsPlaying = false;
        }

        private bool HasRequiredReferences()
        {
            if (sequenceConfig != null &&
                displayController != null &&
                GetFirstAvailableSlot(CabinPortraitSwitchRequestSource.ManualInput) != null)
            {
                return true;
            }

            ReportFailure("Missing sequenceConfig, at least one VideoPlayer, or displayController.");
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
            InvokeStateEntered(nextState);
            return true;
        }

        private void EnterStateDirectly(FlowState nextState)
        {
            currentState = nextState;
            InvokeStateEntered(nextState);
        }

        private void InvokeStateEntered(FlowState state)
        {
            if (stateEvents == null)
            {
                return;
            }

            stateEvents.Invoke(state, activeRequestSource, this);
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
                    return to == FlowState.InitialPreparing;
                case FlowState.InitialPreparing:
                    return to == FlowState.Initial;
                case FlowState.Initial:
                    return to == FlowState.ManualVideoPreparing || to == FlowState.TimerVideoPreparing;
                case FlowState.ManualVideoPreparing:
                    return to == FlowState.ManualVideoPlaying;
                case FlowState.ManualVideoPlaying:
                    return to == FlowState.ReturningToInitial;
                case FlowState.TimerVideoPreparing:
                    return to == FlowState.TimerVideoPlaying;
                case FlowState.TimerVideoPlaying:
                    return to == FlowState.ReturningToInitial;
                case FlowState.ReturningToInitial:
                    return to == FlowState.CoveredRestoringInitial;
                case FlowState.CoveredRestoringInitial:
                    return to == FlowState.Initial;
                case FlowState.ErrorRecovery:
                    return to == FlowState.InitialPreparing;
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
