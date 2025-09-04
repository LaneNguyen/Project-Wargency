using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleScreenController : MonoBehaviour
{
    [Header("UI (màn start)")]
    [SerializeField] private Canvas canvasStart;          // // cái canvas của màn hình Start nè
    [SerializeField] private CanvasGroup startGroup;      // // chỉnh mờ mờ cho sang chảnh, hông có cũng sống được
    [SerializeField] private Button btnStart;             // // bấm cái này để vô game nhe

    [Header("Cameras (đừng rờ mấy camera khác)")]
    [SerializeField] private Camera uiCamera;             // // camera cho UI title, có thì bật khi ở title
    [SerializeField] private Camera gameplayCamera;       // // camera gameplay, bật khi vô game

    [Header("Panels ở canvas khác (bật/tắt cho đúng cảnh)")]
    [SerializeField] private GameObject[] panelsEnableOnTitle;     // // đang ở title thì mấy cái này bật (VD: title HUD)
    [SerializeField] private GameObject[] panelsEnableOnGameplay;  // // vô game rồi thì mấy cái này bật (VD: gameplay HUD)

    [Header("Fade (cho đẹp tí)")]
    [SerializeField, Range(0f, 2f)] private float fadeDuration = 0.25f;  // // cho có hiệu ứng, hông thích thì để 0 nha

    // ===== Flag toàn cục (đi đường dài) =====
    public static bool IsGameStarted { get; private set; }
    public static event Action OnGameStarted;

    private bool isFading;

    private void Awake()
    {
        // // nối nút Start, bấm là đi luôn
        if (btnStart) btnStart.onClick.AddListener(HandleStartGame);

        // // mấy nút kia (Continue/Settings/Quit/First Selected) hông xài… để sau tính
        // // script trước tính sau :v (em ghi chú vậy cho nhớ)

        // // Nếu canvas đang kiểu Screen Space - Camera mà chưa set Camera → lấy uiCamera
        if (canvasStart && canvasStart.renderMode == RenderMode.ScreenSpaceCamera && canvasStart.worldCamera == null)
            canvasStart.worldCamera = uiCamera;

        // // tránh 2 AudioListener đánh nhau (UI camera hổng cần nghe ngóng gì đâu)
        if (uiCamera)
        {
            var al = uiCamera.GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }

        IsGameStarted = false;

        // // bật màn title lúc đầu (cho chắc), làm liền hông cần fade
        SetTitleActive(true, instant: true);

        // // FirstSelected để sau hẵng tính nhe (giờ bấm chuột/enter vẫn ok)
        // // script trước tính sau ^^
    }

    private void Start()
    {
        // // mở nhạc title cho có mood (xài AudioManager chung nha)
        // // nếu thiếu AUDIO.BGM_POSITIVESTART thì đừng chửi em, thêm giùm cái name vô AUDIO.cs nhe :")
        AudioManager.Instance.PlayBGM(AUDIO.BGM_POSITIVESTART);
    }

    // ==== Bật/tắt Title + chuyển camera + bật/tắt panel liên quan ====
    private void SetTitleActive(bool active, bool instant = false)
    {
        // // camera: title thì bật uiCamera, gameplay thì bật gameplayCamera (chỉ 2 đứa này thôi nha)
        if (uiCamera) uiCamera.enabled = active;
        if (gameplayCamera) gameplayCamera.enabled = !active;

        // // bật/tắt mấy panel ở canvas khác cho đúng cảnh
        TogglePanels(panelsEnableOnTitle, active);
        TogglePanels(panelsEnableOnGameplay, !active);

        // // canvas + fade nhẹ
        if (!canvasStart) return;
        canvasStart.enabled = true; // // để còn fade, tắt gameObject sau

        if (instant || startGroup == null || fadeDuration <= 0f)
        {
            if (startGroup != null)
            {
                startGroup.alpha = active ? 1f : 0f;
                startGroup.interactable = active;
                startGroup.blocksRaycasts = active;
            }
            canvasStart.gameObject.SetActive(active);

            if (!active) MarkStartedIfNeeded(); // // tắt title xong thì coi như đã bắt đầu game
        }
        else
        {
            if (!isFading) StartCoroutine(FadeRoutine(active));
        }
    }

    private System.Collections.IEnumerator FadeRoutine(bool toActive)
    {
        isFading = true;

        if (canvasStart) canvasStart.gameObject.SetActive(true);
        if (startGroup)
        {
            // // đang fade thì đừng cho bấm bậy bạ
            startGroup.interactable = false;
            startGroup.blocksRaycasts = false;
        }

        float t = 0f;
        float from = startGroup ? startGroup.alpha : (toActive ? 0f : 1f);
        float to = toActive ? 1f : 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // // timeScale = 0 vẫn fade ok nha
            float k = Mathf.Clamp01(t / fadeDuration);
            if (startGroup) startGroup.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        if (startGroup)
        {
            startGroup.alpha = to;
            startGroup.interactable = toActive;
            startGroup.blocksRaycasts = toActive;
        }

        if (!toActive)
        {
            if (canvasStart) canvasStart.gameObject.SetActive(false);
            // // đảm bảo camera đang đúng phe
            if (gameplayCamera) gameplayCamera.enabled = true;
            if (uiCamera) uiCamera.enabled = false;

            MarkStartedIfNeeded(); // // xong rồi, vào game thôi
        }

        isFading = false;
    }

    private void TogglePanels(GameObject[] arr, bool enable)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            if (!arr[i]) continue;
            // // bật tắt dịu dàng, không đập phá :v
            arr[i].SetActive(enable);
        }
    }

    private void MarkStartedIfNeeded()
    {
        if (IsGameStarted) return;
        IsGameStarted = true;
        OnGameStarted?.Invoke();
    }

    // ====== Nút bấm ======
    private void HandleStartGame()
    {
        // // bấm cái là vô game liền, có tiếng tách nghe đã tai
        AudioManager.Instance.PlaySE(AUDIO.SE_FANTASYSOUND);

        // // Không chơi saveKey/continue gì hết, để sau tính nghe
        SetTitleActive(false);
        // // TODO: GameLoopController.StartNewGame(); (để dành bài nâng cao)
    }

    // // Continue/Settings/Quit/FirstSelected: chưa xài, để đây làm kỉ niệm
    // // script trước tính sau :")
}
