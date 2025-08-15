using UnityEngine;
using System;
using System.Collections.Generic;

namespace Wargency.Gameplay
{
    // Quản lý spawn / tick / complete các TaskInstance.
    // Khi task Completed -> cộng Budget/Score qua GameLoopController.
    public class TaskManager : MonoBehaviour
    {
        [Header("Definitions (tạo trong editor)")]
        [Tooltip("Danh sách các TaskDefinition được chuẩn bị sẵn trong Editor. Kéo các Task Definition vào để spawn từ UI stub")]
        public TaskDefinition[] availableDefinitions;

        [Header("Ticking")]
        [Tooltip("tickInterval <= 0: cập nhật mỗi frame (60 lần/giây ở 60 FPS). tickInterval > 0: cập nhật theo khoảng thời gian cố định, ví dụ tickInterval = 0.5 nghĩa là cập nhật 2 lần/giây.")]
        public float tickInterval = 0f; //ví dụ tickInterval = 0.5 nghĩa là cập nhật 2 lần/giây.

        [Header("Refs GameLoopControl")]
        [Tooltip("Để kéo ref Object có script GameLoopController vào")]
        public MonoBehaviour gameLoopController;      
        
        void Start()
        {

        }
        void Update()
        {

        }
    }
}
