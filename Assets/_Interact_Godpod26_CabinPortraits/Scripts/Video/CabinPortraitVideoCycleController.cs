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

        [Header("Unity Events")]
        [SerializeField, Tooltip("Invoked when a Space request is accepted. Args: current index, next index.")]
        private CabinPortraitVideoSwitchEvent onSwitchRequested = new CabinPortraitVideoSwitchEvent();

        [SerializeField, Tooltip("Invoked immediately before waiting transitionSwitchDelay.")]
        private UnityEvent onTransitionStarted = new UnityEvent();

        [SerializeField, Tooltip("Invoked after the active visible video index changes.")]
        private CabinPortraitVideoIndexEvent onVideoIndexChanged = new CabinPortraitVideoIndexEvent();

        [SerializeField, Tooltip("Invoked when a video first-frame prepare finishes.")]
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
            public bool PlayWhenReady;
            public bool FirstFrameReceived;
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
        private int tokenCounter;
        private int currentIndex = -1;
        private bool initialized;
        private bool inputLocked;
        private bool isSwitching;

        public int CurrentIndex => currentIndex;
        public bool IsInitialized => initialized;
        public bool IsSwitching => isSwitching;
        public bool CanSwitch => CanAcceptSwitchRequest(out _);

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
            StopSlotCoroutines(slotAState);
            StopSlotCoroutines(slotBState);
        }

        public void InitializeAndPlay()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            StopCoroutineIfRunning(ref switchCoroutine);
            StopSlot(slotAState);
            StopSlot(slotBState);

            initialized = true;
            inputLocked = false;
            isSwitching = false;
            currentIndex = sequenceConfig.StartIndex;
            activeSlot = slotAState;
            standbySlot = slotBState;

            int nextIndex = sequenceConfig.GetNextIndex(currentIndex);
            PrepareSlot(activeSlot, currentIndex, true);
            PrepareSlot(standbySlot, nextIndex, false);
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
            StopCoroutineIfRunning(ref switchCoroutine);
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
            onTransitionStarted.Invoke();

            float transitionDelay = sequenceConfig != null ? sequenceConfig.TransitionSwitchDelay : 0.5f;
            if (transitionDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(transitionDelay);
            }

            if (!PlaySlot(nextActiveSlot))
            {
                isSwitching = false;
                inputLocked = false;
                onInputCooldownEnded.Invoke();
                switchCoroutine = null;
                yield break;
            }

            StopSlot(previousActiveSlot);
            activeSlot = nextActiveSlot;
            standbySlot = previousActiveSlot;
            currentIndex = nextIndex;
            onVideoIndexChanged.Invoke(currentIndex);

            int prepareIndex = sequenceConfig.GetNextIndex(currentIndex);
            PrepareSlot(standbySlot, prepareIndex, false);

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

            if (activeSlot == null || !activeSlot.IsPlaying)
            {
                rejectionReason = "No active video is playing yet.";
                return false;
            }

            if (standbySlot == null || !standbySlot.IsReady)
            {
                rejectionReason = "Next video is not prepared yet.";
                return false;
            }

            return true;
        }

        private void PrepareSlot(PlayerSlotState state, int videoIndex, bool playWhenReady)
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

            StopSlotCoroutines(state);
            state.Token = ++tokenCounter;
            state.VideoIndex = sequenceConfig.WrapIndex(videoIndex);
            state.IsPreparing = true;
            state.IsReady = false;
            state.IsPlaying = false;
            state.PlayWhenReady = playWhenReady;
            state.FirstFrameReceived = false;
            state.RelativePath = relativePath;
            state.FullPath = fullPath;
            state.Url = fileUri;

            ConfigurePlayer(state.Player);
            state.Player.Stop();
            state.Player.url = fileUri;
            state.Player.Prepare();

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] Preparing index {state.VideoIndex} on Player {state.Slot}.\n{fullPath}", this);
            }

            state.PrepareTimeoutCoroutine = StartCoroutine(PrepareTimeoutRoutine(state, state.Token));
        }

        private bool PlaySlot(PlayerSlotState state)
        {
            if (state == null || state.Player == null || !state.IsReady || !state.Player.isPrepared)
            {
                ReportFailure("Cannot play because the target video is not prepared.");
                return false;
            }

            state.Player.isLooping = true;
            state.Player.Play();
            state.IsPlaying = true;

            if (displayController != null)
            {
                displayController.ShowSlot(state.Slot, GetTargetTexture(state));
            }

            if (ShouldLog)
            {
                Debug.Log($"[CabinPortraits.Video] Playing index {state.VideoIndex} on Player {state.Slot}.", this);
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
                yield break;
            }

            if (!state.FirstFrameReceived)
            {
                ReportFailure($"First frame timeout after {timeout:0.##} seconds for index {state.VideoIndex}.\n{state.FullPath}");
                yield break;
            }

            if (player != null && player.isPlaying)
            {
                player.Pause();
            }

            if (player != null)
            {
                player.sendFrameReadyEvents = false;
            }

            state.IsPreparing = false;
            state.IsReady = true;
            onVideoPrepared.Invoke(state.VideoIndex);

            if (state.PlayWhenReady)
            {
                PlaySlot(state);
                onVideoIndexChanged.Invoke(state.VideoIndex);
            }
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
        }

        private void HandleLoopPointReached(VideoPlayer player)
        {
            PlayerSlotState state = GetState(player);
            if (state == null || !state.IsPlaying || player == null || player.isLooping)
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

        private void ReportFailure(string message)
        {
            Debug.LogWarning($"[CabinPortraits.Video] {message}", this);
            onVideoError.Invoke(message);
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

        private void StopSlot(PlayerSlotState state)
        {
            if (state == null)
            {
                return;
            }

            StopSlotCoroutines(state);

            if (state.Player != null)
            {
                state.Player.sendFrameReadyEvents = false;
                state.Player.Stop();
            }

            state.IsPreparing = false;
            state.IsReady = false;
            state.IsPlaying = false;
            state.PlayWhenReady = false;
            state.FirstFrameReceived = false;
            state.VideoIndex = -1;
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

        private bool ShouldLog => sequenceConfig == null || sequenceConfig.VerboseLogs;
    }
}
