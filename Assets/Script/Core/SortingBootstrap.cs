using UnityEngine;
using UnityEngine.Rendering;

namespace Wargency.Core
{
    [DefaultExecutionOrder(-100)] //đảm bảo script chạy sớm khi load scene
    public class SortingBootstrap : MonoBehaviour
    {
        //script thiết lập thứ tự vẽ cho sprite (render order), vì isometric giả thì cần cái nào gần màn hình hơn vẽ trước

        [SerializeField]
        private Vector3 customAxis = new Vector3(0f, 1f, 0f); // quyết định cái nào hiện trước, trong trường hợp này là dựa trên giá trị Y của object

        private void Awake()
        {
            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis; //tự định nghĩa trục để so sánh
            GraphicsSettings.transparencySortAxis = customAxis; // sắp xếp dựa vào giá trị Y ở trên
        }

    }
}