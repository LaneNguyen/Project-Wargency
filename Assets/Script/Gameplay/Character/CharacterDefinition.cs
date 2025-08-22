using UnityEngine;
using System.Collections.Generic;


namespace Wargency.Gameplay
{
    public enum CharacterRole { Designer =0, Copywriter=1, Planner =2, Account =3}

    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Wargency/Character")]

    public class CharacterDefinition : ScriptableObject
    {
        [Header("Định danh")]
        [SerializeField, Tooltip("Unique id, dùng nội bộ")] private string id = "char_001";
        [SerializeField, Tooltip("Tên hiển thị")] private string displayName = "Designer";

        [Header("Visual nhân vật")]
        [SerializeField, Tooltip("Ảnh chân dung cho UI/Avatar")] private Sprite avatar;// đang cân nhắc vụ này
        [SerializeField, Tooltip("Sprite body in-scene")] private Sprite body;

        [Header("Role")]
        [SerializeField] private CharacterRole role = CharacterRole.Designer;

        [Header("Chỉ số")]
        [Min(0)][SerializeField] private int baseEnergy = 100;
        [Min(0)][SerializeField] private int baseStress = 0;
        [Min(0f)][SerializeField] private float moveSpeed = 3.5f;

        [Header("Prefabs (override)")]
        [Tooltip("Nếu để trống sẽ dùng agentPrefab mặc định từ Hiring/Wave Spawner")]
        [SerializeField] private List<CharacterAgent> agentPrefabs = new();

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Avatar => avatar;
        public Sprite Body => body;
        public CharacterRole Role => role;
        public int BaseEnergy => baseEnergy;
        public int BaseStress => baseStress;
        public float MoveSpeed => moveSpeed;
        public IReadOnlyList<CharacterAgent> AgentPrefabs => agentPrefabs;

        public CharacterAgent GetRandomPrefab()
        {
            if (agentPrefabs == null || agentPrefabs.Count == 0) return null;
            int i = Random.Range(0, agentPrefabs.Count);
            return agentPrefabs[i];
        }

        public CharacterAgent GetPrefabByIndex(int index)
        {
            if (agentPrefabs == null || agentPrefabs.Count == 0) return null;
            index = Mathf.Clamp(index, 0, agentPrefabs.Count - 1);
            return agentPrefabs[index];
        }

        public bool HasPrefabs => agentPrefabs != null && agentPrefabs.Count > 0;

    }
}