using UnityEngine;

namespace RFIDBaggage.Video
{
    [CreateAssetMenu(
        fileName = "VideoSystemConfig",
        menuName = "RFID Baggage/Video System Config"
    )]
    public sealed class VideoSystemConfig : ScriptableObject
    {
        [SerializeField, Tooltip("Idle loop video path relative to StreamingAssets.")]
        private string idleVideoRelativePath = "LuggageCheck/Videos/Common/Idle_Loop.mp4";

        [SerializeField, Min(0.1f), Tooltip("Maximum seconds to wait for VideoPlayer Prepare.")]
        private float prepareTimeout = 10f;

        [SerializeField, Min(0.1f), Tooltip("Maximum seconds to wait for a visible first frame after Prepare.")]
        private float firstFrameTimeout = 5f;

        [SerializeField, Min(0.1f), Tooltip("Maximum seconds to wait for final-frame image loading.")]
        private float imageLoadTimeout = 10f;

        [SerializeField, Min(0f), Tooltip("Seconds to keep the idle loop visible after a start signal before preparing the intro video.")]
        private float idleLoopSignalToIntroDelay;

        [SerializeField, Tooltip("When enabled, video modules output detailed Console logs.")]
        private bool verboseVideoLogs = true;

        public string IdleVideoRelativePath => idleVideoRelativePath;
        public float PrepareTimeout => prepareTimeout;
        public float FirstFrameTimeout => firstFrameTimeout;
        public float ImageLoadTimeout => imageLoadTimeout;
        public float IdleLoopSignalToIntroDelay => idleLoopSignalToIntroDelay;
        public bool VerboseVideoLogs => verboseVideoLogs;

        private void OnValidate()
        {
            prepareTimeout = Mathf.Max(0.1f, prepareTimeout);
            firstFrameTimeout = Mathf.Max(0.1f, firstFrameTimeout);
            imageLoadTimeout = Mathf.Max(0.1f, imageLoadTimeout);
            idleLoopSignalToIntroDelay = Mathf.Max(0f, idleLoopSignalToIntroDelay);
        }
    }
}
