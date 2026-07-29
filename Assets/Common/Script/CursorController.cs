using UnityEngine;

public class CursorController : MonoBehaviour
{
    [SerializeField]
    private bool lockCursor = false;

    private void Awake()
    {
        ApplyCursorState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Alt+Tab 回到程式後重新套用
        if (hasFocus)
        {
            ApplyCursorState();
        }
    }

    private void ApplyCursorState()
    {
#if !UNITY_EDITOR
        Cursor.visible = false;

        Cursor.lockState = lockCursor
            ? CursorLockMode.Locked
            : CursorLockMode.None;
#endif
    }
}