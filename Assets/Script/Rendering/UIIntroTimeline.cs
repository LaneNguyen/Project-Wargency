using UnityEngine;
using UnityEngine.Playables;

namespace Wargency.UI
{
    [RequireComponent(typeof(PlayableDirector))]
    public class UIIntroTimeline : MonoBehaviour
    {
        public enum IntroType { Startup, Wave }

        [Header("Target")]
        [Tooltip("Nếu null sẽ tự FindObjectOfType<UIManager>()")]
        [SerializeField] private UIManager uiManager;

        [Header("Intro Kind")]
        [SerializeField] private IntroType introType = IntroType.Startup;
        [Tooltip("Chỉ dùng khi IntroType = Wave")]
        [SerializeField] private int waveIndex = 1;

        private PlayableDirector director;

        private void Awake()
        {
            director = GetComponent<PlayableDirector>();
            if (!uiManager) uiManager = FindObjectOfType<UIManager>();
        }

        private void OnEnable()
        {
            if (!director) return;

            // Timeline chạy khi game đang pause (TimeScale = 0)
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

            // Lắng nghe khi Timeline dừng
            director.stopped -= OnTimelineStopped;
            director.stopped += OnTimelineStopped;

            // Play từ đầu
            director.time = 0;
            director.Play();
        }

        private void OnDisable()
        {
            if (director) director.stopped -= OnTimelineStopped;
        }

        private void OnTimelineStopped(PlayableDirector d)
        {
            // Tắt intro + resume game tương ứng
            if (uiManager != null)
            {
                if (introType == IntroType.Startup)
                {
                    uiManager.CloseStartupIntro();
                }
                else
                {
                    uiManager.CloseWaveIntro(waveIndex);
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // Gọi hàm này từ nút "Skip" nếu muốn cho bỏ qua
        public void SkipIntro()
        {
            if (!director) { OnTimelineStopped(null); return; }
            director.time = director.duration;
            director.Stop(); // sẽ kích hoạt OnTimelineStopped
        }
    }
}
