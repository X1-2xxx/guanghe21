using System.Reflection;
using LevelKit;
using UnityEditor;
using UnityEngine;

internal static class InnPlaytestShortcut
{
    private const string Menu = "Level Kit/旅馆/跳到存档点5（测试）";
    [MenuItem(Menu)]
    public static void JumpToStart()
    {
        var player = Object.FindFirstObjectByType<PlayerActionController>();
        var fire = GameObject.Find("存档点5")?.GetComponent<Campfire>();
        if (!player || !fire) return;
        Invoke(player, "StopWalking");
        Invoke(player, "SetCheckpoint", fire, true);
        Invoke(player, "UnlockJump");
        Invoke(player, "ReturnToCheckpoint");
        Debug.Log("已跳到存档点5，并为本次试跑解锁跳跃。继续向右即可进入旅馆；停止运行后恢复原始场景配置。", player);
    }
    [MenuItem(Menu, true)]
    private static bool Available() => EditorApplication.isPlaying && GameObject.Find("旅馆三关");
    private static void Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
}
