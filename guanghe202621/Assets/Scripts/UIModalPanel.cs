using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>屏幕弹窗的层级和交互管理。打开时盖在所属菜单前面，并暂停悬浮提示。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class UIModalPanel : MonoBehaviour
{
    [SerializeField, InspectorName("默认选中的按钮（可选）")] private Button defaultSelectedButton;
    private GameObject previousSelection;
    private bool selectReturnButton;

    private void OnEnable()
    {
        transform.SetAsLastSibling();
        // 全屏底板拦截鼠标，避免点击或悬浮落到后面的道具和菜单上。
        GetComponent<Image>().raycastTarget = true;
        TooltipManager.SetSuppressed(this, true);
        foreach (Text label in GetComponentsInChildren<Text>(true)) TooltipManager.ApplyFallbackFont(label);
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        selectReturnButton = true;
    }

    private void LateUpdate()
    {
        if (!selectReturnButton) return;
        selectReturnButton = false;
        // 设置/说明默认选中返回；确认弹窗可指定取消，避免回车误触重置或退出。
        var button = defaultSelectedButton != null ? defaultSelectedButton : GetComponentInChildren<Button>();
        if (button != null && EventSystem.current != null && !EventSystem.current.alreadySelecting)
            button.Select();
    }

    private void OnDisable()
    {
        TooltipManager.SetSuppressed(this, false);
        if (EventSystem.current != null && !EventSystem.current.alreadySelecting)
        {
            bool canRestore = previousSelection != null && previousSelection.activeInHierarchy;
            EventSystem.current.SetSelectedGameObject(canRestore ? previousSelection : null);
        }
        previousSelection = null;
    }

    private void OnDestroy() => TooltipManager.SetSuppressed(this, false);
}
