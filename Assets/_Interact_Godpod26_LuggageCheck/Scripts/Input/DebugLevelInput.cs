using RFIDBaggage.Core;
using UnityEngine;

namespace RFIDBaggage.Input
{
    public sealed class DebugLevelInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Main flow manager that owns state transitions.")]
        private GameFlowManager gameFlowManager;

        [Header("RFID / Manual Level Start Input")]
        [SerializeField, Tooltip("Enable keyboard-wedge RFID input. Manual typing uses the same flow and must end with Enter.")]
        private bool enableRfidInput = true;

        [SerializeField, Min(1), Tooltip("Maximum characters accepted before Enter submits the RFID ID.")]
        private int maxRfidInputLength = 64;

        [SerializeField, Min(0f), Tooltip("Seconds before partially typed RFID input is cleared. Set 0 to keep the buffer until Enter.")]
        private float rfidInputTimeoutSeconds = 5f;

        [SerializeField, Tooltip("Enable first-stage manual phase keys I/P/O/G/R/E. Keep disabled for phase 2 video flow tests.")]
        private bool enableManualPhaseKeys;

        [SerializeField, Tooltip("Enable direct S/F result keys. Keep disabled for phase 3 gameplay tests.")]
        private bool enableDirectResultKeys;

        [SerializeField, Tooltip("Runtime display only. Current RFID characters waiting for Enter.")]
        private string pendingRfidInput = string.Empty;

        private float lastRfidInputTime;

        private void OnValidate()
        {
            maxRfidInputLength = Mathf.Max(1, maxRfidInputLength);
            rfidInputTimeoutSeconds = Mathf.Max(0f, rfidInputTimeoutSeconds);
        }

        private void Update()
        {
            if (gameFlowManager == null)
            {
                return;
            }

            if (enableRfidInput)
            {
                HandleRfidInput();
            }

            if (enableDirectResultKeys)
            {
                HandleResultKeys();
            }

            if (enableManualPhaseKeys)
            {
                HandleManualPhaseKeys();
            }

            HandleEscapeKey();
        }

        private void HandleRfidInput()
        {
            ClearStaleRfidInput();

            string input = UnityEngine.Input.inputString;
            if (string.IsNullOrEmpty(input))
            {
                if (IsSubmitKeyDown())
                {
                    SubmitPendingRfidInput();
                }

                return;
            }

            bool submittedFromInputString = false;
            for (int i = 0; i < input.Length; i++)
            {
                char character = input[i];
                if (character == '\b')
                {
                    RemoveLastRfidInputCharacter();
                    continue;
                }

                if (character == '\n' || character == '\r')
                {
                    SubmitPendingRfidInput();
                    submittedFromInputString = true;
                    continue;
                }

                if (char.IsControl(character))
                {
                    continue;
                }

                AppendRfidInputCharacter(character);
            }

            if (!submittedFromInputString && IsSubmitKeyDown())
            {
                SubmitPendingRfidInput();
            }
        }

        private static bool IsSubmitKeyDown()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        private void AppendRfidInputCharacter(char character)
        {
            if (pendingRfidInput.Length >= maxRfidInputLength)
            {
                Debug.LogWarning($"[DebugLevelInput] RFID input exceeded {maxRfidInputLength} characters and was cleared.", this);
                ClearPendingRfidInput();
                return;
            }

            pendingRfidInput += character;
            lastRfidInputTime = Time.unscaledTime;
        }

        private void RemoveLastRfidInputCharacter()
        {
            if (pendingRfidInput.Length <= 0)
            {
                return;
            }

            pendingRfidInput = pendingRfidInput.Substring(0, pendingRfidInput.Length - 1);
            lastRfidInputTime = Time.unscaledTime;
        }

        private void SubmitPendingRfidInput()
        {
            string rfidId = pendingRfidInput.Trim();
            ClearPendingRfidInput();

            if (string.IsNullOrEmpty(rfidId))
            {
                return;
            }

            if (gameFlowManager.CurrentState != GameState.Idle)
            {
                Debug.LogWarning($"[DebugLevelInput] Ignored RFID input while state is {gameFlowManager.CurrentState}: {rfidId}", this);
                return;
            }

            gameFlowManager.RequestStartLevelByRfid(rfidId);
        }

        private void ClearStaleRfidInput()
        {
            if (rfidInputTimeoutSeconds <= 0f || string.IsNullOrEmpty(pendingRfidInput))
            {
                return;
            }

            if (Time.unscaledTime - lastRfidInputTime >= rfidInputTimeoutSeconds)
            {
                ClearPendingRfidInput();
            }
        }

        private void ClearPendingRfidInput()
        {
            pendingRfidInput = string.Empty;
            lastRfidInputTime = 0f;
        }

        private void HandleResultKeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                gameFlowManager.NotifyGameSuccess();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                gameFlowManager.NotifyGameFailure();
            }

        }

        private void HandleManualPhaseKeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.I))
            {
                gameFlowManager.NotifyLevelInitialized();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                gameFlowManager.NotifyIntroPrepared();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.O))
            {
                gameFlowManager.NotifyIntroCompleted();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.G))
            {
                gameFlowManager.NotifyGamePrepared();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                gameFlowManager.NotifyGameSuccess();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                gameFlowManager.NotifyGameFailure();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                gameFlowManager.NotifyResultPrepared();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                gameFlowManager.NotifyResultCompleted();
            }
        }

        private void HandleEscapeKey()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                gameFlowManager.ReportRecoverableError("Debug safe reset requested with Escape.");
            }
        }
    }
}
