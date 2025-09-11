using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wargency.UI;

public class TitleScreenController : MonoBehaviour
{
    [Header("UI (Start Screen)")]
    [SerializeField] private Canvas canvasStart;
    [SerializeField] private CanvasGroup startGroup;
    [SerializeField] private Button btnStart;

    [Header("Cameras")]
    [SerializeField] private Camera uiCamera;
    [SerializeField] private Camera gameplayCamera;

    [Header("Panels Toggle")]
    [SerializeField] private GameObject[] panelsEnableOnTitle;
    [SerializeField] private GameObject[] panelsEnableOnGameplay; // include MainUICanvas root

    [Header("Audio IDs")]
    [SerializeField] private string bgmTitle = "BGM_TITLE";
    [SerializeField] private string bgmStoryboard = "BGM_STORY";
    [SerializeField] private string bgmGameplay = "BGM_GAME";

    [Header("Fade Overlay")]
    [SerializeField] private Image fadeOverlay;          // full-screen black Image (alpha 0)
    [SerializeField] private float fadeDuration = 0.6f;  // thời gian fade in/out

    [Header("Storyboard")]
    [SerializeField] private StoryboardPanel storyboardPanelPrefab; // prefab StoryboardPanel mới
    [SerializeField] private Transform uiOverlayParent; // Canvas để đặt storyboard

    private StoryboardPanel activeStoryboard;

    private void Awake()
    {
        if (btnStart) btnStart.onClick.AddListener(StartGameFromTitle);
        SetTitleActive(true, true);
        KillAnyDragArtifacts(); // dọn sạch drag ghost khi vào Title lần đầu
    }

    private void Start()
    {
        TryPlayBGM(bgmTitle);
        // đảm bảo overlay trong suốt lúc đầu
        if (fadeOverlay)
        {
            var c = fadeOverlay.color; c.a = 0f; fadeOverlay.color = c;
        }
    }

    public void StartGameFromTitle()
    {
        // chuyển nhạc sang storyboard
        TryPlayBGM(bgmStoryboard);
        // chạy chuỗi fade → storyboard → fade in
        StartCoroutine(StartSequence());
    }

    private IEnumerator StartSequence()
    {
        // 1) Fade to black
        yield return StartCoroutine(FadeScreen(true));

        // 2) Tắt title UI, tắt gameplay panels để chuẩn bị vào storyboard
        SetTitleActive(false, true);
        TogglePanels(panelsEnableOnGameplay, false);

        // 3) Bật storyboard
        ShowStoryboard();

        // 4) Fade in
        yield return StartCoroutine(FadeScreen(false));
    }

    private void ShowStoryboard()
    {
        if (!storyboardPanelPrefab)
        {
            // không có storyboard → vào gameplay luôn
            TogglePanels(panelsEnableOnGameplay, true);
            TryPlayBGM(bgmGameplay);
            return;
        }

        Transform parent = GetActiveOverlayParent();
        activeStoryboard = Instantiate(storyboardPanelPrefab, parent);
        var rt = activeStoryboard.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        activeStoryboard.transform.SetAsLastSibling();
        activeStoryboard.gameObject.SetActive(true);

        // Khi storyboard chạy xong
        activeStoryboard.OnFinished += () =>
        {
            TogglePanels(panelsEnableOnGameplay, true);
            TryPlayBGM(bgmGameplay);
            // đảm bảo title đã off, camera gameplay on
            SetTitleActive(false, true);
        };

        // Bắt đầu storyboard
        activeStoryboard.Play();
    }

    private Transform GetActiveOverlayParent()
    {
        if (uiOverlayParent && uiOverlayParent.gameObject.activeInHierarchy)
            return uiOverlayParent;

        var canvases = GameObject.FindObjectsOfType<Canvas>(true);
        foreach (var c in canvases)
            if (c.isActiveAndEnabled) return c.transform;

        var go = new GameObject("StoryboardOverlayCanvas",
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas.transform;
    }

    // === Public để gọi từ UnityEvent/Inspector ===
    public void SetTitleActive(bool active) => SetTitleActive(active, false);

    public void SetTitleActive(bool active, bool instant)
    {
        TogglePanels(panelsEnableOnTitle, active);
        TogglePanels(panelsEnableOnGameplay, !active);

        if (canvasStart) canvasStart.gameObject.SetActive(active);
        if (uiCamera) uiCamera.enabled = active;
        if (gameplayCamera) gameplayCamera.enabled = !active;

        if (startGroup)
        {
            startGroup.alpha = active ? 1f : 0f;
            startGroup.interactable = active;
            startGroup.blocksRaycasts = active;
        }

        if (active)
        {
            KillAnyDragArtifacts(); // TEST: mỗi lần bật Title -> reset kéo + xoá ghost
        }

        // Khi ở Title: khóa drag; Khi vào Gameplay: mở lại
        try { Wargency.UI.WorldCharacterSpriteDrag.GlobalEnable = !active; } catch { }
        if (active) KillAnyDragArtifacts();
    }

    private void TogglePanels(GameObject[] arr, bool on)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) arr[i].SetActive(on);
    }

    private IEnumerator FadeScreen(bool toBlack)
    {
        if (!fadeOverlay) yield break;
        float start = fadeOverlay.color.a;
        float end = toBlack ? 1f : 0f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
            float a = Mathf.Lerp(start, end, t);
            var c = fadeOverlay.color; c.a = a; fadeOverlay.color = c;
            yield return null;
        }
        // chốt alpha
        var cc = fadeOverlay.color; cc.a = end; fadeOverlay.color = cc;
    }

    private void TryPlayBGM(string id)
    {
        try { AudioManager.Instance?.PlayBGM(id); } catch { }
    }

    private void KillAnyDragArtifacts()
    {
        // 1) Kết thúc kéo (nếu còn)
        try { UIDragContext.EndDrag(); } catch { }

        // 2) Trả con trỏ về default
        try { CursorManager.Instance?.SetDefaultCursor(); } catch { }

        // 3) Xoá mọi ghost UI còn sót
        var all = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < all.Length; i++)
        {
            var rt = all[i];
            if (rt && rt.name == "DragGhost")
            {
                // dùng DestroyImmediate nếu object đang ở scene ẩn/không active
                if (Application.isPlaying) Destroy(rt.gameObject);
                else DestroyImmediate(rt.gameObject);
            }
        }
    }
}
