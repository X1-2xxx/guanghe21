using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>直接编辑实际 Text；切换预览或选中文案子物体时自动显示对应内容。</summary>
[CustomEditor(typeof(ConfirmationDialog))]
public sealed class ConfirmationDialogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var dialog = (ConfirmationDialog)target;
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewKind"));
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();
        if (changed) Preview(dialog);

        GameObject content = dialog.GetContent(dialog.PreviewKind);
        EditorGUILayout.HelpBox("下方标题、正文就是预制体中实际的 Text 内容，直接填写即可。也可以展开文案物体，选中 Title / MessageText 修改；选中哪组就会预览哪组。", MessageType.Info);
        if (content != null)
        {
            DrawText(content.transform.Find("Title"), "标题");
            DrawText(content.transform.Find("MessageText"), "正文");
        }
        else EditorGUILayout.HelpBox("这份弹窗没有所选文案物体，请检查下方关联或预制体是否已经保存。", MessageType.Warning);

        if (GUILayout.Button("显示所选文案预览")) Preview(dialog);
        if (GUILayout.Button("选中正文 MessageText")) SelectText(dialog, "MessageText");
        if (GUILayout.Button("选中标题 Title")) SelectText(dialog, "Title");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sections"), new GUIContent("文案物体关联（已配置）"), true);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawText(Transform item, string label)
    {
        if (item == null || !item.TryGetComponent<Text>(out var text)) return;
        using (var textData = new SerializedObject(text))
        {
            textData.Update();
            EditorGUILayout.PropertyField(textData.FindProperty("m_Text"), new GUIContent(label), true);
            if (textData.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
        }
    }

    // 全局运行中也允许预览 Prefab 模式：它是资产编辑场景，不是正在玩的场景实例。
    private static void Preview(ConfirmationDialog dialog)
    {
        if (Application.IsPlaying(dialog.gameObject))
        {
            Debug.LogWarning("这是正在运行的场景实例。请停止运行后编辑，或双击预制体进入 Prefab 模式。", dialog);
            return;
        }
        GameObject selected = dialog.GetContent(dialog.PreviewKind);
        if (selected == null) return;
        bool needsChange = !dialog.gameObject.activeSelf || !selected.activeSelf;
        foreach (ConfirmationDialog.Kind kind in System.Enum.GetValues(typeof(ConfirmationDialog.Kind)))
        {
            GameObject content = dialog.GetContent(kind);
            if (content != null && content != selected && content.activeSelf) needsChange = true;
        }
        if (!needsChange) return;
        Undo.RecordObject(dialog.gameObject, "预览确认弹窗");
        foreach (Transform item in dialog.transform) Undo.RecordObject(item.gameObject, "预览确认文案");
        foreach (var section in dialog.Sections)
            if (section != null && section.textRoot != null) Undo.RecordObject(section.textRoot, "预览确认文案");
        if (!dialog.Show(dialog.PreviewKind)) return;
        dialog.gameObject.SetActive(true);
        RecordPreview(dialog.gameObject);
        foreach (Transform item in dialog.transform) RecordPreview(item.gameObject);
        foreach (var section in dialog.Sections)
            if (section != null && section.textRoot != null) RecordPreview(section.textRoot);
        EditorSceneManager.MarkSceneDirty(dialog.gameObject.scene);
        SceneView.RepaintAll();
    }

    private static void RecordPreview(GameObject item)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
    }

    private static void SelectText(ConfirmationDialog dialog, string childName)
    {
        GameObject content = dialog.GetContent(dialog.PreviewKind);
        if (content == null) return;
        Transform text = content.transform.Find(childName);
        if (text == null) return;
        Selection.activeGameObject = text.gameObject;
        EditorGUIUtility.PingObject(text.gameObject);
    }

    /// <summary>从 Hierarchy 选择灰色文案子物体时，自动激活它所属的一组，不修改文字。</summary>
    public static void PreviewSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || Application.IsPlaying(selected)) return;
        var dialog = selected.GetComponentInParent<ConfirmationDialog>(true);
        if (dialog == null) return;
        foreach (ConfirmationDialog.Kind kind in System.Enum.GetValues(typeof(ConfirmationDialog.Kind)))
        {
            GameObject content = dialog.GetContent(kind);
            if (content == null || (selected != content && !selected.transform.IsChildOf(content.transform))) continue;
            if (dialog.PreviewKind != kind)
            {
                using (var data = new SerializedObject(dialog))
                {
                    data.FindProperty("previewKind").enumValueIndex = (int)kind;
                    data.ApplyModifiedProperties();
                }
            }
            Preview(dialog);
            return;
        }
    }
}

/// <summary>只在美术编辑预制体或未运行的场景时生效，不干预游戏中的文案切换。</summary>
[InitializeOnLoad]
public static class ConfirmationDialogSelectionPreview
{
    static ConfirmationDialogSelectionPreview()
    {
        Selection.selectionChanged += ConfirmationDialogEditor.PreviewSelection;
        if (!Application.isBatchMode) EditorApplication.delayCall += ConfirmationDialogEditor.PreviewSelection;
    }
}
