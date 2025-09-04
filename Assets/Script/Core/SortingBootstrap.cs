using UnityEngine;
using UnityEngine.Rendering;

namespace Wargency.Core
{
    // // SortingBootstrap
    // // nhiệm vụ: bật chế độ vẽ sprite theo trục mình muốn (CustomAxis)
    // // ở đây lấy trục Y làm “chuẩn đẹp trai”, y nào thấp hơn thì vẽ sau (đè lên), giả isometric nghèo mà hợp lý
    // // chạy sớm tí cho chắc kèo (DefaultExecutionOrder)

    [DefaultExecutionOrder(-100)] // // cho nó dậy sớm hơn mấy đứa khác
    public class SortingBootstrap : MonoBehaviour
    {
        [SerializeField]
        private Vector3 customAxis = new Vector3(0f, 1f, 0f); // // tập trung vào Y thôi, không suy nghĩ nhiều

        private void Awake()
        {
            // // bật mode theo trục tự chọn
            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;

            // // bảo nó dùng trục này nè (0,1,0), chuẩn bị đi isometric nghèo
            GraphicsSettings.transparencySortAxis = customAxis;
        }
    }
}
