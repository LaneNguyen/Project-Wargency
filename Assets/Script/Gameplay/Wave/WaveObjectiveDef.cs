using System;
using UnityEngine;

namespace Wargency.Gameplay
{
    public enum ObjectiveKind
    {
        CompleteTasks = 0,
        HireCount = 1,
        ResolveEvents = 2,
        KeepStressBelow = 3,
        KeepEnergyAbove = 4,
        ReachBudget = 5,
        ReachScore = 6,
        CustomFlag = 99
    }

    // 1 objective = 1 điều kiện để nhận 1 bút chì vàng
    [Serializable]
    public class WaveObjectiveDef
    {
        [Header("Display")]
        public string displayName = "Objective";

        [Header("Rule")]
        public ObjectiveKind kind = ObjectiveKind.CompleteTasks;
        public float targetValue = 1f; // số lượng/giá trị cần đạt

        [Header("Runtime")]
        public float currentValue = 0f; // tiến độ
        public bool completed = false;  // đã hoàn thành chưa
    }
}
