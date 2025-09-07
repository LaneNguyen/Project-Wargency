// Assets/Script/Systems/ReturnToTitleService.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wargency.Systems
{
    // Gắn script này vào 1 GameObject "Bootstrap" trong scene.
    // Kéo thả reference trong Inspector.
    public class ReturnToTitleService : MonoBehaviour
    {
        [Header("Title Refs")]
        [SerializeField] private Camera titleCamera;
        [SerializeField] private Canvas titleCanvas; // Title Screen Canvas

        [Header("Gameplay Refs")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Canvas gameplayCanvas; // MainCanvas / UIMainGame
        [SerializeField] private Transform gameplayRoot;
        // GameplayRoot là cha của toàn bộ đối tượng spawn runtime (agents, tasks, VFX, event objects...).
        // Nếu chưa có, tạo 1 empty GameObject "GameplayRoot" rồi cho tất cả spawn runtime vào đây.

        [Header("Managers để reset (tùy chọn)")]
        [SerializeField] private MonoBehaviour[] managerResettables;
        // Gắn các manager có ResetState() vào đây (TaskManager, EventManager, WaveManager, BudgetController...)

        [Header("UI Focus (tùy chọn)")]
        [SerializeField] private GameObject titleDefaultSelected;
        // Button đầu tiên trên Title để focus sau khi quay về

        public void ReturnToTitleAndWipe()
        {
            // 1) Tháo pause nếu có
            if (Time.timeScale == 0f) Time.timeScale = 1f;

            // 2) Tắt gameplay UI + camera
            if (gameplayCanvas) gameplayCanvas.enabled = false;
            if (gameplayCamera) gameplayCamera.enabled = false;

            // 3) Bật title UI + camera
            if (titleCanvas) titleCanvas.enabled = true;
            if (titleCamera) titleCamera.enabled = true;

            // 4) Reset managers (nếu có)
            foreach (var m in managerResettables)
            {
                if (m == null) continue;
                var resettable = m as IResettable;
                if (resettable != null)
                {
                    // Cho phép mỗi manager tự dọn dữ liệu của nó (list, dict, pool, coroutine…)
                    resettable.ResetState();
                }
                else
                {
                    // Nếu manager chưa hỗ trợ IResettable, có thể tự viết hàm public Clear() và gọi bằng reflection,
                    // hoặc sau này chuyển dần các manager sang IResettable để đồng bộ.
                }
            }

            // 5) Xóa sạch toàn bộ runtime spawn dưới gameplayRoot
            if (gameplayRoot != null)
            {
                // Xóa lần lượt các con cho an toàn
                var toDelete = new List<Transform>();
                foreach (Transform child in gameplayRoot) toDelete.Add(child);
                for (int i = 0; i < toDelete.Count; i++)
                {
                    if (toDelete[i] != null)
                        Destroy(toDelete[i].gameObject);
                }
            }
            else
            {
                // Fallback: nếu chưa setup gameplayRoot, có thể xóa theo tag "Gameplay"
                // foreach (var go in GameObject.FindGameObjectsWithTag("Gameplay"))
                //     Destroy(go);
            }

            // 6) Dừng âm thanh/nhạc nếu cần (tùy hệ thống audio của em)
            // var audio = FindObjectOfType<AudioManager>();
            // if (audio) audio.StopAll();

            // 7) Clear selection + focus nút mặc định ở Title
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (titleDefaultSelected) EventSystem.current.SetSelectedGameObject(titleDefaultSelected);
            }

            // 8) (Tùy chọn) Gọi TitleScreenController để đảm bảo Title UI active (nếu dùng controller riêng)
            var titleCtrl = FindObjectOfType<TitleScreenController>();
            if (titleCtrl) titleCtrl.SetTitleActive(true, instant: true);
        }
    }

    // Interface đơn giản để các manager implement
    public interface IResettable
    {
        void ResetState();
    }
}
