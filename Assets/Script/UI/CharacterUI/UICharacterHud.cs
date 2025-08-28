using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // HUD nổi cạnh nhân vật: Energy/Stress + Mood icon + tên
    //Tự bám theo worldTarget (nhân vật) => HUD “lơ lửng” theo offset
    //- Nhận StatsChanged và cập nhật Energy/Stress(mượt)
    //- Đổi Mood icon theo ngưỡng
    //- Có OnAlert để đẩy cảnh báo ra “Live Alerts Feed”
    public class UICharacterHUD : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform worldTarget;      // nhân vật (transform của agent)
        [SerializeField] private Camera uiCamera;            // nếu Canvas = Screen Space - Camera
        [SerializeField] private Canvas rootCanvas;          // canvas chứa HUD
        [SerializeField] private Camera worldCamera;   // camera render MainCamera

        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Refs (UI)")]
        [SerializeField] private Slider energyBar;
        [SerializeField] private Slider stressBar;
        [SerializeField] private Image moodIcon;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("Visual")]
        public bool smooth = true;
        public float smoothSpeed = 6f;

        [Header("Thresholds")]
        [Range(0, 100)] public int stressWarn = 60;
        [Range(0, 100)] public int stressDanger = 85;

        [Header("Mood Icons")]
        public Sprite moodHappy;
        public Sprite moodOk;
        public Sprite moodTired;
        public Sprite moodStressed;

        //Hiệu ứng khi stress nhảy vọt
        [SerializeField] private RectTransform effectAnchor;
        [SerializeField] private GameObject stressSpikeEffect; // prefab UI
        [SerializeField] private bool compensateParentScale = true;
        [SerializeField] private Vector2 effectPixelSize = new Vector2(160, 160);
        [SerializeField] private int spikeThreshold = 10; // tăng >=10 điểm trong 1 lần update → coi là "nhảy vọt"

        private int _lastStress = -1; // theo dõi delta

        // link đến stats (gắn sẵn ở Agent)
        [SerializeField] private Wargency.Gameplay.CharacterStats stats;

        public CharacterAgent Agent { get; private set; }

        // internal
        private int maxEnergy = 100, maxStress = 100;
        private float vEnergy01, vStress01; // giá trị hiển thị mượt

        public System.Action<string> OnAlert; // đẩy text alert ra feed nếu muốn

        private void Awake()
        {
            TryAutoBind();
        }

        private void OnEnable()
        {
            BindStats(true);
        }

        private void OnDisable()
        {
            BindStats(false);
        }

        private void LateUpdate()
        {
            UpdatePositionFollow();
            SmoothUpdateBars();
        }

        private void TryAutoBind()
        {
            // Tự tìm canvas/camera nếu trống
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            if (uiCamera == null && rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                uiCamera = rootCanvas.worldCamera;

            // Nếu không set worldTarget, thử lấy parent
            if (worldTarget == null) worldTarget = transform.parent;

            // camera render world
            if (worldCamera == null)
                worldCamera = Camera.main;               

            // Nếu không set stats, thử tìm trên worldTarget
            if (stats == null && worldTarget != null)
                stats = worldTarget.GetComponentInParent<Wargency.Gameplay.CharacterStats>();
        }

        private void BindStats(bool subscribe)
        {
            if (stats == null) return;

            // Lấy max từ stats nếu có API; nếu không, giữ 100
            maxEnergy = Mathf.Max(1, stats.MaxEnergy);
            maxStress = Mathf.Max(1, stats.MaxStress);

            // gắn event
            if (subscribe)
            {
                stats.StatsChanged += HandleStatsChanged;
                // init
                HandleStatsChanged(stats.Energy, stats.Stress);
            }
            else
            {
                stats.StatsChanged -= HandleStatsChanged;
            }

            // tên
            if (nameText != null && stats.TryGetComponent(out Wargency.Gameplay.CharacterAgent agent))
                nameText.text = agent.DisplayName;
        }

        private void HandleStatsChanged(int energy, int stress)
        {
            float e01 = Mathf.Clamp01((float)energy / maxEnergy);
            float s01 = Mathf.Clamp01((float)stress / maxStress);

            if (!smooth)
            {
                if (energyBar) energyBar.value = e01;
                if (stressBar) stressBar.value = s01;
            }
            else
            {
                vEnergy01 = e01;
                vStress01 = s01;
            }

            // detect spike
            if (_lastStress >= 0 && (stress - _lastStress) >= spikeThreshold)
                PlayHudEffect(stressSpikeEffect);

            _lastStress = stress;

            UpdateMoodIcon(e01, s01);
            MaybeAlert(stress);
        }

        private void SmoothUpdateBars()
        {
            if (!smooth) return;

            if (energyBar)
                energyBar.value = Mathf.MoveTowards(energyBar.value, vEnergy01, smoothSpeed * Time.deltaTime);

            if (stressBar)
                stressBar.value = Mathf.MoveTowards(stressBar.value, vStress01, smoothSpeed * Time.deltaTime);
        }

        private void UpdateMoodIcon(float e01, float s01)
        {
            if (moodIcon == null) return;

            // Quy ước đơn giản: ưu tiên stress
            Sprite pick =
                (s01 >= (float)stressDanger / maxStress) ? moodStressed :
                (s01 >= (float)stressWarn / maxStress) ? moodTired :
                (e01 <= 0.25f) ? moodTired :
                (e01 <= 0.5f) ? moodOk :
                                                             moodHappy;

            if (pick != null) moodIcon.sprite = pick;
        }

        private void MaybeAlert(int stress)
        {
            if (OnAlert == null) return;

            if (stress >= stressDanger)
                OnAlert.Invoke("⚠️ Stress nguy hiểm! Cần nghỉ ngơi.");
            else if (stress >= stressWarn)
                OnAlert.Invoke("⚠️ Stress cao, chú ý giảm tải");
        }

        private void UpdatePositionFollow()
        {
            if (worldTarget == null || rootCanvas == null) return;

            // Bảo đảm camera
            if (worldCamera == null) worldCamera = Camera.main;
            if (uiCamera == null) uiCamera = rootCanvas.worldCamera;

            // Vị trí world của nhân vật + offset
            Vector3 worldPos = worldTarget.position + worldOffset;

            // 1) world → screen (dùng camera render world)
            Vector2 screen = worldCamera != null
                ? (Vector2)worldCamera.WorldToScreenPoint(worldPos)
                : (Vector2)worldPos; // fallback

            // 2) screen → local trong KHUNG CHA THỰC TẾ của HUD (not canvas root!)
            RectTransform parentRect = transform.parent as RectTransform;
            if (parentRect == null) parentRect = (RectTransform)rootCanvas.transform; // fallback

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out var localPoint))
            {
                RectTransform rt = (RectTransform)transform;
                // Không để NaN
                if (!float.IsNaN(localPoint.x) && !float.IsNaN(localPoint.y))
                    rt.anchoredPosition = localPoint;
            }
        }


        private void PlayHudEffect(GameObject prefab)
        {
            if (prefab == null) return;

            var parent = (Transform)(effectAnchor != null ? effectAnchor : transform);
            var go = Instantiate(prefab, parent, false);

            if (go.transform is RectTransform rt)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                if (effectPixelSize.x > 0 && effectPixelSize.y > 0)
                    rt.sizeDelta = effectPixelSize;
            }
            else
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            if (compensateParentScale)
            {
                var ls = parent.lossyScale;
                var inv = new Vector3(
                    ls.x != 0 ? 1f / ls.x : 1f,
                    ls.y != 0 ? 1f / ls.y : 1f,
                    ls.z != 0 ? 1f / ls.z : 1f
                );
                go.transform.localScale = Vector3.Scale(go.transform.localScale, inv);
            }

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null && !ps.main.playOnAwake) ps.Play();

            Destroy(go, 1.0f); // tự huỷ sau 1s (chỉnh theo prefab)
        }

        //Hàm bind cho WireUp work
        public void Bind(CharacterAgent agent, CharacterStats s)
        {
            // Gán target & stats cho HUD
            worldTarget = agent != null ? agent.transform : null;
            stats = s;

            // Cập nhật tên hiển thị nếu có
            if (nameText != null && agent != null)
                nameText.text = agent.DisplayName;

            // Re-subscribe sự kiện để đồng bộ UI ngay
            // (BindStats là method đã có trong class, giữ nguyên access 'private' cũng gọi được vì đang ở trong class)
            BindStats(false);
            BindStats(true);
        }

        public void SnapNow()
        {
            UpdatePositionFollow();
            if (energyBar) energyBar.value = vEnergy01;
            if (stressBar) stressBar.value = vStress01;
        }

        public void SetAgent(Wargency.Gameplay.CharacterAgent a)
        {
            Agent = a;
            var drags = GetComponentsInChildren<Wargency.UI.UICharacterDraggable>(true);
            foreach (var d in drags) d.Bind(Agent);
        }

    }

}
