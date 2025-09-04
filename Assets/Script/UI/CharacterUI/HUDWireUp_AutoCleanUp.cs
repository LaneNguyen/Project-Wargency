using UnityEngine;
using Wargency.Gameplay;

// tiện ích nhỏ để tự gỡ HUD khi agent bị destroy
// agent đi rồi thì gọi wire.Unregister để dọn map
// tránh HUD mồ côi trôi lơ lửng

public class UIHudWireUp_AutoCleanup : MonoBehaviour
{
    private UIHudWireUp wire;
    private CharacterAgent agent;

    public void Init(UIHudWireUp w, CharacterAgent a)
    {
        wire = w;
        agent = a;
    }

    private void OnDestroy()
    {
        // Agent biến mất => gỡ HUD tương ứng
        if (wire != null) wire.Unregister(agent);
    }
}