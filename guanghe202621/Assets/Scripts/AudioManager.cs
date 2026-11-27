using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime audio routing with saved music/SFX levels and simple UI feedback.</summary>
public sealed class AudioManager : MonoBehaviour
{
    [SerializeField] private GameAudioLibrary library;
    [Header("背景音乐（可选的单独覆盖）")]
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
    [SerializeField, HideInInspector] private AudioSource musicSource, sfxSource, uiSource, voiceSource;
    private readonly Dictionary<GameplaySfx, float> nextAllowed = new Dictionary<GameplaySfx, float>();
    private readonly Dictionary<GameplaySfx, int> lastVariants = new Dictionary<GameplaySfx, int>();
    private bool initialized, worldAudioPaused;
    private GameState previousState;
    private float lastBounce = -10f;
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    private float MusicGain => library ? library.musicGain : 1f;

    private void Awake() => Initialize();
    private void OnEnable()
    {
        Initialize(); previousState = GameStateManager.Current;
        GameEventCenter.SfxRequested += OnSfxRequested;
        GameEventCenter.StateChanged += OnStateChanged;
    }
    private void OnDisable()
    {
        GameEventCenter.SfxRequested -= OnSfxRequested;
        GameEventCenter.StateChanged -= OnStateChanged;
        if (sfxSource) sfxSource.Stop();
        if (uiSource) uiSource.Stop();
        if (voiceSource) voiceSource.Stop();
        if (musicSource) musicSource.Stop();
        worldAudioPaused = false;
    }

    public void Initialize()
    {
        if (initialized) return;
        if (!library) library = Resources.Load<GameAudioLibrary>("Audio/GameAudioLibrary");
        musicSource = EnsureSource(musicSource, true);
        sfxSource = EnsureSource(sfxSource, false);
        uiSource = EnsureSource(uiSource, false);
        voiceSource = EnsureSource(voiceSource, false);
        uiSource.ignoreListenerPause = voiceSource.ignoreListenerPause = true;
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, .7f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, .8f));
        musicSource.volume = MusicVolume * MusicGain;
        sfxSource.volume = uiSource.volume = voiceSource.volume = SfxVolume;
        initialized = true;
    }
    private AudioSource EnsureSource(AudioSource source, bool loop)
    {
        if (!source) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop;
        source.spatialBlend = 0f; source.dopplerLevel = 0f;
        return source;
    }

    private void Update()
    {
        bool pause = GameStateManager.Current != GameState.Playing || Time.timeScale <= 0f ||
            (GamePresentation.PresentationRuntime.Instance != null && GamePresentation.PresentationRuntime.Instance.Director.WorldPaused);
        if (pause == worldAudioPaused) return;
        worldAudioPaused = pause;
        if (pause) sfxSource.Pause(); else sfxSource.UnPause();
    }
    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Paused) OnSfxRequested(GameplaySfx.PauseMenuOpen);
        else if (state == GameState.Playing && previousState == GameState.Paused) OnSfxRequested(GameplaySfx.PauseMenuClose);
        if (state == GameState.MainMenu || state == GameState.Victory || state == GameState.Defeat)
        {
            sfxSource.Stop(); voiceSource.Stop();
            nextAllowed.Clear(); lastBounce = -10f;
        }
        else if (state == GameState.Paused) voiceSource.Stop();
        previousState = state;
    }
    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        if (musicSource) musicSource.volume = MusicVolume * MusicGain;
    }
    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        if (sfxSource) sfxSource.volume = SfxVolume;
        if (uiSource) uiSource.volume = SfxVolume;
        if (voiceSource) voiceSource.volume = SfxVolume;
    }
    public void PlayMusic(AudioClip clip)
    {
        if (!musicSource) return;
        if (clip && musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.Stop(); musicSource.clip = clip;
        if (clip) musicSource.Play();
    }
    public void PlayMenuMusic() => PlayMusic(menuMusic ? menuMusic : library ? library.menuMusic : null);
    public void PlayGameplayMusic() => PlayMusic(gameplayMusic ? gameplayMusic : library ? library.gameplayMusic : null);
    public void PlaySfx(AudioClip clip)
    {
        if (clip && sfxSource && GameStateManager.Current == GameState.Playing && !WorldPaused())
        { sfxSource.UnPause(); sfxSource.PlayOneShot(clip); }
    }
    public void PlayUiClick() => OnSfxRequested(GameplaySfx.DialogueOptionClick);
    private static bool WorldPaused() => GamePresentation.PresentationRuntime.Instance != null &&
        GamePresentation.PresentationRuntime.Instance.Director.WorldPaused;

    private void OnSfxRequested(GameplaySfx sound)
    {
        bool typing = sound == GameplaySfx.DialogueTyping || sound == GameplaySfx.CharacterVoice;
        bool ui = typing || sound == GameplaySfx.PauseMenuOpen || sound == GameplaySfx.PauseMenuClose ||
            sound == GameplaySfx.DialogueOptionClick || sound == GameplaySfx.SpeechBubblePop;
        // Saving is triggered immediately before a checkpoint dialogue pauses the world.
        bool save = sound == GameplaySfx.CampfireSave;
        if (!ui && (GameStateManager.Current != GameState.Playing || WorldPaused() || Time.timeScale <= 0f)) return;
        if (typing && GameStateManager.Current != GameState.Playing) return;
        float now = Time.unscaledTime;
        if (nextAllowed.TryGetValue(sound, out float next) && now < next) return;
        if (sound == GameplaySfx.JumpLanding && now - lastBounce < .16f) return;
        var cue = library ? library.Find(sound) : null;
        // The dedicated sprint recording has not been supplied yet.
        if (sound == GameplaySfx.Dash && !dash && !HasClips(cue))
        { OnSfxRequested(GameplaySfx.Footstep); return; }
        AudioClip clip = LegacyClip(sound);
        if (!clip && HasClips(cue))
        {
            int count = cue.clips.Length, index = Random.Range(0, count);
            if (count > 1 && lastVariants.TryGetValue(sound, out int last) && index == last)
                index = (index + Random.Range(1, count)) % count;
            lastVariants[sound] = index; clip = cue.clips[index];
        }
        if (!clip) return;
        nextAllowed[sound] = now + (cue != null ? cue.cooldown : .08f);
        if (sound == GameplaySfx.SpringBounce) lastBounce = now;
        var source = typing ? voiceSource : (ui || save) ? uiSource : sfxSource;
        if (!source) return;
        if (source == sfxSource) source.UnPause();
        source.PlayOneShot(clip, cue != null ? cue.volume : 1f);
    }
    private static bool HasClips(GameAudioLibrary.Cue cue) => cue != null && cue.clips != null && cue.clips.Length > 0;
    private AudioClip LegacyClip(GameplaySfx sound) => sound switch
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

    /// <summary>One quiet syllable per revealed group; skipped text and punctuation stay silent.</summary>
    public static void TypeCharacters(string text, int start, int end, bool characterVoice)
    {
        if (string.IsNullOrEmpty(text)) return;
        for (int i = Mathf.Max(0, start); i < Mathf.Min(text.Length, end); i++)
        {
            if (char.IsWhiteSpace(text[i]) || char.IsPunctuation(text[i]) || char.IsControl(text[i])) continue;
            GameEventCenter.RequestSfx(characterVoice ? GameplaySfx.CharacterVoice : GameplaySfx.DialogueTyping);
            return;
        }
    }
}
