using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wargency.Gameplay;

// bản stub để show điểm số tạm
// nếu chưa gắn text thì tự tạo cho khỏi quên
// nghe ngóng GameLoopController đổi điểm là cập nhật ngay
// UI => Manager => Gameplay nối nhau bằng event score

namespace Wargency.UI
{
    public class UIScoreStub : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;

        private void Start()
        {
            if (scoreText == null)
            {
                GameObject go = new GameObject("ScoreText");
                go.transform.SetParent(this.transform);
                scoreText = go.AddComponent<TextMeshProUGUI>();
                //scoreText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                scoreText.color = Color.black;
            }

            GameLoopController.Instance.OnScoreChanged += UpdateScore;
            UpdateScore(GameLoopController.Instance.Score);
        }

        private void OnDestroy()
        {
            if (GameLoopController.Instance != null)
                GameLoopController.Instance.OnScoreChanged -= UpdateScore;
        }

        private void UpdateScore(int newScore)
        {
            scoreText.text = $"{newScore}";
        }
    }
}

