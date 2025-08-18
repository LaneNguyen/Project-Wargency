using TMPro;
using UnityEngine;
using Wargency.Gameplay;

namespace Wargency.UI
{
    // UI Stub hiển thị thông tin Wave (Quý) + tiến độ score/target cho M3.
    public class UIWaveStub : MonoBehaviour
    {
        [Header("References quan trọng")]
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private GameLoopController glc;
        [SerializeField] private TextMeshProUGUI waveText;

        private void Reset() // tự gán reference
        {
            if (waveText == null)
            {
                waveText = GetComponentInChildren<TextMeshProUGUI>();
            }
            if(waveManager == null)
                waveManager = FindFirstObjectByType<WaveManager>();
            if(glc == null)
                glc = FindFirstObjectByType<GameLoopController>();
        }


        private void Awake()
        {
            if (waveText == null)
            {
                var go = new GameObject("WaveText", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                waveText = go.AddComponent<TextMeshProUGUI>();
                waveText.color = Color.black; // debug color
                waveText.alignment = TextAlignmentOptions.Midline;
            }
            //GameLoopController.Instance.OnWaveChanged += UpdateWave;
            //UpdateWave(GameLoopController.Instance.Wave);
        }

        private void OnEnable()
        {
            if(glc != null)
            {
                glc.OnWaveChanged += HandleWaveChanged;
                glc.OnScoreChanged += HandleScoreChanged;
            }

            UpdateWaveUI();//lần đầu update cho chắc
        }

        private void OnDisable()
        {
            if (glc != null)
            {
                glc.OnWaveChanged -= HandleWaveChanged;
                glc.OnScoreChanged -= HandleScoreChanged;
            }
        }

        private void HandleScoreChanged(int score)
        {
            UpdateWaveUI();
        }
        private void HandleWaveChanged(int wave)
        {
            UpdateWaveUI();
        }

        private void OnDestroy()
        {
            if (GameLoopController.Instance != null)
                GameLoopController.Instance.OnWaveChanged -= UpdateWave;
        }

        private void UpdateWave(int newWave)
        {
            waveText.text = $"Wave: {newWave}";
        }


        // Cập nhật dòng hiển thị: "Quý X – Tên: score/target (YY%)"
        // Nếu không có Wave current => hiển thị "No wave".
        private void UpdateWaveUI()
        {
            if (waveText == null) return;

            var currentWave = waveManager != null ? waveManager.CurrentWave : null;

            if (glc == null || currentWave == null || currentWave.targetScore <= 0)
            {
                waveText.text = "Wave: trống";
                return;
            }

            // Tính % tiến độ theo Score tuyệt đối 
            float progress01 = Mathf.Clamp01((float)glc.Score / currentWave.targetScore);
            int percent = Mathf.RoundToInt(progress01 * 100f);

            // Hiển thị: Quý + chạy % + tiến độ
            waveText.text = $"{currentWave.displayName}: {glc.Score}/{currentWave.targetScore} ({percent}%)";
        }
    }
}

