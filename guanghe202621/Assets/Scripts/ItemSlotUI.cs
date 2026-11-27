using UnityEngine;

/// <summary>把道具状态映射为问号、道具图标和悬浮说明，不负责拾取或角色能力实现。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TooltipTrigger))]
public sealed class ItemSlotUI : MonoBehaviour
{
    [SerializeField, InspectorName("对应道具")] private GameItemId itemId;
    [SerializeField, InspectorName("未解锁：问号物体")] private GameObject lockedIcon;
    [SerializeField, InspectorName("已解锁：道具图片物体")] private GameObject unlockedIcon;
    [SerializeField, InspectorName("该框的悬浮提示")] private TooltipTrigger tooltip;

    public GameItemId ItemId => itemId;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (tooltip == null) tooltip = GetComponent<TooltipTrigger>();
        // 面板重新打开和运行中编译时重新订阅，不叠加回调。
        ItemUnlockManager.UnlockStateChanged -= OnUnlockStateChanged;
        ItemUnlockManager.UnlockStateChanged += OnUnlockStateChanged;
        Refresh();
    }

    private void OnDisable() => ItemUnlockManager.UnlockStateChanged -= OnUnlockStateChanged;
    private void OnDestroy() => ItemUnlockManager.UnlockStateChanged -= OnUnlockStateChanged;

    private void OnUnlockStateChanged(GameItemId changedItem, bool unlocked)
    {
        if (changedItem == itemId) ApplyState(unlocked);
    }

    /// <summary>打开暂停面板时读取最新状态，支持玩家先拾取、之后才打开 UI。</summary>
    public void Refresh()
    {
        if (Application.isPlaying) ApplyState(ItemUnlockManager.IsUnlocked(itemId));
    }

    private void ApplyState(bool unlocked)
    {
        if (itemId == GameItemId.Cardboard)
        {
            // Restore legacy whole-slot hiding; all three abilities share the question-mark state.
            var visibility = GetComponent<CanvasGroup>();
            if (visibility) { visibility.alpha = 1f; visibility.interactable = visibility.blocksRaycasts = true; }
            if (tooltip != null) tooltip.enabled = true;
        }
        if (lockedIcon != null) lockedIcon.SetActive(!unlocked);
        if (unlockedIcon != null) unlockedIcon.SetActive(unlocked);
        if (tooltip != null) tooltip.isUnlocked = unlocked;
        // 框内名称保持常驻；正在显示的悬浮说明由 TooltipManager 自动刷新。
    }

    /// <summary>如需绑定 UnityEvent，也可以调用本方法；普通拾取脚本建议使用管理器静态接口。</summary>
    [ContextMenu("运行时测试/解锁这个道具")]
    public void Unlock() => ItemUnlockManager.Unlock(itemId);
}
