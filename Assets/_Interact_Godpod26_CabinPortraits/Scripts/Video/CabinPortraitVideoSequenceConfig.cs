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
            "CabinPortraits/Videos/Video_00.mp4",
            "CabinPortraits/Videos/Video_01.mp4",
            "CabinPortraits/Videos/Video_02.mp4",
            "CabinPortraits/Videos/Video_03.mp4",
            "CabinPortraits/Videos/Video_04.mp4",
            "CabinPortraits/Videos/Video_05.mp4"
        };

        [SerializeField, Min(0), Tooltip("Initial video index shown when the scene starts.")]
        private int startIndex;

        [SerializeField, Tooltip("When enabled, the controller prepares and plays the start index on Start.")]
        private bool playOnStart = true;

        [SerializeField, Min(0.1f), Tooltip("Maximum seconds to wait for VideoPlayer Prepare.")]
        private float prepareTimeout = 10f;

        [SerializeField, Min(0.1f), Tooltip("Maximum seconds to wait for a visible first frame after Prepare.")]
        private float firstFrameTimeout = 5f;

        [SerializeField, Min(0f), Tooltip("Delay after Space is accepted before the prepared next video becomes visible.")]
        private float transitionSwitchDelay = 0.5f;

        [SerializeField, Min(0f), Tooltip("Minimum seconds between accepted Space inputs.")]
        private float inputCooldown = 2f;

        [SerializeField, Tooltip("When enabled, the video cycle outputs detailed Console logs.")]
        private bool verboseLogs = true;

        public IReadOnlyList<string> VideoRelativePaths => videoRelativePaths;
        public int StartIndex => Mathf.Clamp(startIndex, 0, Mathf.Max(0, VideoCount - 1));
        public bool PlayOnStart => playOnStart;
        public float PrepareTimeout => prepareTimeout;
        public float FirstFrameTimeout => firstFrameTimeout;
        public float TransitionSwitchDelay => transitionSwitchDelay;
        public float InputCooldown => inputCooldown;
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
            prepareTimeout = Mathf.Max(0.1f, prepareTimeout);
            firstFrameTimeout = Mathf.Max(0.1f, firstFrameTimeout);
            transitionSwitchDelay = Mathf.Max(0f, transitionSwitchDelay);
            inputCooldown = Mathf.Max(0f, inputCooldown);
        }
    }
}
