using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // file này lo spawn agent test theo wave => debug cho lẹ
    // - Direct Spawn: bỏ qua ngân sách
    // - Hire Spawn: đi qua HiringService => trừ tiền thật
    // - Có auto-wire với WaveManager để mỗi wave mới thì reset điểm spawn, optional spawn roster 1 lần thôi

    public class CharacterWaveSpawner : MonoBehaviour
    {
        [Header("Direct Spawn (bỏ qua ngân sách)")]
        [SerializeField] private CharacterAgent agentPrefab;
        [SerializeField] private CharacterDefinition directSpawnDefinition;
        [SerializeField] private Vector3 directSpawnOffset = Vector3.zero;

        [Header("Hire Spawn (qua HiringService)")]
        [SerializeField] private CharacterHiringService hiringService;
        [SerializeField] private CharacterDefinition hireDefinition;
        [SerializeField] private Vector3 hireSpawnOffset = Vector3.zero;

        [Header("Wave Roster (debug)")]
        [SerializeField, Tooltip("Danh sách spawn 1 lần khi vào wave mới (nếu bật auto)")]
        private List<CharacterDefinition> waveRoster = new();

        [Header("Điểm Spawn")]
        [SerializeField, Tooltip("Tập điểm spawn => round-robin; trống thì lấy transform.position")]
        private SpawnPointSets spawnPointSet;

        [Header("Parenting Object")]
        [SerializeField] private Transform agentsParent;

        [Header("Difficulty/Task refs (optional)")]
        [SerializeField] private UnityEngine.Object difficultyProviderObj;
        [SerializeField] private TaskManager taskManager;
        private IDifficultyProvider difficultyProvider;

        [Header("Wave Hook (optional)")]
        [SerializeField] private WaveManager waveManager;   // => nếu không kéo, sẽ tự Find
        [SerializeField] private bool autoSpawnRosterOnWaveStart = false; // => spawn roster 1 lần mỗi wave
        private int _lastWaveIndex = -1;                    // => nhớ wave cũ để tránh trùng
        private bool _spawnedThisWave = false;              // => flag 1 lần/ wave

        [Header("Hotkeys")]
        [SerializeField] private KeyCode directSpawnKey = KeyCode.F6;
        [SerializeField] private KeyCode hireKey = KeyCode.F7;

        private void Awake()
        {
            if (difficultyProviderObj is IDifficultyProvider dp) difficultyProvider = dp;
            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
            if (!hiringService) hiringService = FindAnyObjectByType<CharacterHiringService>();
            if (!waveManager) waveManager = FindAnyObjectByType<WaveManager>();
        }

        private void OnEnable()
        {
            // nghe thay đổi wave => reset cycle + spawn roster nếu bật auto
            if (waveManager != null)
                waveManager.OnWaveChanged += HandleWaveChanged;
        }

        private void OnDisable()
        {
            if (waveManager != null)
                waveManager.OnWaveChanged -= HandleWaveChanged;
        }

        private void HandleWaveChanged(int index, WaveDefinition w)
        {
            OnBeginWave();                   // => reset thứ tự điểm spawn
            _spawnedThisWave = false;        // => chuẩn bị cho batch spawn 1 lần

            // tránh spawn trùng trong cùng wave
            if (autoSpawnRosterOnWaveStart && !_spawnedThisWave)
            {
                SpawnWaveFromRoster();
                _spawnedThisWave = true;
                _lastWaveIndex = index;
            }
        }

        // gọi lúc vào wave mới
        public void OnBeginWave()
        {
            if (spawnPointSet != null) spawnPointSet.ResetCycle();
        }

        [ContextMenu("Direct Spawn Now")]
        public void SpawnNow()
        {
            // // nếu thiếu asset thì thôi khỏi spawn cho đỡ lỗi null
            if (!directSpawnDefinition && !agentPrefab)
            {
                Debug.LogWarning("[Manual] Thiếu Definition và fallback prefab");
                return;
            }

            var pos = ResolveSpawnPosition(directSpawnOffset);
            var prefab = ResolvePrefab(directSpawnDefinition, agentPrefab);
            if (!prefab)
            {
                Debug.LogWarning("[Spawn] Không tìm thấy prefab");
                return;
            }

            var parent = agentsParent ? agentsParent : null; // null = lên root
            var agent = Instantiate(prefab, pos, Quaternion.identity, parent);
            agent.SetupCharacter(directSpawnDefinition, difficultyProvider, taskManager);
            Debug.Log($"[Manual] Direct spawned: {directSpawnDefinition?.DisplayName} at {pos}");
        }

        [ContextMenu("Hire Now")]
        public void HireNow()
        {
            // // nếu thiếu service/definition thì không hire được
            if (!hiringService || !hireDefinition)
            {
                Debug.LogWarning("[Manual] Thiếu HiringService/hireDefinition.");
                return;
            }

            var pos = ResolveSpawnPosition(hireSpawnOffset);
            var agent = hiringService.Hire(hireDefinition, pos);
            if (agent != null) Debug.Log($"[Manual] Hired: {hireDefinition.DisplayName} at {pos}");
        }

        // spawn 1 agent từ roster
        public void SpawnFromRosterOnce(CharacterDefinition def)
        {
            if (!def && !agentPrefab)
            {
                Debug.LogWarning("[WaveSpawner] Thiếu Definition và fallback prefab");
                return;
            }

            var pos = ResolveSpawnPosition(Vector3.zero);
            var prefab = ResolvePrefab(def, agentPrefab);
            if (!prefab)
            {
                Debug.LogWarning("[WaveSpawner] Không tìm thấy prefab");
                return;
            }

            var parent = agentsParent ? agentsParent : null;
            var agent = Instantiate(prefab, pos, Quaternion.identity, parent);
            agent.SetupCharacter(def, difficultyProvider, taskManager);
            Debug.Log($"[WaveSpawner] Spawned {def?.DisplayName ?? prefab.name} at {pos}");
        }

        // spawn cả roster 1 lượt (debug)
        [ContextMenu("Spawn Waves từ Roster")]
        public void SpawnWaveFromRoster()
        {
            for (int i = 0; i < waveRoster.Count; i++)
                SpawnFromRosterOnce(waveRoster[i]);
        }

        // helper: chọn prefab
        private CharacterAgent ResolvePrefab(CharacterDefinition def, CharacterAgent fallback)
        {
            var fromDef = def ? def.GetRandomPrefab() : null;
            return fromDef ? fromDef : fallback;
        }

        // helper: tìm vị trí spawn => round-robin hoặc transform.position, rồi cộng offset
        private Vector3 ResolveSpawnPosition(Vector3 offset)
        {
            Vector3 basePos = (spawnPointSet != null && spawnPointSet.HasAny)
                ? spawnPointSet.GetNextRoundPoint()
                : transform.position;

            return basePos + offset;
        }

        private void Update()
        {
            if (directSpawnKey != KeyCode.None && Input.GetKeyDown(directSpawnKey)) SpawnNow();
            if (hireKey != KeyCode.None && Input.GetKeyDown(hireKey)) HireNow();
        }
    }
}
