using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wargency.Gameplay;
using Wargency.UI;

// script này gắn HUD trên đầu mỗi agent nè
// nghe HiringService.OnAgentHired để spawn HUD đúng lúc
// cũng dọn HUD khi agent biến mất cho khỏi rác

public class UIHudWireUp : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform hudLayer;            // RectTransform trong Canvas để chứa HUD
    [SerializeField] private UILiveAlertsFeed alertsFeed;   // Panel feed cảnh báo
    [SerializeField] private UICharacterHUD hudPrefab;      // Prefab HUD

    [Header("Game Refs (Optional)")]
    [SerializeField] private CharacterHiringService hiringService;
    [SerializeField] private Transform agentsRoot;          // Parent chứa các agent đã/đang spawn 

    public static UILiveAlertsFeed Alerts;
    private readonly Dictionary<CharacterAgent, UICharacterHUD> map = new();

    private void Awake()
    {
        if (hiringService != null)
            // liên hệ với HiringService => khi thuê xong thì gắn HUD cho agent mới
            hiringService.OnAgentHired += HandleAgentHired;
        Alerts = alertsFeed;
    }

    private void OnDestroy()
    {
        if (hiringService != null)
            hiringService.OnAgentHired -= HandleAgentHired;
    }

    private void Start()
    {
        BootstrapExistingAgents();
    }

    private void BootstrapExistingAgents()
    {
        CharacterAgent[] agents = agentsRoot != null
            ? agentsRoot.GetComponentsInChildren<CharacterAgent>(true)
            : FindObjectsOfType<CharacterAgent>(true);

        foreach (var a in agents) TryAttachHud(a);
    }

    private void HandleAgentHired(CharacterAgent agent)
    {
        StartCoroutine(RegisterNextFrame(agent));
    }

    public void Register(CharacterAgent agent)
    {
        StartCoroutine(RegisterNextFrame(agent));
    }

    public void Unregister(CharacterAgent agent) // Cho despawn/unhire
    {
        if (agent == null) return;

        if (map.TryGetValue(agent, out var hud))
        {
            if (hud != null)
            {
                if (alertsFeed != null) hud.OnAlert -= alertsFeed.Push;
                agent.OnStatsChanged -= hud.OnAgentStatsChanged; // hủy đăng ký event
                Destroy(hud.gameObject);
            }
            map.Remove(agent);
        }
    }

    private IEnumerator RegisterNextFrame(CharacterAgent agent)
    {
        yield return null;              // chờ 1 frame
        TryAttachHud(agent);            // funnel về 1 đường
    }

    private void TryAttachHud(CharacterAgent agent)
    {
        if (agent == null || hudPrefab == null || hudLayer == null) return;

        // Nếu đã có HUD → không spawn thêm
        if (map.TryGetValue(agent, out var existed) && existed != null)
            return;

        // Nếu Dictionary có key nhưng HUD đã bị Destroy (null), dọn dẹp key cũ
        if (existed == null) map.Remove(agent);

        var hud = Instantiate(hudPrefab, hudLayer);
        var stats = agent.GetComponent<CharacterStats>();

        // 1) Bind đúng Agent + Stats
        hud.Bind(agent, stats);

        // 2) Đăng ký delegate đúng chữ ký (Action<CharacterAgent>)
        // liên hệ với CharacterAgent => HUD nghe stats để cập nhật bars
        agent.OnStatsChanged += hud.OnAgentStatsChanged;

        // 3) Ép HUD đồng bộ ngay: bắn sự kiện hiện trạng + vẽ thẳng
        if (stats != null)
        {
            stats.Notify();                 // bắn sự kiện StatsChanged(int,int) hiện trạng
            hud.RefreshFromStats(stats);    // <== đã mở public trong UICharacterHUD
        }

        // 4) Báo cho các avatar draggable biết Agent đã sẵn sàng
        NotifyDraggables(hud, agent);

        if (alertsFeed != null)
            hud.OnAlert += alertsFeed.Push;

        // 5) Đặt đúng vị trí tức thì (tránh nhảy)
        hud.SnapNow();

        // 6) Lưu map để tránh tạo thêm HUD cho agent này
        map[agent] = hud;

        // 7) Tự gỡ HUD khi agent bị destroy
        var auto = agent.gameObject.GetComponent<UIHudWireUp_AutoCleanup>();
        if (auto == null) auto = agent.gameObject.AddComponent<UIHudWireUp_AutoCleanup>();
        auto.Init(this, agent);
    }

    // Báo cho mọi UICharacterDraggable bên trong HUD biết Agent đã sẵn sàng
    private void NotifyDraggables(UICharacterHUD hud, CharacterAgent agent)
    {
        if (hud == null || agent == null) return;
        var drags = hud.GetComponentsInChildren<Wargency.UI.UICharacterDraggable>(true);
        for (int i = 0; i < drags.Length; i++)
            drags[i].Bind(agent);
    }
}