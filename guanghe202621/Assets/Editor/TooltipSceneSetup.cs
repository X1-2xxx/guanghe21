using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>给当前已打开的模板场景补上新增预制体，保留美术尚未保存的场景修改。</summary>
[InitializeOnLoad]
public static class TooltipSceneSetup
{
    static TooltipSceneSetup()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += EnsureSampleSceneTooltip;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureSampleSceneTooltip;
        };
    }

    private static void EnsureSampleSceneTooltip()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        Canvas targetCanvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<TooltipManager>(true);
            if (existing != null)
            {
                // 共享提示通过透明度隐藏，不能关掉整个根物体，否则所有面板都会失去提示。
                if (!existing.gameObject.activeSelf)
                {
                    Undo.RecordObject(existing.gameObject, "启用共享悬浮提示");
                    existing.gameObject.SetActive(true);
                    if (PrefabUtility.IsPartOfPrefabInstance(existing.gameObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(existing.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                var visibility = existing.GetComponent<CanvasGroup>();
                if (visibility != null && visibility.alpha != 0f)
                {
                    Undo.RecordObject(visibility, "隐藏悬浮提示预览");
                    visibility.alpha = 0f;
                    if (PrefabUtility.IsPartOfPrefabInstance(visibility))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(visibility);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                return;
            }
            if (root.TryGetComponent<Canvas>(out var canvas)) targetCanvas = canvas;
        }
        if (targetCanvas == null) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/TooltipPanel.prefab");
        if (prefab == null) return;
        // 只补这一份预制体，不重建旧 UI、不自动保存场景，也不改变现有物体的布局。
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, targetCanvas.transform);
        Undo.RegisterCreatedObjectUndo(instance, "添加通用悬浮提示");
        // Canvas 在编辑器中可能暂时为零尺寸；明确保留预制体根节点的原始布局。
        var rect = instance.GetComponent<RectTransform>();
        var sourceRect = prefab.GetComponent<RectTransform>();
        rect.anchorMin = sourceRect.anchorMin;
        rect.anchorMax = sourceRect.anchorMax;
        rect.pivot = sourceRect.pivot;
        rect.sizeDelta = sourceRect.sizeDelta;
        rect.anchoredPosition = sourceRect.anchoredPosition;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        instance.GetComponent<CanvasGroup>().alpha = 0f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<CanvasGroup>());
        instance.transform.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
