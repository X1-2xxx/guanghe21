using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>把已打开场景里残留的旧字体引用换成项目字体，保留未保存的美术修改。</summary>
[InitializeOnLoad]
public static class UIFontSceneSetup
{
    static UIFontSceneSetup()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += ApplyToOpenScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += ApplyToOpenScenes;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ApplyToOpenScenes;
        };
    }

    private static void ApplyToOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(UIFontUtility.FontAssetPath);
        if (font == null) return;
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            // 恢复用的备份场景保持原样。
            if (!scene.isLoaded || !scene.path.StartsWith("Assets/Scenes/")) continue;
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font == font) continue;
                    Undo.RecordObject(text, "统一 UI 字体");
                    text.font = font;
                    if (PrefabUtility.IsPartOfPrefabInstance(text))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                    changed = true;
                }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
