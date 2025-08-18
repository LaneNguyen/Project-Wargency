using UnityEngine;

namespace Wargency.Gameplay
{
    // Quản lý trình tự Wave: start → check target → end → next.
    // GameLoopController (GLC) có:
    // - public int tScore
    // - public void AddScore(int)
    // - public void AddBudget(int)
    // - public void SetWave(int)  // set index hiện tại (1-based)
    public class WaveManager : MonoBehaviour
    {
        [Header("Ref cần tham chiếu")]
        [SerializeField] private GameLoopController glc;
        [SerializeField] private TaskManager taskManager; //optional: để log/scale

        [Header("Waves")]
        [SerializeField] private WaveDefinition[] waves;

        [Header("Difficulty Scaling")]
        [Tooltip("Mỗi khi sang Wave mới, tăng stress + giá trị này.")]
        [SerializeField] private int stressIncreasePerWave = 5;

        // Chỉ số vị trí Wave hiện tại trong mảng `waves`. Mặc định = -1 nghĩa là "chưa bắt đầu wave nào"
        // Khi StartWave(0) chạy, currentIndex sẽ = 0 (tương ứng Quý 1)
        private int currentIndex = -1;

        //Trả về Wave (Quý) đang chạy dựa trên currentIndex.
        //Nếu currentIndex nằm ngoài khoảng hợp lệ (chưa start || đã vượt quá tổng wave) thì trả về null để các nơi khác biết là hok có wave active.
        public WaveDefinition CurrentWave => (currentIndex >= 0 && currentIndex < waves.Length) ? waves[currentIndex] : null;


        // Reset gọi khi Add component hoặc nhấn Reset trên Inspector. Để đỡ quên sau này chứ ko gì hết
        private void Reset()
        {
            // Nếu chưa kéo GLC vào ref, cố gắng tìm trong Scene.
            if (!glc) glc = FindFirstObjectByType<GameLoopController>();

            // TaskManager optional, nếu có thì lấy để phục vụ scaling/log.
            if (!taskManager) taskManager = FindFirstObjectByType<TaskManager>();
        }

        // Khi game chạy: kiểm tra đã gán danh sách Waves chưa.
        // Nếu chưa: cảnh báo và dừng.
        // Nếu có: tự động bắt đầu Wave đầu tiên (index 0 = Quý 1).
        private void Start()
        {
            // Không có dữ liệu wave → không thể vận hành hệ thống progression.
            if (waves == null || waves.Length == 0)
            {
                Debug.LogWarning("[WaveManager] Chưa gán Waves.");
                return;
            }
            // Bắt đầu từ wave đầu tiên để Acceptance Test auto pass.
            StartWave(0);
        }

        // Mỗi frame: kiểm tra xem đã đạt mốc điểm mục tiêu của Wave hiện tại chưa.
        // Dùng điểm absolute của GameLoopControler: CurrentScore.
        private void Update()
        {
            // Không có wave đang chạy hoặc chưa có GLC để đọc điểm => không làm gì.
            if (CurrentWave == null || glc == null) return;

            // So sánh điểm hiện tại với mốc cần đạt của wave
            // Note: đang dùng điểm tuyệt đối (không trừ điểm lúc vào wave)
            if (glc.Score >= CurrentWave.targetScore)
            {
                // Đã đạt mục tiêu => hoàn thành wave hiện tại và chuyển tiếp.
                CompleteCurrentWave();
            }
        }

        // Bắt đầu 1 Wave theo chỉ số trong mảng `waves`.
        // Quy ước trước logic:
        // - index 0 = Wave/Quý 1, index 1 = Quý 2, ...
        // - Nếu index không hợp lệ => coi như đã hết nội dung (có thể kết thúc game/đi Endless).
        public void StartWave(int index)
        {
            // Nếu index âm hoặc vượt quá số phần tử → kết thúc chuỗi wave.
            if (index < 0 || index >= waves.Length)
            {
                Debug.Log("[WaveManager] Hết wave, game session có thể kết thúc hoặc vào vô hạn thành");
                return;
            }
            // Lưu lại chỉ số wave hiện hành
            currentIndex = index;
            var w = waves[currentIndex];

            // Log để kiểm thử debug
            Debug.Log($"[WaveManager] Start Wave: {w.displayName} (TargetScore={w.targetScore})");

            // Báo cho GLC biết "đang ở Wave thứ mấy" theo cách 1-based (1,2,3,...) để UI/logic khác dễ hiển thị.
            glc?.SetWave(currentIndex + 1);

            // Tăng độ khó khi bước sang Wave > 1 (tức index >= 1). Wave 1 là start nên không tăng ngay khi vào
            if (currentIndex > 0)
            {
                ApplyDifficultyScaling(currentIndex);
            }
        }

        // Hoàn tất Wave hiện tại: thưởng (nếu có) và tự chuyển sang Wave kế tiếp.
        public void CompleteCurrentWave()
        {
            //kiểm tra current wave cái đã
            if (CurrentWave == null) return;

            //1. Thông báo log hết wave
            Debug.Log($"[WaveManager] {CurrentWave.displayName} Complete");
            //2. Add giải thưởng nè
            if (CurrentWave.rewardBudget > 0)
            {
                glc?.AddBudget(CurrentWave.rewardBudget);
                Debug.Log($"[WaveManager] Reward: +{CurrentWave.rewardBudget} Budget");
            }
            //3. Chuyển sang tiếp theo
            StartWave(currentIndex + 1);

        }

        public void ApplyDifficultyScaling(int waveIndex)
        {
            int TotalIncrease = stressIncreasePerWave; // mặc định là 5 lun đi

            if (taskManager != null)
            {
                taskManager.IncreaseStressCost(TotalIncrease);
                Debug.Log($"[WaveManager] Difficulty scaling: stressCost += {TotalIncrease} (Wave {waveIndex + 1})");
            }
            else
            {
                Debug.Log($"[WaveManager] Difficulty scaling (no TaskManager): stressCost += {TotalIncrease} (Wave {waveIndex + 1})");
            }
        }
    }

}

