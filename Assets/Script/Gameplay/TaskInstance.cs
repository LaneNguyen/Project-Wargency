using System;
using UnityEngine;

namespace Wargency.Gameplay
{
    // Trạng thái runtime(dữ liệu khi có chỉ khi chạy game) của 1 TaskDefinition.

    [Serializable] //cho phép serialize class này trong inspector sau này   
    public class TaskInstance
    {
        public enum TaskState { New, InProgess, Completed, Failed } //ko cần none vì New chính là trạng thái khởi đầu

        public TaskDefinition definition;
        [Range(0, 1)] public float progress01;//range để hiện slider dễ theo dõi debug, tính từ 0-100%
        public float timeLeft; //đếm ngược còn bao nhiu time
        public TaskState state; //lưu trạng thái hiện tại của task

        public TaskInstance(TaskDefinition definition) //Hàm tạo task: Gán definition, đặt tiến độ  0, timeLeft = durationSec (có clamp nhỏ để tránh chia 0), trạng thái bắt đầu là New.
        {
            this.definition = definition;
            progress01 = 0f;
            timeLeft = Mathf.Max(0.01f, definition.durationSecond); //đặt giới hạn 0.01f để ko bị lỗi khi nhập sau này, lun giữ tối thiểu
            state = TaskState.New;
        }

        // Tick tiến độ theo deltaTime. Chỉ giảm khi đang progess task. Trả về true nếu vừa Completed/Failed trong tick này.
        public bool Tick(float deltaTime)
        {
            if (state != TaskState.InProgess) return false;

            timeLeft -= Mathf.Max(0f, deltaTime);
            float duration = Mathf.Max(0.001f, definition.durationSecond);
            progress01 = Mathf.Max(1f - (timeLeft / duration));

            if (timeLeft <= 0)
            {
                timeLeft = 0;
                progress01 = 1f;
                state = TaskState.Completed;
                return true;
            }
            return false;
        }

        public void Start()
        {// hàm start để chuyển từ new sang progess đang chạy thôi
            if (state == TaskState.New)
            {
                state = TaskState.InProgess;
            }
        }
        public void Cancel(bool fail = false)
        {
            state = fail ? TaskState.Failed : TaskState.New; //Cho phép hủy và đánh dấu fail cho task. Còn nếu không fail thì thì đưa về new tùy sau này
        }
    }
}
