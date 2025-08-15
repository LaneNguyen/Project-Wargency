using UnityEngine;

namespace Wargency.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Scriptable Objects/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Render/Grid")]
        [Tooltip("Pixels Per Unit – import setting luôn giữ ở mức chung")] public int pixelsPerUnit = 32;

        [Header("Gameplay Defaults")]
        [Tooltip("Initial agency budget - M1")] public int initialBudget = 1000;
        [Tooltip("Initial score - M1")] public int initialScore = 0;
        [Tooltip("Initial wave index (1-based)")] public int startWave = 1;

        [Header("Player")]
        [Tooltip("tốc độ chạy của nhân vật (units/sec)")] public float playerMoveSpeed = 3.5f;

        [Header("Loop")]
        [Tooltip("Vòng lặp test M1 UI các thứ (seconds)")] public float tickInterval = 1f;
    }
}
