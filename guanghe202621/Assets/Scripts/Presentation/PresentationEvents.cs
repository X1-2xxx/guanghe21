using UnityEngine;

namespace GamePresentation
{
    /// <summary>玩法只报告已发生的事实，演出系统不决定物理、移动或任务结果。</summary>
    public enum PlayEvent
    {
        RunStarted, StopBySpace, StopByClick, UnderfootDenied, RunningEditDenied,
        Turned, CheckpointReset, PitDeath, Jumped, Bounced, JumpAcquired, ShoesAcquired,
        WallBlocked, WallBroken, TapeConnected, TapeRemoved, BlockGrabbed, CheckpointReached,
        Falling, StuckInPit, BridgeShook, CrookedBridgePassed, SupportCollapsed, TreeNearMiss, Idle
    }

    public static class PresentationEvents
    {
        public static bool BlocksInput => PresentationRuntime.Instance != null && PresentationRuntime.Instance.Director.BlocksInput;
        public static bool ResetOnly => PresentationRuntime.Instance != null && PresentationRuntime.Instance.Director.ResetOnly;
        public static bool BlocksRun => PresentationRuntime.Instance != null && PresentationRuntime.Instance.BlocksRun;
        public static bool CameraOwned => PresentationRuntime.Instance != null && PresentationRuntime.Instance.Director.CameraBusy;

        public static void Report(PlayEvent kind, Component actor, Component subject = null, float value = 0f)
        {
            if (PresentationRuntime.Instance != null) PresentationRuntime.Instance.Observe(kind, actor, subject, value);
        }
        public static void Signal(string id, Component source = null)
        {
            if (PresentationRuntime.Instance != null) PresentationRuntime.Instance.Signal(id, source);
        }
        public static bool CatchOpeningFall(Component player)
        {
            return PresentationRuntime.Instance != null && PresentationRuntime.Instance.CatchOpeningFall(player);
        }
    }
}
