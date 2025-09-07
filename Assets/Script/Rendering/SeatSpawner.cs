using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SeatSpawner : MonoBehaviour
{
    [Header("Anchors (điểm đặt CHÂN trước bàn)")]
    [Tooltip("Kéo thả tất cả Transform làm feet-anchor vào đây (đặt ngay mép TRƯỚC của bàn).")]
    [SerializeField] private List<Transform> anchors = new List<Transform>();

    [Header("Tự tìm anchor theo Tag (tuỳ chọn)")]
    [SerializeField] private bool autoFindByTag = false;
    [SerializeField] private string anchorTag = "SeatAnchor";

    [Header("Thiết lập vị trí chân")]
    [Tooltip("Âm nghĩa là đẩy nhân vật xuống dưới 1 chút để đứng trước mép bàn.")]
    [SerializeField] private float yInset = -0.10f; // -0.08 ~ -0.12 là vừa

    [Header("Giới hạn Y quanh anchor (anti-push-up)")]
    [Tooltip("Biên DƯỚI tương đối so với anchor Y (ví dụ -0.14).")]
    [SerializeField] private float yClampMin = -0.14f;
    [Tooltip("Biên TRÊN tương đối so với anchor Y (ví dụ -0.06).")]
    [SerializeField] private float yClampMax = -0.06f;

    [Header("Khoá Y vài frame đầu (chống root-motion/physics)")]
    [Tooltip("Số frame sẽ khoá Y cố định sau khi spawn.")]
    [SerializeField] private int lockYForFrames = 5;

    [Header("Cấp phát slot")]
    [Tooltip("Chọn chỗ theo vòng tròn (true) hay lấy chỗ trống đầu tiên (false)")]
    [SerializeField] private bool useRoundRobin = true;

    [Header("Sorting (tuỳ chọn)")]
    [SerializeField] private bool setSortingByY = false;
    [SerializeField] private int sortingBase = 5000;
    [SerializeField] private int sortingMul = 1000;

    // --- Nội bộ ---
    private readonly Dictionary<Transform, GameObject> occupied = new Dictionary<Transform, GameObject>();
    private int lastIndex = -1;

    void Awake()
    {
        if (autoFindByTag)
        {
            anchors = GameObject.FindGameObjectsWithTag(anchorTag)
                                .Select(go => go.transform)
                                .OrderBy(t => t.GetInstanceID())
                                .ToList();
        }

        occupied.Clear();
        foreach (var a in anchors.Where(a => a != null))
            occupied[a] = null;

        // Bảo vệ cấu hình: đảm bảo min <= max
        if (yClampMin > yClampMax)
        {
            float t = yClampMin; yClampMin = yClampMax; yClampMax = t;
        }
    }

    /// <summary>
    /// Spawn 1 prefab vào ghế trống, trả về GameObject đã tạo (null nếu hết chỗ).
    /// YÊU CẦU: Sprite pivot nhân vật là Bottom-Center (0.5, 0).
    /// </summary>
    public GameObject Spawn(GameObject prefab)
    {
        if (prefab == null || anchors == null || anchors.Count == 0)
        {
            Debug.LogWarning("[SeatSpawner] Prefab hoặc anchors rỗng.");
            return null;
        }

        var anchor = GetFreeAnchor();
        if (anchor == null)
        {
            Debug.LogWarning("[SeatSpawner] Hết chỗ trống.");
            return null;
        }

        // Tính vị trí CHÂN
        float baseY = anchor.position.y + yInset;
        // Giới hạn trong dải an toàn quanh anchor
        float clampedY = Mathf.Clamp(baseY, anchor.position.y + yClampMin, anchor.position.y + yClampMax);
        Vector3 feetPos = new Vector3(anchor.position.x, clampedY, anchor.position.z);

        var go = Instantiate(prefab, feetPos, Quaternion.identity);

        // Đánh dấu đã chiếm chỗ
        occupied[anchor] = go;

        // (tuỳ chọn) set sorting theo Y để cảm giác xếp lớp đúng
        if (setSortingByY)
        {
            var r = go.GetComponentInChildren<Renderer>();
            if (r) r.sortingOrder = sortingBase - Mathf.RoundToInt(feetPos.y * sortingMul);
        }

        // Tự giải phóng chỗ khi bị Destroy
        var token = go.AddComponent<SeatReleaseToken>();
        token.Init(this, anchor);

        // Khoá Y vài frame đầu để triệt tiêu animator root-motion / physics đẩy
        if (lockYForFrames > 0)
        {
            var sticky = go.AddComponent<SeatStickyY>();
            sticky.Init(clampedY, lockYForFrames);
        }

        return go;
    }

    public void Release(Transform anchor, GameObject who)
    {
        if (anchor == null) return;
        if (!occupied.ContainsKey(anchor)) return;
        if (occupied[anchor] == who) occupied[anchor] = null;
    }

    // ----- Helpers -----
    private Transform GetFreeAnchor()
    {
        if (useRoundRobin)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                lastIndex = (lastIndex + 1) % anchors.Count;
                var a = anchors[lastIndex];
                if (a != null && !IsOccupied(a)) return a;
            }
            return null;
        }
        else
        {
            foreach (var a in anchors)
                if (a != null && !IsOccupied(a)) return a;
            return null;
        }
    }

    private bool IsOccupied(Transform a)
    {
        return occupied.TryGetValue(a, out var who) && who != null;
    }

    // --- Token tự release khi GameObject bị hủy (gói kèm trong 1 file) ---
    private class SeatReleaseToken : MonoBehaviour
    {
        private SeatSpawner spawner;
        private Transform anchor;
        public void Init(SeatSpawner s, Transform a) { spawner = s; anchor = a; }
        void OnDestroy() { if (spawner != null && anchor != null) spawner.Release(anchor, gameObject); }
    }

    // --- Khoá Y trong vài frame đầu để chống bị đẩy lên cao ---
    private class SeatStickyY : MonoBehaviour
    {
        private float yFixed;
        private int frames;
        public void Init(float y, int frameCount) { yFixed = y; frames = frameCount; }

        void LateUpdate()
        {
            if (frames <= 0) { Destroy(this); return; }
            var p = transform.position;
            p.y = yFixed;
            transform.position = p;
            frames--;
        }
    }

#if UNITY_EDITOR
    // Vẽ gizmo để canh đúng vị trí chân
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        foreach (var a in anchors)
        {
            if (!a) continue;
            float baseY = a.position.y + yInset;
            float minY = a.position.y + yClampMin;
            float maxY = a.position.y + yClampMax;

            // dải clamp
            Gizmos.DrawLine(new Vector3(a.position.x, minY, a.position.z), new Vector3(a.position.x, maxY, a.position.z));
            // vị trí spawn thực tế (đã clamp)
            float clampedY = Mathf.Clamp(baseY, minY, maxY);
            Gizmos.DrawSphere(new Vector3(a.position.x, clampedY, a.position.z), 0.04f);
        }
    }
#endif
}
