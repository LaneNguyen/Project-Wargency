using System;
using System.Linq; // cung cấp hàm mở rộng như Min(), Max(), Where()...
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wargency.Gameplay
{
    // Quản lý việc tạo ngẫu nhiên Event theo khoảng thời gian. 
    // Khi kích hoạt: áp hiệu ứng vào GameLoopController và bắn sự kiện cho UI.
    public class EventManager : MonoBehaviour
    {
        [Header("Config")]
        public EventDefinition[] availableEvents; //danh sách các Event có thể xảy ra

        [Header("Refs")]
        public GameLoopController gameLoopController; //tham chiếu để áp tác động budget/score

        // Báo khi event xuất hiện. UI nghe để popup.
        public event Action<EventDefinition> OnEventTrigger;

        float timer; //đếm ngược tới lần event kế tiếp
        float min; // khoảng thời gian nhỏ nhất
        float max; // khoảng thời gian lớn nhất

        void Start()
        {
            if (availableEvents == null || availableEvents.Length == 0)
            {
                Debug.LogWarning("[EventManager] Đang ko có sự kiện gì cả ");
                enabled = false; // tắt script nếu không có Event nào
                return;
            }

            //Gom khoảng thời gian từ list Event (lấy min nhỏ nhất, max lớn nhất)
            min = Mathf.Max(0.1f, availableEvents.Min(e => e != null ? e.minIntervalSec : 999f)); //min = max(0.1, giá trị min nhỏ nhất trong tất cả event)
            max = Mathf.Max(min + 0.1f, availableEvents.Max(e => e != null ? e.maxIntervalSec : 0.1f)); // max = max(_min + 0.1, giá trị max lớn nhất trong tất cả event)

            ResetTimer(); //set timer ban đầu
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                TriggerRandomEvent(); // tới hạn → gọi event
                ResetTimer(); // đặt lại timer cho lần tiếp theo
            }
        }

        // Chọn 1 event ngẫu nhiên, áp hiệu ứng và bắn OnEventTriggered.
        public void TriggerRandomEvent()
        {  // lọc bỏ null, tạo pool các Event hợp lệ
            var pool = availableEvents?.Where(e => e != null).ToArray();
            if (pool == null || pool.Length == 0) return;

            // chọn ngẫu nhiên 1 event từ pool
            var chosen = pool[Random.Range(0, pool.Length)];

            // áp hiệu ứng lên GameLoopController (budget/score)
            if (gameLoopController != null && chosen != null)
            {
                if (chosen.budgetChange != 0)
                    gameLoopController.AddBudget(chosen.budgetChange);
                if (chosen.scoreChange != 0)
                    gameLoopController.AddScore(chosen.scoreChange);
            }
            else
            {
                Debug.LogWarning("[EventManager] Ko tìm thấy gameLoopController hoặc là ko tìm thấy event được chọn");
            }
            // Bắn event cho UI
            OnEventTrigger?.Invoke(chosen);
        }

        // Đặt lại timer với giá trị ngẫu nhiên trong khoảng [_min, _max].
        void ResetTimer()
        {
            timer = Random.Range(min, max);
        }
    }
}