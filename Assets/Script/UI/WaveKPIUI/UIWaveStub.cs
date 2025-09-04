using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UISlider = UnityEngine.UI.Slider;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // UIWaveStub này gom đủ thứ liên quan wave
    // có progress kiểu cũ theo score, có objective rows và có timer nếu có
    // có nút badge để bật tắt panel objective cho gọn
    public class UIWaveStub : MonoBehaviour
    {
        [Header("Legacy Refs (giữ nguyên)")]
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private GameLoopController glc;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private UISlider waveProgressBar; // legacy: score/targetScore

        [Header("Objective (tùy chọn)")]
        [SerializeField] private GameObject objectivePanelRoot;    // panel show/hide (có thể để trống)
        [SerializeField] private Transform objectiveListParent;    // content để spawn dòng

        [Tooltip("Prefab đúng loại UIObjectiveRow. Nếu null, sẽ fallback tạo TMP row đơn giản.")]
        [SerializeField] private UIObjectiveRow objectiveRowPrefab;

        [Tooltip("Fallback template: nếu prefab không có, dùng TMP row này làm khuôn.")]
        [SerializeField] private TextMeshProUGUI rowTemplateTMP;

        [SerializeField] private UISlider objectiveProgressBar;    // progress riêng cho objectives (completed/total)
        [SerializeField] private RectTransform ticksContainer;     // container vạch chia
        [SerializeField] private Image tickPrefab;                 // prefab vạch chia

        [Header("Badge Toggle (tùy chọn)")]
        [SerializeField] private Button objectiveToggleBadge;      // nếu gán → bấm để bật/tắt panel

        [Header("Timer (tùy chọn)")]
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private UISlider timerSlider;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Layout fallback (khi KHÔNG dùng VerticalLayoutGroup)")]
        [SerializeField] private float rowLineHeight = 24f;
        [SerializeField] private float rowSpacing = 6f;
        [SerializeField] private float listWidth = 520f;
        [SerializeField] private bool autoResizeParentHeight = true;

        // runtime
        private WaveDefinition _cachedWave;
        private UIObjectiveRow[] _rowsPrefab;            // khi dùng prefab
        private TextMeshProUGUI[] _rowsTMPFallback;      // khi fallback TMP

        private void Reset()
        {
            if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();
            if (!glc) glc = FindFirstObjectByType<GameLoopController>();
        }

        private void Awake()
        {
            if (objectiveToggleBadge && objectivePanelRoot)
            {
                objectiveToggleBadge.onClick.RemoveAllListeners();
                objectiveToggleBadge.onClick.AddListener(() =>
                {
                    objectivePanelRoot.SetActive(!objectivePanelRoot.activeSelf);
                });
            }
        }

        private void OnEnable()
        {
            // Thử subscribe sự kiện từ WaveManager nếu có
            // (Bản WaveManager patched đã có OnWaveChanged; nếu bản cũ thì lệnh dưới không gây lỗi)
            try
            {
                if (waveManager != null)
                    waveManager.OnWaveChanged += HandleWaveChangedInternal;
            }
            catch { /* ignore */ }

            BuildForCurrentWave();
            UpdateUI();
        }

        private void OnDisable()
        {
            try
            {
                if (waveManager != null)
                    waveManager.OnWaveChanged -= HandleWaveChangedInternal;
            }
            catch { /* ignore */ }
        }

        private void Update()
        {
            UpdateUI();
        }

        private void HandleWaveChangedInternal(int waveIdx, WaveDefinition wave)
        {
            BuildForCurrentWave();
            UpdateUI();
        }

        // ================== BUILD ==================
        private void BuildForCurrentWave()
        {
            if (!waveManager) waveManager = FindFirstObjectByType<WaveManager>();
            var w = waveManager != null ? waveManager.CurrentWave : null;
            _cachedWave = w;

            // Legacy header
            if (waveText)
            {
                var label = (w != null && !string.IsNullOrEmpty(w.displayName))
                    ? w.displayName
                    : $"Wave {waveManager?.GetCurrentWaveIndex() + 1}";
                waveText.text = label;
            }

            // Objectives
            var defs = waveManager != null ? waveManager.GetObjectives() : null;
            if (defs == null)
            {
                Debug.Log("[UIWaveStub] GetObjectives() trả về null — kiểm tra WaveManager bản mới.");
                return;
            }

            if (objectiveListParent == null)
            {
                Debug.LogWarning("[UIWaveStub] objectiveListParent chưa gán — không có nơi gắn ObjectiveRow.");
                _rowsPrefab = null; _rowsTMPFallback = null;
                return;
            }

            // clear old
            for (int i = objectiveListParent.childCount - 1; i >= 0; i--)
                Destroy(objectiveListParent.GetChild(i).gameObject);

            // không spawn gì nếu không có objective
            if (defs.Length == 0)
            {
                _rowsPrefab = null; _rowsTMPFallback = null;
                Debug.Log("[UIWaveStub] Wave hiện tại không có objective nào (length=0).");
                RebuildTicks(0);
                return;
            }

            // Kiểm tra panel/parent có đang active trong hierarchy không (để user dễ chẩn đoán)
            if (objectivePanelRoot && !objectivePanelRoot.activeInHierarchy)
            {
                Debug.Log("[UIWaveStub] objectivePanelRoot đang tắt (inactiveInHierarchy) — rows vẫn spawn nhưng UI sẽ không thấy cho tới khi bật panel.");
            }

            var vlg = objectiveListParent.GetComponent<VerticalLayoutGroup>();
            bool usingVLG = vlg != null;

            if (objectiveRowPrefab != null)
            {
                // ===== Spawn bằng prefab UIObjectiveRow =====
                _rowsTMPFallback = null;
                _rowsPrefab = new UIObjectiveRow[defs.Length];

                for (int i = 0; i < defs.Length; i++)
                {
                    var row = Instantiate(objectiveRowPrefab, objectiveListParent);
                    if (row == null)
                    {
                        Debug.LogError("[UIWaveStub] Instantiate objectiveRowPrefab trả về null — kiểm tra prefab!");
                        continue;
                    }

                    // BIND data
                    row.Bind(defs[i]);
                    _rowsPrefab[i] = row;

                    // Layout thủ công nếu không dùng VLG
                    if (!usingVLG)
                    {
                        var rt = row.transform as RectTransform;
                        if (rt != null)
                        {
                            rt.anchorMin = new Vector2(0f, 1f);
                            rt.anchorMax = new Vector2(0f, 1f);
                            rt.pivot = new Vector2(0f, 1f);
                            float y = -i * (rowLineHeight + rowSpacing);
                            rt.anchoredPosition = new Vector2(0f, y);
                            rt.sizeDelta = new Vector2(listWidth, rowLineHeight);
                        }
                    }
                }

                // Resize parent nếu không dùng VLG
                if (!usingVLG && autoResizeParentHeight)
                {
                    var parentRT = objectiveListParent as RectTransform;
                    if (parentRT)
                    {
                        float totalH = defs.Length * rowLineHeight + Mathf.Max(0, defs.Length - 1) * rowSpacing;
                        var size = parentRT.sizeDelta; size.y = totalH; parentRT.sizeDelta = size;
                    }
                }
            }
            else
            {
                // ===== Fallback: tạo TMP row đơn giản =====
                _rowsPrefab = null;
                _rowsTMPFallback = new TextMeshProUGUI[defs.Length];

                for (int i = 0; i < defs.Length; i++)
                {
                    TextMeshProUGUI rowTMP;
                    if (rowTemplateTMP)
                    {
                        rowTMP = Instantiate(rowTemplateTMP, objectiveListParent);
                    }
                    else
                    {
                        var go = new GameObject($"ObjectiveRow_{i + 1}", typeof(RectTransform), typeof(TextMeshProUGUI));
                        var rt = go.GetComponent<RectTransform>();
                        rt.SetParent(objectiveListParent, false);
                        rowTMP = go.GetComponent<TextMeshProUGUI>();
                        rowTMP.fontSize = 18;
                        rowTMP.enableWordWrapping = false;

                        if (usingVLG)
                        {
                            rt.anchorMin = new Vector2(0f, 1f);
                            rt.anchorMax = new Vector2(1f, 1f);
                            rt.pivot = new Vector2(0f, 1f);
                            rt.anchoredPosition = Vector2.zero;
                            rt.sizeDelta = new Vector2(0f, rowLineHeight);

                            var le = go.AddComponent<LayoutElement>();
                            le.preferredHeight = rowLineHeight;
                            le.minHeight = rowLineHeight;
                        }
                        else
                        {
                            rt.anchorMin = new Vector2(0f, 1f);
                            rt.anchorMax = new Vector2(0f, 1f);
                            rt.pivot = new Vector2(0f, 1f);
                            float y = -i * (rowLineHeight + rowSpacing);
                            rt.anchoredPosition = new Vector2(0f, y);
                            rt.sizeDelta = new Vector2(listWidth, rowLineHeight);
                        }
                    }

                    rowTMP.text = BuildRowText(defs[i]);
                    _rowsTMPFallback[i] = rowTMP;
                }

                if (!usingVLG && autoResizeParentHeight)
                {
                    var parentRT = objectiveListParent as RectTransform;
                    if (parentRT)
                    {
                        float totalH = defs.Length * rowLineHeight + Mathf.Max(0, defs.Length - 1) * rowSpacing;
                        var size = parentRT.sizeDelta; size.y = totalH; parentRT.sizeDelta = size;
                    }
                }
            }

            // Ticks cho progress bar theo số objectives
            RebuildTicks(defs.Length);
        }

        // ================== UPDATE ==================
        private void UpdateUI()
        {
            var w = waveManager != null ? waveManager.CurrentWave : null;

            // 1) LEGACY: Wave progress theo Score/targetScore (giữ nguyên)
            if (glc != null && waveProgressBar != null && w != null)
            {
                float target = Mathf.Max(1f, w.targetScore); // targetScore để backward compatible
                float progress01 = Mathf.Clamp01(glc.Score / target);
                waveProgressBar.value = progress01;

                if (waveText != null && !string.IsNullOrEmpty(w.displayName))
                {
                    int percent = Mathf.RoundToInt(progress01 * 100f);
                    waveText.text = $"{w.displayName}: {percent}%";
                }
            }

            // 2) OBJECTIVES (optional)
            var defs = waveManager != null ? waveManager.GetObjectives() : null;
            if (defs != null)
            {
                // progress theo objectives
                if (objectiveProgressBar)
                {
                    int total = defs.Length;
                    int completed = 0;
                    for (int i = 0; i < total; i++) if (defs[i].completed) completed++;
                    float denom = Mathf.Max(1, total);
                    objectiveProgressBar.value = completed / denom;
                }

                // update rows
                if (_rowsPrefab != null)
                {
                    int n = Mathf.Min(_rowsPrefab.Length, defs.Length);
                    for (int i = 0; i < n; i++)
                    {
                        if (_rowsPrefab[i]) _rowsPrefab[i].UpdateProgress(defs[i]);
                    }
                }
                else if (_rowsTMPFallback != null)
                {
                    int n = Mathf.Min(_rowsTMPFallback.Length, defs.Length);
                    for (int i = 0; i < n; i++)
                    {
                        if (_rowsTMPFallback[i]) _rowsTMPFallback[i].text = BuildRowText(defs[i]);
                    }
                }
            }

            // 3) TIMER (optional)
            if (timerRoot && timerSlider && timerText && w != null)
            {
                bool on = w.useTimer;
                timerRoot.SetActive(on);
                if (on)
                {
                    float remain = waveManager.TimeLeftSeconds;
                    float limit = Mathf.Max(1f, waveManager.TimeLimitSeconds);
                    timerSlider.value = Mathf.Clamp01(remain / limit);
                    timerText.text = $"{Mathf.CeilToInt(remain)}s";
                }
            }

            // header refresh nếu wave thay đổi
            if (w != _cachedWave && waveText)
            {
                var label = (w != null && !string.IsNullOrEmpty(w.displayName))
                    ? w.displayName
                    : $"Wave {waveManager?.GetCurrentWaveIndex() + 1}";
                waveText.text = label;
                _cachedWave = w;
            }
        }

        // ================== HELPERS ==================
        private string BuildRowText(WaveObjectiveDef def)
        {
            if (def == null) return "-";
            var name = string.IsNullOrWhiteSpace(def.displayName) ? def.kind.ToString() : def.displayName;
            switch (def.kind)
            {
                case ObjectiveKind.CompleteTasks:
                case ObjectiveKind.HireCount:
                case ObjectiveKind.ResolveEvents:
                    return $"{name}: {Mathf.FloorToInt(def.currentValue)} / {Mathf.FloorToInt(def.targetValue)}";
                case ObjectiveKind.KeepStressBelow:
                case ObjectiveKind.KeepEnergyAbove:
                case ObjectiveKind.ReachBudget:
                case ObjectiveKind.ReachScore:
                    return def.completed ? $"{name}: OK" : $"{name}: …";
                default:
                    return def.completed ? $"{name}: OK" : $"{name}: …";
            }
        }

        private void RebuildTicks(int count)
        {
            if (!ticksContainer || !tickPrefab) return;

            for (int i = ticksContainer.childCount - 1; i >= 0; i--)
                Destroy(ticksContainer.GetChild(i).gameObject);

            if (count <= 1) return;

            for (int i = 1; i < count; i++)
            {
                var tick = Instantiate(tickPrefab, ticksContainer);
                var rt = tick.rectTransform;
                rt.anchorMin = new Vector2(i / (float)count, 0.5f);
                rt.anchorMax = rt.anchorMin;
                rt.anchoredPosition = Vector2.zero;
            }
        }
    }
}