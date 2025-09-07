using System.Collections.Generic;
using UnityEngine;
using Wargency.Systems;

namespace Wargency.Gameplay
{
    // Giữ tổng số "bút chì vàng" toàn game.
    // Mỗi wave kết thúc => AddOrReplaceWaveResult(currentWaveIndex, earned, possible)
    // Replay từ wave 3/4 => ClearFromWave(2 hoặc 3)

    public class GameResultController : MonoBehaviour, IResettable
    {
        [Header("Totals")] // 2 con số tổng => earned và possible
        public int totalPencilsEarned = 0;
        public int totalPencilsPossible = 0;

        // Lưu theo wave để có thể replay và ghi đè
        private readonly Dictionary<int, (int earned, int possible)> waveResults = new();

        // reset toàn bộ (nếu cần bắt đầu game mới)
        public void ResetAll()
        {
            totalPencilsEarned = 0;
            totalPencilsPossible = 0;
            waveResults.Clear();
        }

        // API cũ (vẫn giữ để không phá chỗ khác nếu có dùng)
        public void AddWaveResult(int earned, int possible)
        {
            totalPencilsEarned += Mathf.Max(0, earned);
            totalPencilsPossible += Mathf.Max(0, possible);
        }

        // API mới: ghi đè kết quả theo wave (0-based)
        public void AddOrReplaceWaveResult(int waveIndex, int earned, int possible)
        {
            earned = Mathf.Max(0, earned);
            possible = Mathf.Max(0, possible);

            if (waveResults.TryGetValue(waveIndex, out var old))
            {
                totalPencilsEarned -= Mathf.Max(0, old.earned);
                totalPencilsPossible -= Mathf.Max(0, old.possible);
            }

            waveResults[waveIndex] = (earned, possible);
            totalPencilsEarned += earned;
            totalPencilsPossible += possible;
        }

        // Xoá kết quả từ 1 wave trở đi (dùng cho replay 3/4)
        public void ClearFromWave(int startWaveIndexInclusive)
        {
            var toRemove = new List<int>();
            foreach (var kv in waveResults)
            {
                if (kv.Key >= startWaveIndexInclusive)
                {
                    totalPencilsEarned -= Mathf.Max(0, kv.Value.earned);
                    totalPencilsPossible -= Mathf.Max(0, kv.Value.possible);
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var k in toRemove) waveResults.Remove(k);

            if (totalPencilsEarned < 0) totalPencilsEarned = 0;
            if (totalPencilsPossible < 0) totalPencilsPossible = 0;
        }

        public void ResetState()
        {
            ResetAll(); 
        }
    }
}
