using UnityEngine;
using UnityEngine.Playables;

public class TimelineAutoDisable : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [Tooltip("Tắt GameObject này khi timeline chạy xong")]
    [SerializeField] private GameObject targetToDisable;

    void Awake()
    {
        if (!director) director = GetComponent<PlayableDirector>();
        if (!targetToDisable) targetToDisable = gameObject;

        if (director) director.stopped += OnTimelineStopped;
    }

    private void OnTimelineStopped(PlayableDirector obj)
    {
        targetToDisable.SetActive(false);
    }

    void OnDestroy()
    {
        if (director) director.stopped -= OnTimelineStopped;
    }
}
