using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    // Goal hiện tại: Spawner theo Wave — lắng nghe WaveManager để spawn/unlock agent. thủ công để test thôi
    // - Direct Spawn: bỏ qua ngân sách (dành cho debug).
    // - Hire Spawn: thông qua HiringService (trừ tiền thật).

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

        [ContextMenu("Direct Spawn Now")]
        public void SpawnNow()
        {
            if (!agentPrefab || !directSpawnDefinition) { Debug.LogWarning("[Manual] Thiếu prefab/definition."); return; }
            var pos = transform.position + directSpawnOffset;
            var agent = Instantiate(agentPrefab, pos, Quaternion.identity);
            agent.SetupCharacter(directSpawnDefinition, difficultyProvider, taskManager);
            Debug.Log($"[Manual] Direct spawned: {directSpawnDefinition.DisplayName} at {pos}");
        }

        [ContextMenu("Hire Now")]
        public void HireNow()
        {
            if (!hiringService || !hireDefinition) { Debug.LogWarning("[Manual] Thiếu HiringService/hireDefinition."); return; }
            var pos = transform.position + hireSpawnOffset;
            var agent = hiringService.Hire(hireDefinition, pos);
            if (agent != null) Debug.Log($"[Manual] Hired: {hireDefinition.DisplayName} at {pos}");
        }

        private void Update()
        {
            if (directSpawnKey != KeyCode.None && Input.GetKeyDown(directSpawnKey)) SpawnNow();
            if (hireKey != KeyCode.None && Input.GetKeyDown(hireKey)) HireNow();
        }
    }
}