using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    [CreateAssetMenu(fileName = "SpawnPointSet", menuName = "Wargency/Spawning/Spawn Point Set")]
    public class SpawnPointSets : ScriptableObject
    {
        public List<Vector3> worldPositions = new List<Vector3>();
        [SerializeField] private int lastIndex = -1;
        public bool HasAny => worldPositions != null && worldPositions.Count > 0;

        public Vector3 GetNextRoundPoint()
        {
            if (!HasAny) return Vector3.zero;
            lastIndex = (lastIndex + 1) % worldPositions.Count;
            return worldPositions[lastIndex];
        }

        public Vector3 GetRandom()
        {
            if (!HasAny) return Vector3.zero;
            int idx = Random.Range(0, worldPositions.Count);
            return worldPositions[idx];
        }

        public void ResetCycle() => lastIndex = -1;
    }
}
