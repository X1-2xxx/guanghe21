using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>挂到有 Image/Text 射线区域的 UI 物体上，只配置提示文字和解锁状态。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    [TextArea(2, 6), InspectorName("提示内容")] public string tipText;
    [InspectorName("已经解锁")] public bool isUnlocked = true;

    public string DisplayText => isUnlocked ? tipText : "【尚未解锁】";

    private void OnEnable()
    {
        // 名称是预制体中已有的 Text，只统一字体，不创建或改写名称内容。
        foreach (Text label in GetComponentsInChildren<Text>(true)) TooltipManager.ApplyFallbackFont(label);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) return;
        if (isUnlocked && TryGetComponent<ItemSlotUI>(out var item) && item.ItemId == GameItemId.Cardboard)
            GamePresentation.PresentationEvents.Signal("inventory.cardboard", this);
        if (TooltipManager.Instance != null) TooltipManager.Instance.Show(this, eventData);
        else Debug.LogWarning("请把 TooltipPanel 预制体放到 Canvas 下，与主菜单和暂停面板同级。", this);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null) return;
        // 弹窗关闭时鼠标可能仍留在原区域；允许移动时补回被屏蔽的那次移入。
        if (!TooltipManager.Instance.IsVisible && !TooltipManager.IsSuppressed) TooltipManager.Instance.Show(this, eventData);
        else TooltipManager.Instance.Move(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData) => HideOwnTooltip();

    // 父面板关闭、物体被禁用或销毁，都会走这里，不需要业务脚本手动补隐藏代码。
    private void OnDisable() => HideOwnTooltip();
    private void OnDestroy() => HideOwnTooltip();

    private void HideOwnTooltip()
    {
        if (TooltipManager.Instance != null) TooltipManager.Instance.Hide(this);
    }
}
