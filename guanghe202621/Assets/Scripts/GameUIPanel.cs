using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>保存在预置体里的面板身份和功能引用，改名、换图、调布局不会丢失按钮用途。</summary>
[DisallowMultipleComponent]
public sealed class GameUIPanel : MonoBehaviour
{
    public enum PanelKind
    {
        [InspectorName("主菜单")] MainMenu,
        [InspectorName("暂停面板")] PausePanel,
        [InspectorName("游戏界面")] HudPanel
    }

    public enum ElementKey
    {
        [InspectorName("开始游戏按钮")] StartButton,
        [InspectorName("主菜单设置按钮")] SettingsButton,
        [InspectorName("主菜单说明按钮")] HelpButton,
        [InspectorName("版本信息按钮")] VersionButton,
        [InspectorName("主菜单退出按钮")] ExitButton,
        [InspectorName("继续游戏按钮")] ResumeButton,
        [InspectorName("暂停设置按钮")] PauseSettingsButton,
        [InspectorName("暂停说明按钮")] PauseHelpButton,
        [InspectorName("重置关卡按钮")] ResetButton,
        [InspectorName("返回主菜单按钮")] ReturnToMenuButton,
        [InspectorName("暂停按钮")] PauseButton,
        [InspectorName("确认按钮")] ConfirmButton,
        [InspectorName("取消按钮")] CancelButton,
        [InspectorName("关闭按钮")] CloseButton,
        [InspectorName("台词继续按钮")] DialogueContinueButton,
        [InspectorName("重试按钮")] RetryButton,
        [InspectorName("结果返回菜单按钮")] ResultMenuButton,
        [InspectorName("音乐音量滑条")] MusicVolumeSlider,
        [InspectorName("音效音量滑条")] SfxVolumeSlider,
        [InspectorName("确认弹窗")] ConfirmationPanel,
        [InspectorName("设置面板")] SettingsPanel,
        [InspectorName("说明面板")] HelpPanel,
        [InspectorName("版本面板")] VersionPanel,
        [InspectorName("对话面板")] DialoguePanel,
        [InspectorName("结果面板")] ResultPanel,
        [InspectorName("确认提示文字")] MessageText,
        [InspectorName("角色名文字")] SpeakerText,
        [InspectorName("台词文字")] DialogueText,
        [InspectorName("结果标题")] ResultTitle,
        [InspectorName("面板标题")] Title
    }

    [Serializable]
    public sealed class ElementReference
    {
        [InspectorName("功能")] public ElementKey key;
        [InspectorName("界面对象")] public GameObject target;
    }

    [SerializeField, InspectorName("面板类型")] private PanelKind panelKind;
    [SerializeField, InspectorName("功能对象引用")]
    private List<ElementReference> elements = new List<ElementReference>();

    public PanelKind Kind => panelKind;

    /// <summary>只返回当前查找范围中的对象，多个弹窗里的“关闭”按钮互不混淆。</summary>
    public GameObject FindElement(string elementName, Transform scope)
    {
        if (!Enum.TryParse(elementName, out ElementKey key)) return null;
        foreach (var entry in elements)
        {
            if (entry == null || entry.key != key || entry.target == null) continue;
            Transform item = entry.target.transform;
            if (item == scope || item.IsChildOf(scope)) return entry.target;
        }
        return null;
    }
}
