using UnityEngine;

public class QuitGameButton : MonoBehaviour
{
    // Gán hàm này vào OnClick của nút "Exit"
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // // thoát Play Mode khi test trong Editor
#else
        Application.Quit(); // // build thật: thoát ứng dụng
#endif
    }
}
