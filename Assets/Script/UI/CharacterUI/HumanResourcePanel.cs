using System.Collections.Generic;
using UnityEngine;

namespace Wargency.Gameplay
{

    // panel nhân sự để hiện list ứng viên thuê
    // lấy catalog từ HiringService rồi spawn từng item UI
    // nếu item bị limit hay thuê rồi thì panel tự gỡ nó ra
    // panel này hiện danh sách ứng viên để thuê
    // build list từ Catalog của HiringService => mỗi item tự biết cách enable nút hire
    // item thuê xong hay đụng limit thì gửi tín hiệu để panel gỡ item cho gọn
    public class HumanResourcePanel : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private CharacterHiringService hiringService;
        [SerializeField, Tooltip("Nơi agent sẽ spawn")] private Transform defaultSpawnPoint;
        [SerializeField, Tooltip("Optional WaveManager")] private WaveManager waveManager;

        [Header("UI Layout")]
        [SerializeField, Tooltip("Prefab 1 item HireCharacter")]
        private HireCharacterUI itemPrefab;
        [SerializeField, Tooltip("Content của ScrollView/Grid để chứa item")]
        private Transform itemsParent;

        private readonly List<HireCharacterUI> pool = new();

        private void OnEnable() => BuildOrRefresh();

        public void BuildOrRefresh()
        {
            if (!hiringService || !itemPrefab || !itemsParent) return;

            // Dọn cũ
            for (int i = 0; i < pool.Count; i++)
                if (pool[i]) Destroy(pool[i].gameObject);
            pool.Clear();

            // Lấy catalog từ service
            var catalog = hiringService.Catalog; // :contentReference[oaicite:6]{index=6}
            if (catalog == null) return;

            foreach (var opt in catalog)
            {
                if (opt == null || opt.definition == null) continue;

                var item = Instantiate(itemPrefab, itemsParent);
                item.Setup(hiringService, opt, defaultSpawnPoint, waveManager); // đảm bảo đúng chữ ký
                item.OnRequestRemove += HandleItemRemoveRequested;

                pool.Add(item);
            }
        }

        private void HandleItemRemoveRequested(HireCharacterUI item)
        {
            if (!item) return;

            int idx = pool.IndexOf(item);
            if (idx >= 0) pool.RemoveAt(idx);

            Destroy(item.gameObject);

            // (Optional) Nếu muốn rebuild/relayout sau khi gỡ
            // StartCoroutine(RebuildNextFrame());
        }
    }
}