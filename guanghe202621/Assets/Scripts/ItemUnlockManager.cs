using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>保存本次游戏的道具解锁状态。拾取脚本调用静态接口，无需查找暂停面板。</summary>
[DisallowMultipleComponent]
public sealed class ItemUnlockManager : MonoBehaviour
{
    private static ItemUnlockManager instance;

    // 用序列化列表保留运行中编译前的状态；不写 PlayerPrefs，不修改预制体资产。
    [SerializeField, InspectorName("本次游戏已解锁的道具")]
    private List<GameItemId> unlockedItems = new List<GameItemId>();

    /// <summary>道具状态真正改变时通知，重复拾取不会重复广播。</summary>
    public static event Action<GameItemId, bool> UnlockStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void PrepareSession()
    {
        // 兼容关闭域重载的播放模式。具体状态仍保存在运行时管理器的序列化字段中。
        instance = null;
        UnlockStateChanged = null;
    }

    private void Awake() => RegisterInstance();
    private void OnEnable() => RegisterInstance();

    private void RegisterInstance()
    {
        if (instance != null && instance != this)
        {
            // 只移除重复组件，避免误删其他程序挂在同一物体上的相机或玩法组件。
            enabled = false;
            Destroy(this);
            return;
        }
        instance = this;
        unlockedItems ??= new List<GameItemId>();
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }

    private static ItemUnlockManager GetOrCreate()
    {
        if (instance != null) return instance;
        // 运行中编译后先接回原对象，保证已经拾取的道具不丢失。
        instance = FindAnyObjectByType<ItemUnlockManager>();
        if (instance != null) return instance;
        // 这是独立的数据管理器，不创建 UI；即使暂停面板尚未打开也能接收拾取结果。
        return new GameObject("Item Unlock Manager").AddComponent<ItemUnlockManager>();
    }

    /// <summary>拾取成功时调用。首次解锁返回 true，已经拥有时返回 false。</summary>
    public static bool Unlock(GameItemId item) => SetUnlocked(item, true);

    /// <summary>查询能力或 UI 状态，硬纸板由剧情教学解锁。</summary>
    public static bool IsUnlocked(GameItemId item)
    {
        if (!IsKnownItem(item)) return false;
        if (!Application.isPlaying) return false;
        return GetOrCreate().unlockedItems.Contains(item);
    }

    /// <summary>设置状态并通知 UI；允许新游戏重新锁定硬纸板。</summary>
    public static bool SetUnlocked(GameItemId item, bool unlocked)
    {
        if (!IsKnownItem(item))
        {
            Debug.LogWarning($"未知道具标识：{item}");
            return false;
        }
        if (!Application.isPlaying) return false;
        var manager = GetOrCreate();
        bool previous = manager.unlockedItems.Contains(item);
        if (previous == unlocked) return false;
        if (unlocked) manager.unlockedItems.Add(item);
        else manager.unlockedItems.RemoveAll(value => value == item);
        UnlockStateChanged?.Invoke(item, unlocked);
        return true;
    }

    /// <summary>开始全新的一局时由玩法调用，回到所有道具尚未解锁的初始状态。</summary>
    public static void ResetProgress()
    {
        if (!Application.isPlaying) return;
        foreach (GameItemId item in Enum.GetValues(typeof(GameItemId)))
            SetUnlocked(item, false);
    }

    private static bool IsKnownItem(GameItemId item) => Enum.IsDefined(typeof(GameItemId), item);

    private void OnDestroy() { if (instance == this) instance = null; }
}
