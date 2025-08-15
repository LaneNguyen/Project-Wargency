using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

namespace Wargency.UI
{
    /// UI Stub để hiển thị Wave hiện tại tạm thời.
    public class UIWaveStub : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI waveText;

        private void Start()
        {
            if (waveText == null)
            {
                GameObject go = new GameObject("WaveText");
                go.transform.SetParent(this.transform);
                waveText = go.AddComponent<TextMeshProUGUI>();
                //waveText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                waveText.color = Color.black;
            }

            GameLoopController.Instance.OnWaveChanged += UpdateWave;

            UpdateWave(GameLoopController.Instance.Wave);
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
    }
}
