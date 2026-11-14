using UnityEngine;

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    Victory,
    Defeat
}

/// <summary>Central game state and pause-time controller.</summary>
public static class GameStateManager
{
    public static GameState Current { get; private set; } = GameState.MainMenu;

    public static void SetState(GameState next)
    {
        if (Current == next) return;
        Current = next;
        Time.timeScale = next == GameState.Paused ? 0f : 1f;
        GameEventCenter.PublishState(next);
    }
}
