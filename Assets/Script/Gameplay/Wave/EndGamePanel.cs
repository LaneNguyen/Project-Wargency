using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wargency.UI
{
    // panel này để hiện tổng kết cuối game
    // show tiêu đề ending + bao nhiêu bút chì vàng kiếm được
    // có nút thoát để quay về ngoài game
    // giữ nguyên api cũ để không phá prefab hay scene

    public class EndgamePanel : MonoBehaviour
    {
        [Header("UI Refs")] // tham chiếu tới mấy cái text và button trong inspector => kéo đúng mới chạy
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button exitButton;

        private Action _onExit; // callback khi bấm quit => cho game biết thoát

        // set dữ liệu tổng kết và gắn sự kiện nút exit
        public void Setup(int totalEarned, int totalPossible, Action onExit)
        {
            _onExit = onExit;

            // update kết quả => text
            if (resultText != null)
            {
                resultText.SetText($"Bạn đã đạt: {totalEarned} / {totalPossible} Bút Chì Vàng!");
            }

            // chọn ending dựa vào tỉ lệ hoàn thành => hiển thị
            string ending = ComputeEnding(totalEarned, totalPossible);
            if (titleText != null)
            {
                titleText.SetText(ending);
            }

            // nút exit sẽ gọi callback ngoài
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(() => _onExit?.Invoke());
            }
        }

        // helper nhỏ => quyết định text ending
        private string ComputeEnding(int earned, int possible)
        {
            float ratio = (possible > 0) ? (earned / (float)possible) : 0f;

            if (ratio >= 0.8f) return "Chiến Thần Agềncy";
            if (ratio >= 0.5f) return "Cư Dân Agềncy";
            return "Còn Thở Là Còn Gỡ";
        }
    }

    // chú ý khi xài file này
    // - phải kéo đúng ref trong inspector
    // - nhớ gọi Setup sau khi có kết quả
    // - onExit cho biết thoát ra đâu
}
