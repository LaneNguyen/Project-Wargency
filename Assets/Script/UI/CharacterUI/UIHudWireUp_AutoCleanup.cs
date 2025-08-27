using UnityEngine;
using Wargency.Gameplay;

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
        // Agent biến mất → gỡ HUD tương ứng
        if (wire != null) wire.Unregister(agent);
    }
}
