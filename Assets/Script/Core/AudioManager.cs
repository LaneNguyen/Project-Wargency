using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : BaseManager<AudioManager>
{
	private const float BGM_FADE_SPEED_RATE_HIGH = 0.9f;
	private const float BGM_FADE_SPEED_RATE_LOW = 0.3f;

	private const string BGM_VOLUME_KEY = "BGM_VOLUME_KEY";
	private const string SE_VOLUME_KEY = "SE_VOLUME_KEY";
	private const float BGM_VOLUME_DEFAULT = 0.2f;
	private const float SE_VOLUME_DEFAULT = 1f;

	private const string BGM_MUTE_KEY = "BGM_MUTE_KEY";
	private const string SE_MUTE_KEY = "SE_MUTE_KEY";
	private const int BGM_MUTE_DEFAULT = 0;
	private const int SE_MUTE_DEFAULT = 0;

	private float bgmFadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH;

	//Next BGM name, SE name
	private string nextBGMName;
	private string nextSEName;

	//Is the background music fading out?
	private bool isFadeOut = false;

	//Separate audio sources for BGM and SE
	public AudioSource AttachBGMSource;
	public AudioSource AttachSESource;

	//Keep All Audio
	private Dictionary<string, AudioClip> bgmDic;
	private Dictionary<string, AudioClip> seDic;

	protected override void Awake()
    {
        base.Awake();
		//Load all SE & BGM files from resource folder
		bgmDic = new Dictionary<string, AudioClip>();
		seDic = new Dictionary<string, AudioClip>();

		object[] bgmList = Resources.LoadAll("Audio/BGM");
		object[] seList = Resources.LoadAll("Audio/SE");

		foreach (AudioClip bgm in bgmList)
		{
			bgmDic[bgm.name] = bgm;
		}
		foreach (AudioClip se in seList)
		{
			seDic[se.name] = se;
		}
	}

    private void Start()
	{
		AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
		//Debug.Log($"AttachBGMSource volume: {AttachBGMSource.volume}");
		AttachSESource.volume = PlayerPrefs.GetFloat(SE_VOLUME_KEY, SE_VOLUME_DEFAULT);
		//Debug.Log($"AttachSESource: volume: {AttachSESource.volume}");
		bool isMuteBgm = (PlayerPrefs.GetInt(BGM_MUTE_KEY, BGM_MUTE_DEFAULT) == BGM_MUTE_DEFAULT) ? false : true;
		AttachBGMSource.mute = isMuteBgm;
		//Debug.Log($"AttachBGMSource mute: {AttachBGMSource.mute}");
		bool isMuteSe = (PlayerPrefs.GetInt(SE_MUTE_KEY, SE_MUTE_DEFAULT) == BGM_MUTE_DEFAULT) ? false : true;
		AttachSESource.mute = isMuteSe;
		//Debug.Log($"AttachSESource mute: {AttachSESource.mute}");
	}

	public void PlaySE(string seName, float delay = 0.0f)
	{
		if (!seDic.ContainsKey(seName))
		{
			Debug.Log(seName + "There is no SE named");
			return;
		}

		nextSEName = seName;
		Invoke(nameof(DelayPlaySE), delay);
	}

	private void DelayPlaySE()
	{
		AttachSESource.PlayOneShot(seDic[nextSEName] as AudioClip);
	}

	public void PlayBGM(string bgmName, float fadeSpeedRate = BGM_FADE_SPEED_RATE_HIGH)
	{
		if (!bgmDic.ContainsKey(bgmName))
		{
			Debug.Log(bgmName + "There is no BGM named");
			return;
		}

		//If BGM is not currently playing, play it as is
		if (!AttachBGMSource.isPlaying)
		{
			nextBGMName = "";
			AttachBGMSource.clip = bgmDic[bgmName] as AudioClip;
			AttachBGMSource.Play();
		}
		//When a different BGM is playing, fade out the BGM that is playing before playing the next one.
        //Ignore when the same BGM is playing
		else if (AttachBGMSource.clip.name != bgmName)
		{
			nextBGMName = bgmName;
			FadeOutBGM(fadeSpeedRate);
		}

	}

	public void FadeOutBGM(float fadeSpeedRate = BGM_FADE_SPEED_RATE_LOW)
	{
		bgmFadeSpeedRate = fadeSpeedRate;
		isFadeOut = true;
	}

	private void Update()
	{
		if (!isFadeOut)
		{
			return;
		}

		//Gradually lower the volume, and when the volume reaches 0
        //return the volume and play the next song
		AttachBGMSource.volume -= Time.deltaTime * bgmFadeSpeedRate;
		if (AttachBGMSource.volume <= 0)
		{
			AttachBGMSource.Stop();
			AttachBGMSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, BGM_VOLUME_DEFAULT);
			isFadeOut = false;

			if (!string.IsNullOrEmpty(nextBGMName))
			{
				PlayBGM(nextBGMName);
			}
		}
	}

	public void ChangeBGMVolume(float BGMVolume)
	{
		AttachBGMSource.volume = BGMVolume;
		PlayerPrefs.SetFloat(BGM_VOLUME_KEY, BGMVolume);
	}

	public void ChangeSEVolume(float SEVolume)
	{
		AttachSESource.volume = SEVolume;
		PlayerPrefs.SetFloat(SE_VOLUME_KEY, SEVolume);
	}

	public void MuteBGM(bool isMute)
    {
		AttachBGMSource.mute = isMute;

		int isMuteValue = 0;

        if (isMute)
        {
			isMuteValue = 1;
		}

		PlayerPrefs.SetInt(BGM_MUTE_KEY, isMuteValue);

		//Debug.Log($"AttachBGMSource mute: {PlayerPrefs.GetInt(BGM_MUTE_KEY)}");
	}

	public void MuteSE(bool isMute)
	{
		AttachSESource.mute = isMute;

		int isMuteValue = 0;

		if (isMute)
		{
			isMuteValue = 1;
		}

		PlayerPrefs.SetInt(SE_MUTE_KEY, isMuteValue);

		//Debug.Log($"AttachSESource mute: {PlayerPrefs.GetInt(SE_MUTE_KEY)}");
	}
}
