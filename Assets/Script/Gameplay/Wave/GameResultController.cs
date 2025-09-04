using UnityEngine;

namespace Wargency.Gameplay
{
    // file này giữ tổng số bút chì vàng trong cả game
    // mỗi wave xong thì gọi AddWaveResult để cộng dồn
    // cuối game EndgamePanel sẽ lấy hai con số này để show

    public class GameResultController : MonoBehaviour
    {
        [Header("Totals")] // 2 con số tổng => earned và possible
        public int totalPencilsEarned = 0;
        public int totalPencilsPossible = 0;

        // reset về 0 => dùng khi restart game mới
        public void ResetAll()
        {
            totalPencilsEarned = 0;
            totalPencilsPossible = 0;
        }

        // cộng kết quả của 1 wave vào tổng
        public void AddWaveResult(int earned, int possible)
        {
            totalPencilsEarned += Mathf.Max(0, earned);
            totalPencilsPossible += Mathf.Max(0, possible);
        }
    }

    // lưu ý nhỏ
    // - reset lại khi bắt đầu game mới
    // - chỉ gọi AddWaveResult 1 lần cho mỗi wave
}
