using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor; // để dùng EditorApplication.isPlaying
#endif

namespace Wargency.UI
{
    public class EndgamePanel : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button exitButton;

        [Header("Replay (optional)")]
        [SerializeField] private Button replayWave3Button;
        [SerializeField] private Button replayWave4Button;

        private Action _onExit;
        private Action _onReplayW3;
        private Action _onReplayW4;

        private bool _pausedByMe = false;

        public bool HasReplayButtons =>
            (replayWave3Button != null && replayWave4Button != null);

        private void PauseGame()
        {
            if (Time.timeScale != 0f)
            {
                _pausedByMe = true;
                Time.timeScale = 0f;
            }
        }

        private void ResumeGameIfPausedByMe()
        {
            if (_pausedByMe)
            {
                _pausedByMe = false;
                Time.timeScale = 1f;
            }
        }

        public void Setup(int totalEarned, int totalPossible, Action onExit)
        {
            _onExit = onExit;

            if (resultText != null)
                resultText.SetText($"Bạn đã đạt: {totalEarned} / {totalPossible} Bút Chì Vàng!");

            string ending = ComputeEnding(totalEarned, totalPossible);
            if (titleText != null)
                titleText.SetText(ending);

            PauseGame();

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(() =>
                {
                    ResumeGameIfPausedByMe();
                    _onExit?.Invoke();

                    // ✅ Thoát ứng dụng hẳn
                    Application.Quit();
#if UNITY_EDITOR
                    EditorApplication.isPlaying = false;
#endif
                });
            }

            if (replayWave3Button) replayWave3Button.gameObject.SetActive(false);
            if (replayWave4Button) replayWave4Button.gameObject.SetActive(false);
        }

        public void SetupWithReplay(int totalEarned, int totalPossible,
                                    Action onExit, Action onReplayWave3, Action onReplayWave4)
        {
            _onExit = onExit;
            _onReplayW3 = onReplayWave3;
            _onReplayW4 = onReplayWave4;

            if (resultText != null)
                resultText.SetText($"Bạn đã đạt: {totalEarned} / {totalPossible} Bút Chì Vàng!");

            string ending = ComputeEnding(totalEarned, totalPossible);
            if (titleText != null)
                titleText.SetText(ending);

            PauseGame();

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(() =>
                {
                    ResumeGameIfPausedByMe();
                    _onExit?.Invoke();

                    // ✅ Thoát ứng dụng hẳn
                    Application.Quit();
#if UNITY_EDITOR
                    EditorApplication.isPlaying = false;
#endif
                });
            }

            if (replayWave3Button != null)
            {
                replayWave3Button.gameObject.SetActive(true);
                replayWave3Button.onClick.RemoveAllListeners();
                replayWave3Button.onClick.AddListener(() =>
                {
                    ResumeGameIfPausedByMe();
                    gameObject.SetActive(false);
                    _onReplayW3?.Invoke();
                });
            }

            if (replayWave4Button != null)
            {
                replayWave4Button.gameObject.SetActive(true);
                replayWave4Button.onClick.RemoveAllListeners();
                replayWave4Button.onClick.AddListener(() =>
                {
                    ResumeGameIfPausedByMe();
                    gameObject.SetActive(false);
                    _onReplayW3?.Invoke();
                });
            }
        }

        private string ComputeEnding(int earned, int possible)
        {
            float ratio = (possible > 0) ? (earned / (float)possible) : 0f;
            if (ratio >= 0.8f) return "Chiến Thần Agềncy";
            if (ratio >= 0.5f) return "Cư Dân Agềncy";
            return "Còn Thở Là Còn Gỡ";
        }
    }
}
