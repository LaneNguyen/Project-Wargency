using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{
    public class HumanResourcePanel : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private CharacterHiringService hiringService;
        [SerializeField, Tooltip("Nơi agent sẽ spawn")]
        private Transform defaultSpawnPoint;
        [SerializeField, Tooltip("Optional WaveManager")]
        private WaveManager waveManager;
    

        [Header("UI Layout")]
        [SerializeField, Tooltip("Prefab 1 item HireCharacter")]
        private HireCharacterUI itemPrefab;
        [SerializeField, Tooltip("Content của ScrollView/Grid để chứa item")]
        private Transform itemsParent;

        private readonly List<HireCharacterUI> pool = new();

        private void OnEnable()
        {
            BuildOrRefresh();
        }

        public void BuildOrRefresh()
        {
            if (!hiringService || !itemPrefab || !itemsParent) return;

            // dọn cũ
            for (int i = 0; i < pool.Count; i++)
                if (pool[i])
                { 
                    Destroy(pool[i].gameObject); 
                }
            pool.Clear();

            // lấy catalog từ service
            var catalog = hiringService.Catalog;
            if (catalog == null) return;

            foreach (var opt in catalog)
            {
                if (opt == null || opt.definition == null) continue;

                var item = Instantiate(itemPrefab, itemsParent);
                item.Setup(hiringService, opt, defaultSpawnPoint, waveManager);
                pool.Add(item);
            }
        }
    }
}
