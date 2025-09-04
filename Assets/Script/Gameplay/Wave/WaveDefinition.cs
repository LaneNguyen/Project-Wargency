using UnityEngine;

namespace Wargency.Gameplay
{
    // dữ liệu 1 wave, chỉnh trong editor
    [CreateAssetMenu(menuName = "Wargency/Wave Definition", fileName = "WaveDefinition")]
    public class WaveDefinition : ScriptableObject
    {
        [Header("Meta")]
        public string displayName;
        [TextArea(2, 6)] public string waveDescription; // mô tả hiện khi kết thúc wave
        [TextArea(1, 4)] public string endSummaryHint;  // hint nhỏ cuối wave

        [Header("Timer")]
        public bool useTimer = false;
        [Min(1f)] public float timeLimitSeconds = 60f;

        [Header("Objectives (1 objective = 1 bút chì vàng)")]
        public WaveObjectiveDef[] objectives;

        [Header("Legacy Progress (optional)")]
        public int targetScore = 100;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (timeLimitSeconds < 1f) timeLimitSeconds = 1f;
        }
#endif
    }
}
