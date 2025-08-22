using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // Goal hiện tại: Spawner theo Wave — lắng nghe WaveManager để spawn/unlock agent. thủ công để test thôi
    // - Direct Spawn: bỏ qua ngân sách (dành cho debug).
    // - Hire Spawn: thông qua HiringService (trừ tiền thật).
    // update này 2208 - Vị trí spawn lấy từ SpawnPointSets (round-robin) hoặc fallback transform.position

    public class CharacterWaveSpawner : MonoBehaviour
    {
        [Header("Direct Spawn (bỏ qua ngân sách)")]
        [SerializeField] private CharacterAgent agentPrefab; // fallback khi Definition không có prefab
        [SerializeField] private CharacterDefinition directSpawnDefinition;
        [SerializeField] private Vector3 directSpawnOffset = Vector3.zero;

        [Header("Hire Spawn (qua HiringService)")]
        [SerializeField] private CharacterHiringService hiringService;
        [SerializeField] private CharacterDefinition hireDefinition;
        [SerializeField] private Vector3 hireSpawnOffset = Vector3.zero;

        [Header("Wave Roster")]
        [SerializeField, Tooltip("Spawn theo list sẵn cho mỗi wave (nếu dùng cơ chế wave tự động)")]
        private List<CharacterDefinition> waveRoster = new();

        [Header("Điểm Spawn")]
        [SerializeField, Tooltip("Tập điểm spawn cho Wave/Hiring. Nếu null hoặc rỗng => fallback transform.position")]
        private SpawnPointSets spawnPointSet; 

        [Header("Difficulty/Task refs (optional)")]
        [SerializeField] private UnityEngine.Object difficultyProviderObj;
        [SerializeField] private TaskManager taskManager;
        private IDifficultyProvider difficultyProvider;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode directSpawnKey = KeyCode.F6;
        [SerializeField] private KeyCode hireKey = KeyCode.F7;

        private void Awake()
        {
            if (difficultyProviderObj is IDifficultyProvider dp) difficultyProvider = dp;
            if (!taskManager) taskManager = FindAnyObjectByType<TaskManager>();
        }


        // Gọi ở đầu mỗi wave dựa theo WaveManager
        public void OnBeginWave()
        {
            if (spawnPointSet != null) spawnPointSet.ResetCycle();
        }

        [ContextMenu("Direct Spawn Now")]
        public void SpawnNow()
        {
            if (!directSpawnDefinition && !agentPrefab)
            {
                Debug.LogWarning("[Manual] Thiếu Definition và fallback prefab");
                return; 
            }

            var pos = ResolveSpawnPosition(directSpawnOffset);

            var prefab = ResolvePrefab(directSpawnDefinition, agentPrefab);

            if (!prefab)
            {
                Debug.LogWarning("[Spawn] Không tìm thấy prefab (Definition rỗng & fallback hổng có)");
                return;
            }

            var agent = Instantiate(prefab, pos, Quaternion.identity, transform);
            agent.SetupCharacter(directSpawnDefinition, difficultyProvider, taskManager);
            Debug.Log($"[Manual] Direct spawned: {directSpawnDefinition.DisplayName} at {pos}");
        }


        [ContextMenu("Hire Now")]
        public void HireNow()
        {
            if (!hiringService || !hireDefinition) 
            { 
                Debug.LogWarning("[Manual] Thiếu HiringService/hireDefinition."); 
                return; 
            }

            var pos = ResolveSpawnPosition(hireSpawnOffset);
            var agent = hiringService.Hire(hireDefinition, pos);
            if (agent != null) Debug.Log($"[Manual] Hired: {hireDefinition.DisplayName} at {pos}");
        }



        // Spawn một agent theo Definition (dùng cho cơ chế roster).
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

            var agent = Instantiate(prefab, pos, Quaternion.identity, transform);
            agent.SetupCharacter(def, difficultyProvider, taskManager);
            Debug.Log($"[WaveSpawner] Spawned {def?.DisplayName ?? prefab.name} ({def?.Role.ToString() ?? "Unknown"}) at {pos}");
        }

        // Vòng qua roster và spawn tất cả. Dùng để debug
        [ContextMenu("Spawn Waves từ Roster")]
        public void SpawnWaveFromRoster() 
        {
            for (int i = 0; i < waveRoster.Count; i++)
                SpawnFromRosterOnce(waveRoster[i]);
        }


        // ------------------ khu vực helper cho đống trên ------------------

        private CharacterAgent ResolvePrefab(CharacterDefinition def, CharacterAgent fallback)
        {
            var fromDef = def != null ? def.GetRandomPrefab() : null; // hoặc GetPrefabByIndex(...)
            return fromDef != null ? fromDef : fallback;
        }
        // Lấy vị trí spawn:
        // - Nếu SpawnPointSets có dữ liệu => dùng round-robin
        // - Ngược lại => transform.position
        // Offset được cộng sau cùng
        private Vector3 ResolveSpawnPosition(Vector3 offset) // ADDED
        {
            Vector3 basePos;
            if (spawnPointSet != null && spawnPointSet.HasAny)
                basePos = spawnPointSet.GetNextRoundPoint();
            else
                basePos = transform.position;

            return basePos + offset;
        }


        private void Update()
        {
            if (directSpawnKey != KeyCode.None && Input.GetKeyDown(directSpawnKey)) SpawnNow();
            if (hireKey != KeyCode.None && Input.GetKeyDown(hireKey)) HireNow();
        }
    }
}