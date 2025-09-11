using UnityEngine;
using UnityEngine.UI;
using Wargency.UI;

public class SettingPanel : MonoBehaviour
{
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Toggle bgmMute;
    [SerializeField] private Toggle seMute;

    private float bgmValue;
    private float seValue;

    private bool isBGMMute;
    private bool isSEMute;

    void Start()
    {
        if (AudioManager.HasInstance)
        {
            bgmValue = AudioManager.Instance.AttachBGMSource.volume;
            seValue = AudioManager.Instance.AttachSESource.volume;
            bgmSlider.value = bgmValue;
            seSlider.value = seValue;

            isBGMMute = AudioManager.Instance.AttachBGMSource.mute;
            isSEMute = AudioManager.Instance.AttachSESource.mute;
            bgmMute.isOn = isBGMMute;
            seMute.isOn = isSEMute;
        }
    }

    private void OnEnable()
    {
        if (AudioManager.HasInstance)
        {
            bgmValue = AudioManager.Instance.AttachBGMSource.volume;
            seValue = AudioManager.Instance.AttachSESource.volume;
            bgmSlider.value = bgmValue;
            seSlider.value = seValue;

            isBGMMute = AudioManager.Instance.AttachBGMSource.mute;
            isSEMute = AudioManager.Instance.AttachSESource.mute;
            bgmMute.isOn = isBGMMute;
            seMute.isOn = isSEMute;
        }
    }

    public void OnSliderChangeBGMValue(float v)
    {
        bgmValue = v;
    }

    public void OnSliderChangeSEValue(float v)
    {
        seValue = v;
    }

    public void OnChangeValueBGMMute(bool v)
    {
        isBGMMute = v;
    }

    public void OnChangeValueSEMute(bool v)
    {
        isSEMute = v;
    }

    public void OnSubmitButtonClick()
    {
        if (AudioManager.HasInstance)
        {
            AudioManager.Instance.ChangeBGMVolume(bgmValue);
            AudioManager.Instance.ChangeSEVolume(seValue);
            AudioManager.Instance.MuteBGM(isBGMMute);
            AudioManager.Instance.MuteSE(isSEMute);
        }

        // đóng panel
        UIManager.instance?.CloseSettings();
    }

    public void OnCloseButtonClick()
    {
        // Đóng đúng cách: gỡ pause
        UIManager.instance?.CloseSettings(); // thay vì chỉ SetActive(false)
    }

}
