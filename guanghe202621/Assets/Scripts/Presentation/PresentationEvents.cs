using UnityEngine;

namespace GamePresentation
{
    /// <summary>玩法只报告已发生的事实，演出系统不决定物理、移动或任务结果。</summary>
    public enum PlayEvent
    {
        RunStarted, StopBySpace, StopByClick, UnderfootDenied, RunningEditDenied,
        Turned, CheckpointReset, PitDeath, Jumped, Bounced, JumpAcquired, ShoesAcquired,
        WallBlocked, WallBroken, TapeConnected, TapeRemoved, BlockGrabbed, CheckpointReached,
        Falling, StuckInPit, BridgeShook, CrookedBridgePassed, SupportCollapsed, TreeNearMiss, Idle,
        InnVaseCollected, InnChocolateCollected, InnPortraitCollected
    }

    public static class PresentationEvents
    {
        public static bool BlocksInput => PresentationRuntime.Instance != null && PresentationRuntime.Instance.Director.BlocksInput;
        public static bool CheckpointResetAvailable => PresentationRuntime.Instance == null || PresentationRuntime.Instance.CheckpointResetAvailable;
        public static bool ResetOnly => PresentationRuntime.Instance != null && PresentationRuntime.Instance.Director.ResetOnly;
        public static bool CameraOwned => PresentationRuntime.Instance != null && PresentationRuntime.Instance.Director.CameraBusy;

        public static void Report(PlayEvent kind, Component actor, Component subject = null, float value = 0f)
        {
            // Audio responds to successful gameplay facts even without a story runtime.
            switch (kind)
            {
                case PlayEvent.Turned: GameEventCenter.RequestSfx(GameplaySfx.TurnCharacterSwitch); break;
                case PlayEvent.Bounced: GameEventCenter.RequestSfx(GameplaySfx.SpringBounce); break;
                case PlayEvent.WallBroken: GameEventCenter.RequestSfx(GameplaySfx.ThinBoardBreak); break;
                case PlayEvent.TapeConnected: GameEventCenter.RequestSfx(GameplaySfx.PhysicsBlockAttach); break;
                case PlayEvent.TapeRemoved: GameEventCenter.RequestSfx(GameplaySfx.PhysicsBlockDetach); break;
                case PlayEvent.CheckpointReached: GameEventCenter.RequestSfx(GameplaySfx.CampfireSave); break;
            }
            if (PresentationRuntime.Instance != null) PresentationRuntime.Instance.Observe(kind, actor, subject, value);
        }
        public static void Signal(string id, Component source = null)
        {
            if (PresentationRuntime.Instance != null) PresentationRuntime.Instance.Signal(id, source);
        }
    }
}
