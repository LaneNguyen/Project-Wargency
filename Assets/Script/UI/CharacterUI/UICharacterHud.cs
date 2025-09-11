using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{

    // HUD nhỏ bay trên đầu agent nè
    // có thanh energy, stress, mặt mood, tên nhân vật
    // bind trực tiếp agent + stats rồi follow theo world position
    // stress tăng mạnh sẽ nổ effect cảnh báo cho vui mắt
    // HUD nổi trên đầu agent nè
    // show Energy và Stress kèm mood với tên nhân vật
    // bind trực tiếp vào CharacterAgent + CharacterStats để update mượt
    public class UICharacterHUD : MonoBehaviour
    {
        [Header("Target (runtime)")]
        [SerializeField] private Transform worldTarget;               // transform của agent
        [SerializeField] private CharacterStats stats;                // stats hiện tại
        public CharacterAgent Agent { get; private set; }

        [Header("Cameras & Canvas")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Camera uiCamera;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);

        [Header("UI Refs")]
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

        [Header("Effects")]
        [SerializeField] private RectTransform effectAnchor;
        [SerializeField] private GameObject stressSpikeEffect;
        [SerializeField] private bool compensateParentScale = true;
        [SerializeField] private Vector2 effectPixelSize = new Vector2(160, 160);
        [SerializeField] private int spikeThreshold = 10; // tăng >=10/lần → spike
        private int _lastStress = -1;

        // Hiệu ứng Stress cao (duy trì trong lúc > ngưỡng) 
        [Header("High Stress VFX (>=80)")]
        [Tooltip("Prefab particle sẽ hiển thị khi Stress vượt ngưỡng cao.")]
        [SerializeField] private GameObject stressOver80Effect;
        [Tooltip("Ngưỡng phần trăm Stress để bật VFX (0..100).")]
        [Range(0, 100)][SerializeField] private int stressVfxThreshold = 80;
        [Tooltip("Hysteresis: chỉ tắt khi Stress tụt xuống dưới (threshold - hysteresis).")]
        [Range(0, 50)][SerializeField] private int stressVfxHysteresis = 5;
        [Tooltip("Nếu bật, VFX sẽ được giữ loop tới khi Stress hạ dưới ngưỡng reset. Tắt = chỉ nổ 1 lần có cooldown.")]
        [SerializeField] private bool stressVfxPersistWhileHigh = true;
        [Tooltip("Cooldown (giây) cho chế độ không persist (tránh spam khi dao động quanh ngưỡng).")]
        [Min(0f)][SerializeField] private float stressVfxCooldown = 1.0f;

        private GameObject _activeHighStressFx;     // instance đang chạy (chế độ persist)
        private bool _overThresholdPlayed = false;  // flag (chế độ không persist)
        private float _overThresholdCooldownLeft = 0f;

        // internal
        private int maxEnergy = 100, maxStress = 100;
        private float vEnergy01, vStress01;
        public System.Action<string> OnAlert;

        [Header("Debug")]
        [SerializeField] private bool logAutoBindIssues = false; // đã có WireUp lo bind, tắt cảnh báo mặc định

        private void Start()
        {
            TryAutoBind(); // an toàn khi prefab đã gán sẵn target/stats (optional)
        }

        private void OnEnable()
        {
            BindStats(true);   // nếu stats đã có, subscribe ngay
        }

        private void OnDisable()
        {
            BindStats(false);  // hủy đăng ký event khi disable
            // đảm bảo dọn VFX đang bật nếu HUD bị tắt
            if (_activeHighStressFx != null)
            {
                Destroy(_activeHighStressFx);
                _activeHighStressFx = null;
            }
        }

        private void LateUpdate()
        {
            UpdatePositionFollow();
            SmoothUpdateBars();

            //giảm cooldown mỗi frame (chế độ non-persist)
            if (_overThresholdCooldownLeft > 0f)
                _overThresholdCooldownLeft -= Time.deltaTime;
        }

        // ================== API CHÍNH ==================

        public void Bind(CharacterAgent agent, CharacterStats s)
        {
            Agent = agent;
            worldTarget = agent ? agent.transform : null;
            stats = s;

            if (nameText != null && agent != null)
                nameText.text = agent.DisplayName;

            // re-subscribe cho chắc
            BindStats(false);
            BindStats(true);

            // vẽ ngay (không đợi tick/event)
            if (stats != null)
                RefreshFromStats(stats);

            SnapNow();
        }

        public void SetWorldTarget(Transform target)
        {
            worldTarget = target;

            if (stats == null && worldTarget != null)
            {
                stats = worldTarget.GetComponent<CharacterStats>()
                        ?? worldTarget.GetComponentInChildren<CharacterStats>()
                        ?? worldTarget.GetComponentInParent<CharacterStats>();
            }

            BindStats(false);
            BindStats(true);

            if (stats != null)
                RefreshFromStats(stats);
        }

        public void OnAgentStatsChanged(CharacterAgent agent)
        {
            RefreshFromStats(agent);
        }

        // ================== NỘI BỘ ==================

        private void TryAutoBind()
        {
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            if (uiCamera == null && rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                uiCamera = rootCanvas.worldCamera;
            if (worldCamera == null) worldCamera = Camera.main;

            if (worldTarget == null)
            {
                var maybeAgent = GetComponentInParent<CharacterAgent>();
                if (maybeAgent != null) worldTarget = maybeAgent.transform;
            }

            if (stats == null && worldTarget != null)
            {
                stats = worldTarget.GetComponent<CharacterStats>()
                        ?? worldTarget.GetComponentInChildren<CharacterStats>()
                        ?? worldTarget.GetComponentInParent<CharacterStats>();
            }

            if (stats == null && logAutoBindIssues)
            {
                Debug.LogWarning($"[UICharacterHUD] Chưa bind được CharacterStats. Hãy gọi hud.Bind(agent, agent.stats) hoặc SetWorldTarget(agent.transform).", this);
            }
            else if (stats != null)
            {
                RefreshFromStats(stats);
            }
        }

        private void BindStats(bool subscribe)
        {
            if (stats == null) return;

            maxEnergy = Mathf.Max(1, stats.MaxEnergy);
            maxStress = Mathf.Max(1, stats.MaxStress);

            if (subscribe)
            {
                stats.StatsChanged += HandleStatsChanged;
                // kéo HUD về trạng thái hiện tại
                HandleStatsChanged(stats.Energy, stats.Stress);
            }
            else
            {
                stats.StatsChanged -= HandleStatsChanged;
            }

            if (nameText != null && Agent != null)
                nameText.text = Agent.DisplayName;
            else if (nameText != null && stats.TryGetComponent(out CharacterAgent a))
                nameText.text = a.DisplayName;
        }

        private void HandleStatsChanged(int energy, int stress)
        {
            float e01 = Mathf.Clamp01((float)energy / Mathf.Max(1, maxEnergy));
            float s01 = Mathf.Clamp01((float)stress / Mathf.Max(1, maxStress));

            if (!smooth)
            {
                if (energyBar) energyBar.normalizedValue = e01;
                if (stressBar) stressBar.normalizedValue = s01;
            }
            else
            {
                vEnergy01 = e01;
                vStress01 = s01;
            }

            // Spike effect khi Stress tăng nhanh (giữ nguyên)
            if (_lastStress >= 0 && (stress - _lastStress) >= spikeThreshold)
                PlayHudEffect(stressSpikeEffect);
            _lastStress = stress;

            //High-stress VFX stable (persist hoặc burst có cooldown) 
            int thresholdAbs = Mathf.RoundToInt((stressVfxThreshold / 100f) * maxStress);
            int resetAbs = Mathf.Max(0, thresholdAbs - stressVfxHysteresis);

            if (stressVfxPersistWhileHigh)
            {
                // PERSIST MODE: tạo/cất VFX theo trạng thái Stress
                if (stress >= thresholdAbs)
                {
                    if (_activeHighStressFx == null)
                    {
                        _activeHighStressFx = SpawnPersistentHudEffect(stressOver80Effect);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (_activeHighStressFx) Debug.Log($"[UICharacterHUD] HighStress ON → spawn loop FX (Stress {stress}/{maxStress})");
#endif
                    }
                }
                else if (stress <= resetAbs)
                {
                    if (_activeHighStressFx != null)
                    {
                        // Dừng hẳn rồi hủy (để particle tan tự nhiên)
                        StopAndDestroyParticle(_activeHighStressFx, 0.25f);
                        _activeHighStressFx = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[UICharacterHUD] HighStress OFF → stop FX (Stress {stress}/{maxStress})");
#endif
                    }
                }
                // nếu nằm giữa resetAbs..thresholdAbs thì giữ trạng thái hiện tại (hysteresis)
            }
            else
            {
                // BURST MODE: nổ một phát khi vượt ngưỡng, có cooldown + hysteresis
                if (stress >= thresholdAbs)
                {
                    if (!_overThresholdPlayed && _overThresholdCooldownLeft <= 0f)
                    {
                        PlayHudEffect(stressOver80Effect);
                        _overThresholdPlayed = true;
                        _overThresholdCooldownLeft = stressVfxCooldown;
                    }
                }
                else if (stress <= resetAbs)
                {
                    _overThresholdPlayed = false;
                }
            }

            UpdateMoodIcon(e01, s01);
            MaybeAlert(stress);
        }

        // === ĐÃ MỞ: public để HUDWireUp gọi thẳng bằng CharacterStats ===
        public void RefreshFromStats(CharacterStats s)
        {
            if (s == null) return;
            HandleStatsChanged(s.Energy, s.Stress);
        }

        // === Overload nhận Agent (forward từ OnAgentStatsChanged) ===
        public void RefreshFromStats(CharacterAgent agent)
        {
            if (agent == null) return;
            RefreshFromStats(agent.GetComponent<CharacterStats>());
        }

        private void SmoothUpdateBars()
        {
            if (!smooth) return;
            if (energyBar)
                energyBar.normalizedValue = Mathf.MoveTowards(energyBar.normalizedValue, vEnergy01, smoothSpeed * Time.deltaTime);
            if (stressBar)
                stressBar.normalizedValue = Mathf.MoveTowards(stressBar.normalizedValue, vStress01, smoothSpeed * Time.deltaTime);
        }

        private void UpdateMoodIcon(float e01, float s01)
        {
            if (moodIcon == null) return;

            // stress dùng ngưỡng tuyệt đối, energy dùng tỷ lệ
            bool stressHigh = s01 >= (float)stressDanger / Mathf.Max(1, maxStress);
            bool stressWarned = s01 >= (float)stressWarn / Mathf.Max(1, maxStress);

            Sprite pick =
                stressHigh ? moodStressed :
                stressWarned ? moodTired :
                (e01 <= 0.25f) ? moodTired :
                (e01 <= 0.5f) ? moodOk :
                moodHappy;

            if (pick != null) moodIcon.sprite = pick;
        }

        private void MaybeAlert(int stress)
        {
            if (OnAlert == null) return;
            if (stress >= stressDanger) OnAlert.Invoke("⚠️ Stress nguy hiểm! Cần nghỉ ngơi.");
            else if (stress >= stressWarn) OnAlert.Invoke("⚠️ Stress cao, chú ý giảm tải");
        }

        private void UpdatePositionFollow()
        {
            if (worldTarget == null || rootCanvas == null) return;

            if (worldCamera == null) worldCamera = Camera.main;
            if (uiCamera == null) uiCamera = rootCanvas.worldCamera;

            Vector3 worldPos = worldTarget.position + worldOffset;
            Vector2 screen = worldCamera != null
                ? (Vector2)worldCamera.WorldToScreenPoint(worldPos)
                : (Vector2)worldPos;

            RectTransform parentRect = transform.parent as RectTransform;
            if (parentRect == null) parentRect = (RectTransform)rootCanvas.transform;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out var localPoint))
            {
                RectTransform rt = (RectTransform)transform;
                if (!float.IsNaN(localPoint.x) && !float.IsNaN(localPoint.y))
                    rt.anchoredPosition = localPoint;
            }
        }

        // ====== Effect helpers ======

        // Burst effect: nổ 1 cái rồi tự hủy (dùng cho spike hoặc burst mode)
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

            // Tự động hủy theo tổng thời lượng particle (ổn định hơn hủy cứng 1s)
            float life = ComputeTotalParticleLifetime(go);
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null && !ps.main.playOnAwake) ps.Play(true);
            Destroy(go, life);
        }

        // PERSIST effect: bật loop khi stress cao và giữ tới khi hạ ngưỡng
        private GameObject SpawnPersistentHudEffect(GameObject prefab)
        {
            if (prefab == null) return null;

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

            // Bật loop cho tất cả ParticleSystem con (đề phòng prefab không loop)
            var all = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in all)
            {
                var main = p.main;
                main.loop = true;
                // Tránh destroy tự động khi dừng
#if UNITY_2021_2_OR_NEWER
                main.stopAction = ParticleSystemStopAction.None;
#endif
                if (!main.playOnAwake) p.Play(true);
            }

            return go;
        }

        private void StopAndDestroyParticle(GameObject go, float extraDelay)
        {
            if (go == null) return;
            // Cho particle dừng phát, chờ tan rồi hủy
            float life = 0.15f; // mặc định
            var all = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in all)
            {
                var main = p.main;
                main.loop = false; // tắt loop để nó tự tan
                p.Stop(true, ParticleSystemStopBehavior.StopEmitting); // ngừng phát thêm hạt
                // Ước lượng thời gian tan
                float est = main.duration + main.startLifetime.constantMax;
                if (est > life) life = est;
            }
            Destroy(go, life + Mathf.Max(0f, extraDelay));
        }

        private float ComputeTotalParticleLifetime(GameObject go)
        {
            float maxT = 1.0f; // fallback an toàn
            var all = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in all)
            {
                var m = p.main;
                float startLife = m.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                    ? m.startLifetime.constantMax
                    : (m.startLifetime.mode == ParticleSystemCurveMode.Constant ? m.startLifetime.constant : m.startLifetime.constantMax);
                float t = m.duration + startLife;
                if (t > maxT) maxT = t;
            }
            return maxT + 0.15f; // thêm 0.15s đệm
        }

        public void SnapNow()
        {
            UpdatePositionFollow();
            if (energyBar) energyBar.normalizedValue = vEnergy01;
            if (stressBar) stressBar.normalizedValue = vStress01;
        }

        public void SetAgent(CharacterAgent a)
        {
            Agent = a;
            var drags = GetComponentsInChildren<UICharacterDraggable>(true);
            foreach (var d in drags) d.Bind(Agent);
        }
    }
}
