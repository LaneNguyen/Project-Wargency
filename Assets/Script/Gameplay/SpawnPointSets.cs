using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // Tập các vị trí spawn (world-space). Hai hàm xài:
    // - GetNextRoundPoint(): lấy vị trí theo vòng tròn (A=>B=>C=>...=>A)
    // - GetRandom(): lấy vị trí ngẫu nhiên

    [CreateAssetMenu(
        fileName = "SpawnPointSet", menuName = "Wargency/Spawning/Spawn Point Set")]
    public class SpawnPointSets : ScriptableObject
    {
        [Tooltip("Các vị trí WORLD-SPACE để spawn (theo grid)")]
        public List<Vector3> worldPositions = new List<Vector3>();

        [SerializeField, Tooltip("Chỉ số nội bộ phục vụ cho spawn theo hình tròn (không chỉnh tay)")]
        private int lastIndex = -1;

        public bool HasAny => worldPositions != null && worldPositions.Count > 0;

        // Lấy vị trí tiếp theo theo vòng tròn. Nếu danh sách rỗng → trả về Vector3.zero.
        public Vector3 GetNextRoundPoint()
        {
            if (!HasAny) return Vector3.zero;
            lastIndex = (lastIndex + 1) % worldPositions.Count;
            return worldPositions[lastIndex];
        }

        // Lấy vị trí ngẫu nhiên Nếu rỗng → Vector3.zero.
        public Vector3 GetRandom()
        {
            if (!HasAny) return Vector3.zero;
            int idx = Random.Range(0, worldPositions.Count);
            return worldPositions[idx];
        }

        // Reset chu kỳ round-robin (ví dụ khi sang Wave mới)
        public void ResetCycle() => lastIndex = -1;
    }
}