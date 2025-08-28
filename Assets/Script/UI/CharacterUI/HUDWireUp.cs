using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wargency.Gameplay;
using Wargency.UI;

public class UIHudWireUp : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform hudLayer;            // RectTransform trong Canvas để chứa HUD
    [SerializeField] private UILiveAlertsFeed alertsFeed;   // Panel feed cảnh báo
    [SerializeField] private UICharacterHUD hudPrefab;      // Prefab HUD

    [Header("Game Refs (Optional)")]
    [SerializeField] private CharacterHiringService hiringService;
    [SerializeField] private Transform agentsRoot;                 // Parent chứa các agent đã/đang spawn 
    private readonly Dictionary<CharacterAgent, UICharacterHUD> map = new();

    private void Awake()
    {
        // Nghe sự kiện thuê người
        if (hiringService != null)
            hiringService.OnAgentHired += HandleAgentHired;
    }

    private void OnDestroy()
    {
        if (hiringService != null)
            hiringService.OnAgentHired -= HandleAgentHired;
    }

    private void Start()
    {
        // Gắn HUD cho các agent đang có sẵn trong scene lúc bắt đầu
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
        TryAttachHud(agent);
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
                hud.OnAlert -= alertsFeed.Push;
                Destroy(hud.gameObject);
            }
            map.Remove(agent);
        }
    }

    private IEnumerator RegisterNextFrame(CharacterAgent agent)
    {
        yield return null; // chờ 1 frame cho agent & camera ổn định
        var hud = Instantiate(hudPrefab, hudLayer);
        var stats = agent.GetComponent<CharacterStats>();
        hud.Bind(agent, stats);
        // SAU KHI HUD ĐÃ GẮN AGENT => Báo cho các avatar draggable biết
        NotifyDraggables(hud, agent); // <= thêm dòng này để dứt điểm null Agent khi kéo
        hud.OnAlert += alertsFeed.Push;
        hud.SnapNow(); // ép HUD nhảy đúng vị trí ngay lập tức thay vì loạn xạ ngầu
        // SAU KHI HUD ĐÃ GẮN AGENT => Báo cho các avatar draggable biết
        NotifyDraggables(hud, agent);
    }
    private void TryAttachHud(CharacterAgent agent)
    {
        if (agent == null || hudPrefab == null || hudLayer == null) return;
        if (map.ContainsKey(agent)) return; // đã có HUD

        var hud = Instantiate(hudPrefab, hudLayer);
        var stats = agent.GetComponent<CharacterStats>();
        hud.Bind(agent, stats);
        // Báo cho các avatar draggable biết
        NotifyDraggables(hud, agent); 
        hud.OnAlert += alertsFeed.Push;

        map[agent] = hud;

        // Tự gỡ HUD khi agent bị destroy
        var auto = agent.gameObject.AddComponent<UIHudWireUp_AutoCleanup>();
        auto.Init(this, agent);
    }

    // Báo cho mọi UICharacterDraggable bên trong HUD biết Agent đã sẵn sàng
    // HUD vừa biết 'Agent là ai' => nói cho avatar con biết luôn, để kéo-thả không bị null
    private void NotifyDraggables(UICharacterHUD hud, CharacterAgent agent)
    {
        if (hud == null || agent == null) return;
        var drags = hud.GetComponentsInChildren<Wargency.UI.UICharacterDraggable>(true);
        foreach (var d in drags)
        {
            d.Bind(agent); // gán trực tiếp → không phụ thuộc auto tìm, tránh lệch thời điểm
        }
    }

}