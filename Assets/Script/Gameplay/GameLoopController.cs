using UnityEngine;
using System;
using Wargency.Core;

namespace Wargency.Gameplay
{
    // cái đồng hồ chạy game: giữ state, ngân sách, điểm, wave
    // có tick để test, ai cần nghe gì thì có event hết
    public enum GameLoopState { None, Init, Run, End }

    public class GameLoopController : MonoBehaviour
    {
        public static GameLoopController Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private GameConfig config; // để lấy mấy con số khởi tạo

        [Header("Runtime (Read-Only)")]
        [SerializeField] private GameLoopState state = GameLoopState.None;
        [SerializeField] private int budget;
        [SerializeField] private int score;
        [SerializeField] private int wave;

        // team stats cho objective kiểu giữ stress/energy…
        [Header("Team Stats (Objectives)")]
        [SerializeField] private float teamStressValue = 0f;
        [SerializeField] private float teamEnergyValue = 100f;
        public float TeamStressValue => teamStressValue;
        public float TeamEnergyValue => teamEnergyValue;

        // get nhanh cho mấy chỗ khác
        public GameLoopState State => state;
        public int Budget => budget;
        public int CurrentBudget => budget; // chỗ khác đang gọi tên này nên giữ
        public int Score => score;
        public int Wave => wave;

        // tick đơn giản, để test
        private float tickTimer;

        [Header("Debug")]
        [Tooltip("bật lên thì mỗi tick +1 điểm cho vui, giống bản M1")]
        [SerializeField] private bool autoDebugScoreTick = false;

        // sự kiện cho ai quan tâm
        public event Action OnTick;
        public event Action<int> OnWaveChanged;
        public event Action<int> OnBudgetChanged;
        public event Action<int> OnScoreChanged;
        public event Action<GameLoopState> OnStateChanged;
        public event Action<float, float> OnTeamStatsChanged; // stress, energy

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            Boot(); // khởi động cái đã
        }

        private void Update()
        {
            if (state != GameLoopState.Run) return;

            tickTimer += Time.deltaTime;

            // chỉ cộng điểm nếu bật chế độ debug tick
            if (autoDebugScoreTick && tickTimer >= (config != null ? config.tickInterval : 0.5f))
            {
                tickTimer = 0;
                AddScore(1);
                OnTick?.Invoke();
            }
        }

        public void Boot()
        {
            budget = config ? config.initialBudget : 1000; // không có config thì xài số tạm
            score = config ? config.initialScore : 0;
            wave = config ? config.startWave : 1;

            tickTimer = 0f;
            SetState(GameLoopState.Init);
            SetState(GameLoopState.Run);
        }

        public void End()
        {
            SetState(GameLoopState.End);
            Debug.Log("Game over thử nghiệm"); // placeholder
        }

        private void SetState(GameLoopState next)
        {
            if (state == next) return; // khỏi báo lại cho ồn
            state = next;
            OnStateChanged?.Invoke(state);
        }

        public void SetWave(int value)
        {
            int v = Mathf.Max(1, value); // wave dưới 1 nhìn kỳ
            if (wave == v) return;
            wave = v;
            OnWaveChanged?.Invoke(wave);
        }

        public void NextWave()
        {
            SetWave(wave + 1);
            tickTimer = 0f; // reset nhịp cho gọn
        }

        public void AddBudget(int amount)
        {
            budget += amount;
            OnBudgetChanged?.Invoke(budget);
        }

        public bool TrySpendBudget(int amount)
        {
            int cost = Mathf.Abs(amount); // lỡ đưa số âm thì vẫn trừ dương
            if (budget < cost) return false;
            budget -= cost;
            OnBudgetChanged?.Invoke(budget);
            return true;
        }

        public void AddScore(int amount)
        {
            score += amount;
            OnScoreChanged?.Invoke(score);
        }

        public void ResetRun()
        {
            budget = config ? config.initialBudget : 1000;
            wave = config ? config.startWave : 1;
            score = config ? config.initialScore : 0;
            tickTimer = 0f;
            SetState(GameLoopState.Run);
            OnBudgetChanged?.Invoke(budget);
            OnWaveChanged?.Invoke(wave);
            OnScoreChanged?.Invoke(score);
        }

        public void SetTeamStats(float stress, float energy)
        {
            teamStressValue = stress;
            teamEnergyValue = energy;
            OnTeamStatsChanged?.Invoke(teamStressValue, teamEnergyValue);
        }
    }
}
