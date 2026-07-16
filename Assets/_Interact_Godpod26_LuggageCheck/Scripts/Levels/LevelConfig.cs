using UnityEngine;

namespace RFIDBaggage.Levels
{
    [CreateAssetMenu(
        fileName = "LevelConfig",
        menuName = "RFID Baggage/Level Config"
    )]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Basic")]
        [SerializeField, Tooltip("Unique level identifier, for example Level_01.")]
        private string levelId;

        [SerializeField, Tooltip("Display name used in logs and future UI.")]
        private string displayName;

        [SerializeField, Tooltip("RFID identifier that starts this level.")]
        private string rfidId;

        [Header("StreamingAssets Relative Paths")]
        [SerializeField, Tooltip("Intro video path relative to StreamingAssets.")]
        private string introVideoRelativePath;

        [SerializeField, Tooltip("Gameplay looping background video path relative to StreamingAssets.")]
        private string gameplayVideoRelativePath;

        [SerializeField, Tooltip("Success video path relative to StreamingAssets.")]
        private string successVideoRelativePath;

        [SerializeField, Tooltip("Failure video path relative to StreamingAssets.")]
        private string failureVideoRelativePath;

        [SerializeField, Tooltip("Final-frame image path relative to StreamingAssets.")]
        private string finalFrameImageRelativePath;

        [Header("Result Performance Cues")]
        [SerializeField, Min(0f), Tooltip("Seconds before the success video ends to fire the success performance event. Set 0 to disable.")]
        private float successPerformanceLeadTime;

        [SerializeField, Min(0f), Tooltip("Seconds before the failure video ends to fire the failure performance event. Set 0 to disable.")]
        private float failurePerformanceLeadTime;

        [SerializeField, Min(0f), Tooltip("Seconds after entering SuccessPreparing or FailurePreparing from Gameplay before pausing the gameplay loop and preparing the result video.")]
        private float gameplayVideoStopDelayBeforeResultPrepare = 5f;

        [Header("Scene References")]
        [SerializeField, Tooltip("Optional level root. Prefer GameplayController Level Views for scene object references.")]
        private GameObject levelRoot;

        [Header("Gameplay Rules")]
        [SerializeField, Min(0.1f), Tooltip("Gameplay duration in seconds.")]
        private float gameplayDuration = 15f;

        [SerializeField, Min(0f), Tooltip("Delay after entering Gameplay before input and countdown start.")]
        private float gameplayStartDelay;

        [SerializeField, Min(0f), Tooltip("Countdown warning threshold in seconds.")]
        private float warningStartTime = 5f;

        [SerializeField, Min(0f), Tooltip("Cooldown after direction navigation input in seconds.")]
        private float selectionInputCooldown = 0.15f;

        [SerializeField, Min(0f), Tooltip("Cooldown after confirm input in seconds.")]
        private float confirmInputCooldown = 0.25f;

        [SerializeField, Min(0f), Tooltip("Legacy confirm cooldown. Kept for compatibility with phase 1 assets.")]
        private float inputCooldown = 0.2f;

        [SerializeField, Tooltip("Whether a wrong selection deducts time.")]
        private bool wrongSelectionDeductsTime;

        [SerializeField, Min(0f), Tooltip("Seconds deducted when wrongSelectionDeductsTime is enabled.")]
        private float wrongSelectionTimePenalty;

        public string LevelId => levelId;
        public string DisplayName => displayName;
        public string RfidId => rfidId;
        public string IntroVideoRelativePath => introVideoRelativePath;
        public string GameplayVideoRelativePath => gameplayVideoRelativePath;
        public string SuccessVideoRelativePath => successVideoRelativePath;
        public string FailureVideoRelativePath => failureVideoRelativePath;
        public string FinalFrameImageRelativePath => finalFrameImageRelativePath;
        public float SuccessPerformanceLeadTime => successPerformanceLeadTime;
        public float FailurePerformanceLeadTime => failurePerformanceLeadTime;
        public float GameplayVideoStopDelayBeforeResultPrepare => gameplayVideoStopDelayBeforeResultPrepare;
        public GameObject LevelRoot => levelRoot;
        public float GameplayDuration => gameplayDuration;
        public float GameplayStartDelay => gameplayStartDelay;
        public float WarningStartTime => warningStartTime;
        public float SelectionInputCooldown => selectionInputCooldown;
        public float ConfirmInputCooldown => confirmInputCooldown > 0f ? confirmInputCooldown : inputCooldown;
        public float InputCooldown => inputCooldown;
        public bool WrongSelectionDeductsTime => wrongSelectionDeductsTime;
        public float WrongSelectionTimePenalty => wrongSelectionTimePenalty;

        public bool IsValid(out string message)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                message = $"{name} has an empty level ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rfidId))
            {
                message = $"{name} has an empty RFID ID.";
                return false;
            }

            if (gameplayDuration <= 0f)
            {
                message = $"{name} has an invalid gameplay duration.";
                return false;
            }

            if (successPerformanceLeadTime < 0f)
            {
                message = $"{name} has an invalid success performance lead time.";
                return false;
            }

            if (failurePerformanceLeadTime < 0f)
            {
                message = $"{name} has an invalid failure performance lead time.";
                return false;
            }

            if (gameplayVideoStopDelayBeforeResultPrepare < 0f)
            {
                message = $"{name} has an invalid gameplay video stop delay before result prepare.";
                return false;
            }

            if (inputCooldown < 0f)
            {
                message = $"{name} has an invalid input cooldown.";
                return false;
            }

            if (gameplayStartDelay < 0f)
            {
                message = $"{name} has an invalid gameplay start delay.";
                return false;
            }

            if (selectionInputCooldown < 0f)
            {
                message = $"{name} has an invalid selection input cooldown.";
                return false;
            }

            if (confirmInputCooldown < 0f)
            {
                message = $"{name} has an invalid confirm input cooldown.";
                return false;
            }

            if (wrongSelectionTimePenalty < 0f)
            {
                message = $"{name} has an invalid wrong selection time penalty.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public string GetNormalizedLevelId()
        {
            return NormalizeIdentifier(levelId);
        }

        public string GetNormalizedRfidId()
        {
            return NormalizeIdentifier(rfidId);
        }

        public static string NormalizeIdentifier(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void OnValidate()
        {
            if (gameplayDuration <= 0f)
            {
                gameplayDuration = 15f;
            }

            inputCooldown = Mathf.Max(0f, inputCooldown);
            successPerformanceLeadTime = Mathf.Max(0f, successPerformanceLeadTime);
            failurePerformanceLeadTime = Mathf.Max(0f, failurePerformanceLeadTime);
            gameplayVideoStopDelayBeforeResultPrepare = Mathf.Max(0f, gameplayVideoStopDelayBeforeResultPrepare);
            gameplayStartDelay = Mathf.Max(0f, gameplayStartDelay);
            warningStartTime = Mathf.Max(0f, warningStartTime);
            selectionInputCooldown = Mathf.Max(0f, selectionInputCooldown);
            confirmInputCooldown = Mathf.Max(0f, confirmInputCooldown);
            wrongSelectionTimePenalty = Mathf.Max(0f, wrongSelectionTimePenalty);
        }
    }
}
