using System;

public enum GameplaySfx
{
    TurnCharacterSwitch,
    Footstep,
    Dash,
    JumpTakeoff,
    JumpLanding,
    SpringBounce,
    ThinBoardBreak,
    PhysicsBlockImpact,
    PhysicsBlockAttach,
    PhysicsBlockDetach,
    PauseMenuOpen, PauseMenuClose, DialogueOptionClick, SpeechBubblePop,
    DialogueTyping, CharacterVoice, CampfireSave
}

/// <summary>Typed message hub for decoupled gameplay systems.</summary>
public static class GameEventCenter
{
    public static event Action<GameState> StateChanged;
    public static event Action<string, string> DialogueRequested;
    public static event Action LevelReset;
    public static event Action<GameplaySfx> SfxRequested;

    public static void PublishState(GameState state) => StateChanged?.Invoke(state);
    public static void RequestDialogue(string speaker, string line) => DialogueRequested?.Invoke(speaker, line);
    public static void PublishLevelReset() => LevelReset?.Invoke();
    public static void RequestSfx(GameplaySfx sound) => SfxRequested?.Invoke(sound);
}
