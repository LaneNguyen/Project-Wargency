using UnityEngine;

namespace Wargency.Core
{
    public static class IsometricHelper
    {
        //script static để khỏi gán vào bất kỳ object nào để chạy, chạy ở đâu cũng dc
        public static Vector3 SnapToGrid(Vector3 worldPos, float cellWidth, float cellHeight)
        {
            //Căn chỉnh vị trí một object về chính xác ô grid gần nhất dựa trên chiều rộng và chiều cao cell.
            float x = Mathf.Round(worldPos.x / cellWidth) * cellWidth; // tọa độ đối tượng chia kích thước ô để biết nằm ở khoảng nào, làm tròn để chỉ ra ô gần nhất
                                                                       //nhân lại với kích thước ô để ra vị trí tâm ô đó
            float y = Mathf.Round(worldPos.y / cellHeight) * cellHeight;
            return new Vector3(x, y, 0f); //trả lại giá trị x y với z =0f vì 2d
        }
        public static int OrderFromY(float y, float scale = 100f)
        {   //tính sorting order cho sprite dựa vào Y → đảm bảo object thấp hơn sẽ vẽ đè lên object cao hơn hệt isometric thực
            return Mathf.RoundToInt(-y * scale); //-y để object nào có y thấp hơn => sorting order lớn hơn 
        }
    }
}
