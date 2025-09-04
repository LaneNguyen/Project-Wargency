using UnityEngine;
using TMPro;

///// Update 0309 : bỏ cái này để xài sau này vì hiện tại ko bít để lèm gì =)))))
// hiển thị điểm agency score cho người chơi xem
// nghe ngóng manager score đổi số thì update label ngay
// số điểm được format có dấu phẩy cho dễ đọc
// UI => Manager => Gameplay truyền số qua lại

namespace Wargency.UI
{
    // Meter/label hiển thị điểm Agency Score.
    // - Lắng nghe AgencyScoreController.OnScoreChanged để cập nhật.
    [DisallowMultipleComponent]
    public class UIAgencyScoreMeter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;

        private void OnEnable()
        {
            if (Wargency.Gameplay.AgencyScoreController.I != null)
            {
                Wargency.Gameplay.AgencyScoreController.I.OnScoreChanged += Refresh;
                Refresh(Wargency.Gameplay.AgencyScoreController.I.Score);
            }
        }

        private void OnDisable()
        {
            if (Wargency.Gameplay.AgencyScoreController.I != null)
                Wargency.Gameplay.AgencyScoreController.I.OnScoreChanged -= Refresh;
        }

        private void Refresh(int newScore)
        {
            if (scoreText != null)
                scoreText.text = newScore.ToString("N0");
        }
    }
}