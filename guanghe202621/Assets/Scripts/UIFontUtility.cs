using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>整个项目的 UGUI 文字统一使用 UI 文件夹中指定的字体资产。</summary>
public static class UIFontUtility
{
    public const string FontAssetPath = "Assets/Resources/UI/UI/ZhaiZaiJiaFenTiaoTian-2.ttf";
    private const string ResourcePath = "UI/UI/ZhaiZaiJiaFenTiaoTian-2";
    private static Font cachedFont;
    private static bool reportedMissing;
    private static bool sceneHookRegistered;

    public static Font GetFont()
    {
        // 运行中编译后重新接回场景通知，后续加载的 UI 仍保持同一字体。
        if (Application.isPlaying && !sceneHookRegistered) RegisterScenes();
        if (cachedFont == null) cachedFont = Resources.Load<Font>(ResourcePath);
        if (cachedFont == null && !reportedMissing)
        {
            reportedMissing = true;
            Debug.LogError("统一 UI 字体丢失，请检查：" + FontAssetPath);
        }
        return cachedFont;
    }

    /// <summary>只修改字体引用，保留美术调整的字号、颜色、文字内容及布局。</summary>
    public static void Apply(Text target)
    {
        if (target == null) return;
        Font font = GetFont();
        if (font != null && target.font != font) target.font = font;
    }

    public static void ApplyToHierarchy(GameObject root)
    {
        if (root == null) return;
        foreach (Text text in root.GetComponentsInChildren<Text>(true)) Apply(text);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) ApplyToHierarchy(root);
    }
}
