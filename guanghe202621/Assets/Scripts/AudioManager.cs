using UnityEngine;

/// <summary>Runtime audio routing with saved music/SFX levels and simple UI feedback.</summary>
public sealed class AudioManager : MonoBehaviour
{
    [Header("背景音乐")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;

    [Header("玩法音效")]
    [SerializeField] private AudioClip turnCharacterSwitch;
    [SerializeField] private AudioClip footstep;
    [SerializeField] private AudioClip dash;
    [SerializeField] private AudioClip jumpTakeoff;
    [SerializeField] private AudioClip jumpLanding;
    [SerializeField] private AudioClip springBounce;
    [SerializeField] private AudioClip thinBoardBreak;

    [Header("物理块音效")]
    [Tooltip("物理块碰撞或落地时播放，同一段音效可复用。")]
    [SerializeField] private AudioClip physicsBlockImpact;
    [Tooltip("粘贴成功时播放，纸张掀动后啪的一声粘在一起。")]
    [SerializeField] private AudioClip physicsBlockAttach;
    [Tooltip("分离成功时播放，剪刀剪开物理块之间纸片的声音。")]
    [SerializeField] private AudioClip physicsBlockDetach;

    private const string MusicKey = "GameTemplate.MusicVolume";
    private const string SfxKey = "GameTemplate.SfxVolume";
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioClip uiTone;

    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }

    private void Awake() => Initialize();
    private void OnEnable() => GameEventCenter.SfxRequested += OnSfxRequested;
    private void OnDisable() => GameEventCenter.SfxRequested -= OnSfxRequested;

    public void Initialize()
    {
        if (musicSource != null) return;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.7f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        ApplyVolumes();
        uiTone = CreateUiTone();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        if (musicSource != null) musicSource.volume = MusicVolume;
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        if (sfxSource != null) sfxSource.volume = SfxVolume;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null) return;
        if (clip != null && musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.Stop();
        musicSource.clip = clip;
        if (clip != null) musicSource.Play();
    }

    public void PlayMenuMusic() => PlayMusic(menuMusic);
    public void PlayGameplayMusic() => PlayMusic(gameplayMusic);

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
    }

    public void PlayUiClick()
    {
        if (uiTone != null && sfxSource != null) sfxSource.PlayOneShot(uiTone, 0.35f);
    }

    private void OnSfxRequested(GameplaySfx sound)
    {
        var clip = sound switch
        {
            GameplaySfx.TurnCharacterSwitch => turnCharacterSwitch,
            GameplaySfx.Footstep => footstep,
            GameplaySfx.Dash => dash,
            GameplaySfx.JumpTakeoff => jumpTakeoff,
            GameplaySfx.JumpLanding => jumpLanding,
            GameplaySfx.SpringBounce => springBounce,
            GameplaySfx.ThinBoardBreak => thinBoardBreak,
            GameplaySfx.PhysicsBlockImpact => physicsBlockImpact,
            GameplaySfx.PhysicsBlockAttach => physicsBlockAttach,
            GameplaySfx.PhysicsBlockDetach => physicsBlockDetach,
            _ => null
        };

        PlaySfx(clip);
    }

    private void ApplyVolumes()
    {
        musicSource.volume = MusicVolume;
        sfxSource.volume = SfxVolume;
    }

    private static AudioClip CreateUiTone()
    {
        const int sampleRate = 22050;
        const int sampleCount = 2205;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var envelope = 1f - (float)i / sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 660f * i / sampleRate) * envelope * 0.12f;
        }
        var clip = AudioClip.Create("UI Click Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
