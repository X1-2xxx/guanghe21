using System;
using UnityEngine;

/// <summary>确认弹窗只切换预制体中的文案物体，不在代码里拼接或覆盖 Text 内容。</summary>
[DisallowMultipleComponent]
public sealed class ConfirmationDialog : MonoBehaviour
{
    public enum Kind
    {
        [InspectorName("重置关卡")] ResetLevel = 0,
        [InspectorName("返回主菜单")] ReturnToMenu = 1,
        [InspectorName("退出游戏")] ExitGame = 2
    }

    [Serializable]
    public sealed class Section
    {
        [InspectorName("对应操作")] public Kind kind;
        [InspectorName("文案子物体")] public GameObject textRoot;
    }

    [Header("编辑预览：实际文字在下方子物体的 Text 组件中修改")]
    [SerializeField, InspectorName("预览哪组文案")] private Kind previewKind;
    [Header("文案物体关联（已配置）")]
    [SerializeField] private Section[] sections = Array.Empty<Section>();

    public Kind PreviewKind => previewKind;
    public Section[] Sections => sections ?? Array.Empty<Section>();

    public GameObject GetContent(Kind kind)
    {
        foreach (Section section in Sections)
            if (section != null && section.kind == kind && BelongsTo(section.textRoot, transform)) return section.textRoot;
        // 旧实例在运行中更新、或手动丢失关联时，接回当前弹窗已有的文案，不创建 UI。
        return FindContent(transform, kind);
    }

    /// <summary>只选择对应文案，标题、正文、字号和布局完全沿用美术编辑的预制体。</summary>
    public bool Show(Kind kind)
    {
        return ShowContent(transform, GetContent(kind), Sections);
    }

    /// <summary>兼容缺少新增组件的旧场景实例，仍只显示它自己已有的预制体文案。</summary>
    public static bool TryShow(GameObject panel, Kind kind)
    {
        if (panel == null) return false;
        var dialog = panel.GetComponent<ConfirmationDialog>();
        return dialog != null
            ? dialog.Show(kind)
            : ShowContent(panel.transform, FindContent(panel.transform, kind), Array.Empty<Section>());
    }

    private static GameObject FindContent(Transform panel, Kind kind)
    {
        string groupName;
        switch (kind)
        {
            case Kind.ResetLevel: groupName = "重置关卡文案"; break;
            case Kind.ReturnToMenu: groupName = "返回主菜单文案"; break;
            case Kind.ExitGame: groupName = "退出游戏文案"; break;
            default: return null;
        }
        Transform content = panel.Find(groupName);
        return content != null ? content.gameObject : null;
    }

    private static bool BelongsTo(GameObject content, Transform panel) =>
        content != null && content.transform != panel && content.transform.IsChildOf(panel);

    private static bool ShowContent(Transform panel, GameObject selected, Section[] entries)
    {
        if (!BelongsTo(selected, panel)) return false;
        foreach (Section section in entries)
            if (section != null && BelongsTo(section.textRoot, panel)) section.textRoot.SetActive(section.textRoot == selected);
        foreach (Kind kind in Enum.GetValues(typeof(Kind)))
        {
            GameObject content = FindContent(panel, kind);
            if (content != null) content.SetActive(content == selected);
        }
        selected.SetActive(true);
        return true;
    }
}
