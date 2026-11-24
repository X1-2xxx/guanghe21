using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>恢复模板场景中被意外关闭的 UI 控制器，保留已有预制体和美术修改。</summary>
[InitializeOnLoad]
public static class GameFlowSceneSetup
{
    static GameFlowSceneSetup()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += RestoreController;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += RestoreController;
        };
    }

    private static void RestoreController()
    {
        foreach (GameFlowTemplate flow in Object.FindObjectsByType<GameFlowTemplate>(FindObjectsInactive.Include))
        {
            if (flow.gameObject.scene.path != "Assets/Scenes/SampleScene.unity" || flow.enabled) continue;
            if (!Application.isPlaying) Undo.RecordObject(flow, "恢复游戏 UI 控制器");
            flow.enabled = true;
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(flow);
                EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
            }
        }
    }
}
