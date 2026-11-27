using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio Library", fileName = "GameAudioLibrary")]
public sealed class GameAudioLibrary : ScriptableObject
{
    public AudioClip menuMusic, gameplayMusic;
    [Range(0f, 1f)] public float musicGain = .45f;
    public Cue[] cues = Array.Empty<Cue>();

    [Serializable]
    public sealed class Cue
    {
        public GameplaySfx sound;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = .8f;
        [Min(0f)] public float cooldown = .08f;
    }

    public Cue Find(GameplaySfx sound)
    {
        foreach (var cue in cues) if (cue != null && cue.sound == sound) return cue;
        return null;
    }
}
