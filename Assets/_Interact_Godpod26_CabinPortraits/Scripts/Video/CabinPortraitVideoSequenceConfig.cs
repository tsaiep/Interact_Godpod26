using System.Collections.Generic;
using UnityEngine;

namespace CabinPortraits.Video
{
    [CreateAssetMenu(
        fileName = "CabinPortraitVideoSequenceConfig",
        menuName = "Godpod/Cabin Portraits/Video Sequence Config"
    )]
    public sealed class CabinPortraitVideoSequenceConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Video file paths relative to StreamingAssets. The controller loops through this list in order.")]
        private List<string> videoRelativePaths = new List<string>
        {
            "CabinPortraits/Videos/Video_01.mp4",
            "CabinPortraits/Videos/Video_02.mp4",
            "CabinPortraits/Videos/Video_03.mp4",
            "CabinPortraits/Videos/Video_04.mp4",
            "CabinPortraits/Videos/Video_05.mp4",
            "CabinPortraits/Videos/Video_06.mp4"
        };

        [SerializeField, Min(0), Tooltip("Initial video index shown when the scene starts.")]
        private int startIndex;

        [SerializeField, Tooltip("When enabled, the controller prepares and plays the start index on Start.")]
        private bool playOnStart = true;

        [SerializeField, Min(0.1f), Tooltip("Seconds before logging that VideoPlayer Prepare is taking longer than expected. This is diagnostic only and does not enter ErrorRecovery.")]
        private float prepareWarningTimeout = 10f;

        [SerializeField, Min(0.1f), Tooltip("Seconds before logging that the first frame is taking longer than expected. This is diagnostic only and does not enter ErrorRecovery.")]
        private float firstFrameWarningTimeout = 10f;

        [SerializeField, Min(0f), Tooltip("Seconds to wait after On Transition Started before stopping the current video and preparing the next one under the fully covered transition.")]
        private float transitionCoverDelay = 1f;

        [SerializeField, Min(0f), Tooltip("Minimum seconds between accepted switch requests. This only keeps input locked; it does not delay video prepare or playback.")]
        private float switchInputCooldown = 5f;

        [SerializeField, Min(0f), Tooltip("Seconds between automatic switch requests while the video is playing. Set 0 to disable automatic switching.")]
        private float autoSwitchInterval = 60f;

        [SerializeField, Tooltip("When enabled, the video cycle outputs detailed Console logs.")]
        private bool verboseLogs = true;

        public IReadOnlyList<string> VideoRelativePaths => videoRelativePaths;
        public int StartIndex => Mathf.Clamp(startIndex, 0, Mathf.Max(0, VideoCount - 1));
        public bool PlayOnStart => playOnStart;
        public float PrepareWarningTimeout => prepareWarningTimeout;
        public float FirstFrameWarningTimeout => firstFrameWarningTimeout;
        public float TransitionCoverDelay => transitionCoverDelay;
        public float SwitchInputCooldown => switchInputCooldown;
        public float AutoSwitchInterval => autoSwitchInterval;
        public bool VerboseLogs => verboseLogs;
        public int VideoCount => videoRelativePaths != null ? videoRelativePaths.Count : 0;

        public bool TryGetVideoPath(int index, out string relativePath)
        {
            relativePath = string.Empty;

            if (videoRelativePaths == null || videoRelativePaths.Count == 0)
            {
                return false;
            }

            int wrappedIndex = WrapIndex(index);
            relativePath = videoRelativePaths[wrappedIndex];
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        public int GetNextIndex(int index)
        {
            return WrapIndex(index + 1);
        }

        public int WrapIndex(int index)
        {
            if (videoRelativePaths == null || videoRelativePaths.Count == 0)
            {
                return 0;
            }

            int count = videoRelativePaths.Count;
            return (index % count + count) % count;
        }

        private void OnValidate()
        {
            if (videoRelativePaths == null)
            {
                videoRelativePaths = new List<string>();
            }

            startIndex = Mathf.Max(0, startIndex);
            prepareWarningTimeout = Mathf.Max(0.1f, prepareWarningTimeout);
            firstFrameWarningTimeout = Mathf.Max(0.1f, firstFrameWarningTimeout);
            transitionCoverDelay = Mathf.Max(0f, transitionCoverDelay);
            switchInputCooldown = Mathf.Max(0f, switchInputCooldown);
            autoSwitchInterval = Mathf.Max(0f, autoSwitchInterval);
        }
    }
}
