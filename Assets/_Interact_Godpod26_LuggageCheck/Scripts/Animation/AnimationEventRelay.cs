using UnityEngine;
using UnityEngine.Events;

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField]
    private UnityEvent[] events;

    // 在 Animation Event 中設定 Int 參數
    public void InvokeEvent(int index)
    {
        if (index < 0 || index >= events.Length)
        {
            Debug.LogWarning(
                $"Animation Event index out of range：{index}",
                this
            );

            return;
        }

        events[index]?.Invoke();
    }
}
