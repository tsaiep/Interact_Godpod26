using RFIDBaggage.Core;
using RFIDBaggage.Levels;
using UnityEngine;

namespace RFIDBaggage.Input
{
    public sealed class DebugLevelInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Main flow manager that owns state transitions.")]
        private GameFlowManager gameFlowManager;

        [SerializeField, Tooltip("Level database used to look up test RFID IDs.")]
        private LevelDatabase levelDatabase;

        [SerializeField, Tooltip("RFID IDs triggered by number keys 1 through 6.")]
        private string[] numberKeyRfidIds =
        {
            "RFID_01",
            "RFID_02",
            "RFID_03",
            "RFID_04",
            "RFID_05",
            "RFID_06"
        };

        private void Update()
        {
            if (gameFlowManager == null)
            {
                return;
            }

            HandleLevelKeys();
            HandleFlowKeys();
        }

        private void HandleLevelKeys()
        {
            for (int i = 0; i < numberKeyRfidIds.Length && i < 6; i++)
            {
                KeyCode keyCode = (KeyCode)((int)KeyCode.Alpha1 + i);
                if (!UnityEngine.Input.GetKeyDown(keyCode))
                {
                    continue;
                }

                if (levelDatabase == null)
                {
                    Debug.LogWarning("[DebugLevelInput] LevelDatabase is not assigned.", this);
                    return;
                }

                string rfidId = numberKeyRfidIds[i];
                if (!levelDatabase.TryGetByRfidId(rfidId, out LevelConfig level))
                {
                    Debug.LogWarning($"[DebugLevelInput] No test level found for RFID: {rfidId}", this);
                    return;
                }

                gameFlowManager.RequestStartLevel(level);
            }
        }

        private void HandleFlowKeys()
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                gameFlowManager.ReportRecoverableError("Debug safe reset requested with Escape.");
            }
        }
    }
}
