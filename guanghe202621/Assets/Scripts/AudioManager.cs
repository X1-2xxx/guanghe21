using UnityEngine;

/// <summary>Runtime audio routing with saved music/SFX levels and simple UI feedback.</summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicKey = "GameTemplate.MusicVolume";
    private const string SfxKey = "GameTemplate.SfxVolume";
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioClip uiTone;

    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }

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
        musicSource.clip = clip;
        if (clip == null) musicSource.Stop();
        else musicSource.Play();
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip, SfxVolume);
    }

    public void PlayUiClick()
    {
        if (uiTone != null && sfxSource != null) sfxSource.PlayOneShot(uiTone, SfxVolume * 0.35f);
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
