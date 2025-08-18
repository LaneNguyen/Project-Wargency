using UnityEngine;
using System;
using Wargency.Core;
using System.Runtime.CompilerServices;


namespace Wargency.Gameplay
{
    public enum GameLoopState { None, Init, Run, End }

    //tóm tắt: 
    // Minimal game loop for M1.
    // - Holds Budget / Score / Wave runtime values
    // - Ticks a dummy timer to drive UI stubs
    // - Cơ chế quản lý đơn giản: Init -> Run -> End
    public class GameLoopController : MonoBehaviour
    {
        // Instance tĩnh để có thể gọi từ bất kỳ script nào khác.
        // Chỉ được set trong script này (private set) để tránh thay đổi ngoài ý muốn.
        public static GameLoopController Instance { get; private set; }
        [Header("Config")]
        [SerializeField]
        private GameConfig config;

        [Header("Runtime (Read-Only)")]
        [SerializeField]
        private GameLoopState state = GameLoopState.None;
        [SerializeField]
        private int budget;
        [SerializeField]
        private int score;
        [SerializeField]
        private int wave;

        // Các getter cho UI sử dụng sau. (chỉ đọc, không sửa trực tiếp).
        public GameLoopState State => state;
        public int Budget => budget;
        public int Score => score;
        public int Wave => wave;

        // Đồng hồ nhỏ để tính thời gian giữa các lần cập nhật điểm  (giai đoạn đầu)
        private float tickTimer;

        // Các sự kiện để UI check theo sau này khi cần
        public event Action OnTick;
        public event Action<int> OnWaveChanged; //thông báo đổi wave (có truyền vào int)
        public event Action<int> OnBudgetChanged; //thông báo đổi budget
        public event Action<int> OnScoreChanged; //thông báo đổi điểm
        public event Action<GameLoopState> OnStateChanged; //thông báo đổi trạng thái

        private void Awake()
        {
            if (Instance != null && Instance != this) //kiểm tra nếu có GameLoopControl khác thì xóa, tránh bị overlapse
            {
                Destroy(gameObject);
                return;
            }
            Instance = this; //tự gán vô làm bản chạy duy nhất
        }

        private void Start()
        {
            Boot();
        }

        private void Update()
        {
            if (state != GameLoopState.Run) return;

            //bấm đồng hồ đến giờ
            tickTimer += Time.deltaTime;

            // nếu đồng hồ đạt đến lúc tick, đang để 0.5 phút
            if (tickTimer >= (config != null ? config.tickInterval : 0.5f))
            {
                tickTimer = 0;
                AddScore(1); //mỗi khi tick thì sẽ lên 1 điểm để test UI
                OnTick?.Invoke();
            }
        }

        #region Core Flow
        // Chuẩn bị: lấy số tiền/điểm/wave ban đầu từ GameConfig, rồi chuyển sang trạng thái Run (đang chơi)
        public void Boot()
        {
            // nếu có config thì gán config vào ko thì tự gán
            budget = config ? config.initialBudget : 1000;
            score = config ? config.initialScore : 0;
            wave = config ? config.startWave : 1;

            tickTimer = 0f;
            SetState(GameLoopState.Init); // Bước “chuẩn bị”
            SetState(GameLoopState.Run);  // Bắt đầu “giờ học”
        }

        public void End()
        {
            SetState(GameLoopState.End);
            Debug.Log("Hết game - place holder trước");
        }


        // Chuyển trạng thái một cách an toàn + thông báo cho ai cần
        private void SetState(GameLoopState next)
        {
            if (state == next) return;//nếu sẵn rồi thì khỏi set
            state = next;
            OnStateChanged?.Invoke(state);
        }
        #endregion

        #region Public API
        //Hàm qua màn
        public void SetWave(int value)
        {
            int limitwave = Mathf.Max(1, value); //Số màn set nhỏ hơn 1 => lấy 1
            if (wave == limitwave) return;
            wave = limitwave;
            OnWaveChanged?.Invoke(wave);
        }
        public void NextWave()
        {
            SetWave(wave + 1);
            tickTimer = 0f; //reset bộ đếm khi sang wave mới
        }
        //Hàm cộng budget
        public void AddBudget(int amount)
        {
            budget += amount;
            OnBudgetChanged?.Invoke(budget);
        }
        //Hàm thử xài tiền coi đủ hay không
        public bool TrySpendBudget(int amount) //bỏ số tiền và coi thử đủ tiền xài không
        {
            int cost = Mathf.Abs(amount); //đảm bảo là số dương
            if (budget < cost) return false; //hổng đủ tiền
            budget -= cost;
            OnBudgetChanged?.Invoke(budget);
            return true;
        }

        //hàm cộng điểm
        public void AddScore(int amount)
        {
            score += amount;
            OnScoreChanged?.Invoke(score);
        }

        //hàm reset màn chơi
        public void ResetRun()
        {//reset lại chỉ số
            budget = config ? config.initialBudget : 1000;
            wave = config ? config.startWave : 1;
            score = config ? config.initialScore : 0;
            tickTimer = 0f;//reset bộ đếm
            SetState(GameLoopState.Run);
            OnBudgetChanged?.Invoke(budget);
            OnWaveChanged?.Invoke(wave);
            OnScoreChanged?.Invoke(Score);
        }
        #endregion
    }
}
