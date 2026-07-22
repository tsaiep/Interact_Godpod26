using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CabinPortraits.Video
{
    public enum CabinPortraitVideoSequenceKind
    {
        ManualInput,
        Timer
    }

    [CreateAssetMenu(
        fileName = "CabinPortraitVideoSequenceConfig",
        menuName = "Godpod/Cabin Portraits/Video Sequence Config"
    )]
    public sealed class CabinPortraitVideoSequenceConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Manual button video file paths relative to StreamingAssets. The controller plays one item per accepted button request.")]
        private List<string> videoRelativePaths = new List<string>
        {
            "CabinPortraits/Videos/Video_01.mp4",
            "CabinPortraits/Videos/Video_02.mp4",
            "CabinPortraits/Videos/Video_03.mp4",
            "CabinPortraits/Videos/Video_04.mp4",
            "CabinPortraits/Videos/Video_05.mp4",
            "CabinPortraits/Videos/Video_06.mp4"
        };

        [SerializeField, Tooltip("Timer-triggered video file paths relative to StreamingAssets. This list is independent from Video Relative Paths.")]
        private List<string> timerVideoRelativePaths = new List<string>();

        [SerializeField, Min(0), Tooltip("First manual video index used after the scene enters the initial static screen.")]
        private int startIndex;

        [SerializeField, Min(0), Tooltip("First timer video index used when the idle timer fires.")]
        private int timerStartIndex;

        [SerializeField, Tooltip("When enabled, the controller enters the initial static screen on Start.")]
        private bool playOnStart = true;

        [SerializeField, Min(0.1f), Tooltip("Seconds before logging that VideoPlayer Prepare is taking longer than expected. This is diagnostic only and does not enter ErrorRecovery.")]
        private float prepareWarningTimeout = 10f;

        [SerializeField, Min(0.1f), Tooltip("Seconds before logging that the first frame is taking longer than expected. This is diagnostic only and does not enter ErrorRecovery.")]
        private float firstFrameWarningTimeout = 10f;

        [SerializeField, Min(0f), Tooltip("Seconds to wait after the source-specific transition started event before stopping the current video and restoring the initial screen under the fully covered transition.")]
        private float transitionCoverDelay = 1f;

        [SerializeField, Min(0f), Tooltip("Seconds before the playing video ends to invoke On Manual Transition Started or On Timer Transition Started. Set 0 to transition at the end.")]
        private float transitionTriggerBeforeVideoEnd = 1f;

        [SerializeField, Min(0f), Tooltip("Seconds after entering the initial static screen before playing the next timer video. Set 0 to disable timer playback.")]
        [FormerlySerializedAs("autoSwitchInterval")]
        private float timerVideoDelay = 60f;

        [SerializeField, Tooltip("When enabled, the video cycle outputs detailed Console logs.")]
        private bool verboseLogs = true;

        public IReadOnlyList<string> VideoRelativePaths => videoRelativePaths;
        public IReadOnlyList<string> TimerVideoRelativePaths => timerVideoRelativePaths;
        public int StartIndex => Mathf.Clamp(startIndex, 0, Mathf.Max(0, VideoCount - 1));
        public int TimerStartIndex => Mathf.Clamp(timerStartIndex, 0, Mathf.Max(0, TimerVideoCount - 1));
        public bool PlayOnStart => playOnStart;
        public float PrepareWarningTimeout => prepareWarningTimeout;
        public float FirstFrameWarningTimeout => firstFrameWarningTimeout;
        public float TransitionCoverDelay => transitionCoverDelay;
        public float TransitionTriggerBeforeVideoEnd => transitionTriggerBeforeVideoEnd;
        public float TimerVideoDelay => timerVideoDelay;
        public bool VerboseLogs => verboseLogs;
        public int VideoCount => videoRelativePaths != null ? videoRelativePaths.Count : 0;
        public int TimerVideoCount => timerVideoRelativePaths != null ? timerVideoRelativePaths.Count : 0;

        public bool TryGetVideoPath(int index, out string relativePath)
        {
            return TryGetVideoPath(CabinPortraitVideoSequenceKind.ManualInput, index, out relativePath);
        }

        public bool TryGetVideoPath(CabinPortraitVideoSequenceKind sequenceKind, int index, out string relativePath)
        {
            relativePath = string.Empty;
            List<string> paths = GetPaths(sequenceKind);

            if (paths == null || paths.Count == 0)
            {
                return false;
            }

            int wrappedIndex = WrapIndex(sequenceKind, index);
            relativePath = paths[wrappedIndex];
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        public int GetNextIndex(int index)
        {
            return GetNextIndex(CabinPortraitVideoSequenceKind.ManualInput, index);
        }

        public int GetNextIndex(CabinPortraitVideoSequenceKind sequenceKind, int index)
        {
            return WrapIndex(sequenceKind, index + 1);
        }

        public int WrapIndex(int index)
        {
            return WrapIndex(CabinPortraitVideoSequenceKind.ManualInput, index);
        }

        public int WrapIndex(CabinPortraitVideoSequenceKind sequenceKind, int index)
        {
            List<string> paths = GetPaths(sequenceKind);
            if (paths == null || paths.Count == 0)
            {
                return 0;
            }

            int count = paths.Count;
            return (index % count + count) % count;
        }

        public int GetStartIndex(CabinPortraitVideoSequenceKind sequenceKind)
        {
            return sequenceKind == CabinPortraitVideoSequenceKind.Timer ? TimerStartIndex : StartIndex;
        }

        public int GetVideoCount(CabinPortraitVideoSequenceKind sequenceKind)
        {
            return sequenceKind == CabinPortraitVideoSequenceKind.Timer ? TimerVideoCount : VideoCount;
        }

        private List<string> GetPaths(CabinPortraitVideoSequenceKind sequenceKind)
        {
            return sequenceKind == CabinPortraitVideoSequenceKind.Timer ? timerVideoRelativePaths : videoRelativePaths;
        }

        private void OnValidate()
        {
            if (videoRelativePaths == null)
            {
                videoRelativePaths = new List<string>();
            }

            if (timerVideoRelativePaths == null)
            {
                timerVideoRelativePaths = new List<string>();
            }

            startIndex = Mathf.Max(0, startIndex);
            timerStartIndex = Mathf.Max(0, timerStartIndex);
            prepareWarningTimeout = Mathf.Max(0.1f, prepareWarningTimeout);
            firstFrameWarningTimeout = Mathf.Max(0.1f, firstFrameWarningTimeout);
            transitionCoverDelay = Mathf.Max(0f, transitionCoverDelay);
            transitionTriggerBeforeVideoEnd = Mathf.Max(0f, transitionTriggerBeforeVideoEnd);
            timerVideoDelay = Mathf.Max(0f, timerVideoDelay);
        }
    }
}
