using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent screen fade and additive level scene loader.</summary>
public sealed class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }
    [SerializeField, HideInInspector] private CanvasGroup fadeGroup;
    private bool transitioning;
    private const float FadeDuration = 0.28f;

    public static SceneTransitionManager Create()
    {
        if (Instance != null) return Instance;
        // 重载脚本会清空静态引用，接回已有管理器，避免重复创建遮罩。
        Instance = FindAnyObjectByType<SceneTransitionManager>();
        if (Instance != null) return Instance;
        var root = new GameObject("Scene Transition Manager");
        return root.AddComponent<SceneTransitionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeOverlay();
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (Instance != this) return;
        transitioning = false;
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void FadeAction(Action action)
    {
        StartTransition(CallbackRoutine(action));
    }
    public void LoadLevelAdditive(string sceneName, Action completed = null)
    {
        if (!CanLoad(sceneName)) return;
        StartTransition(LoadAdditiveRoutine(sceneName, completed));
    }

    public void ReloadLevelAdditive(string sceneName, Action completed = null)
    {
        if (!CanLoad(sceneName)) return;
        StartTransition(ReloadAdditiveRoutine(sceneName, completed));
    }

    public void UnloadLevelAdditive(string sceneName, Action completed = null)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            completed?.Invoke();
            return;
        }
        StartTransition(UnloadAdditiveRoutine(sceneName, completed));
    }

    private void StartTransition(IEnumerator routine)
    {
        if (!transitioning) StartCoroutine(TransitionRoutine(routine));
    }

    private IEnumerator TransitionRoutine(IEnumerator routine)
    {
        transitioning = true;
        yield return Fade(1f);
        yield return routine;
        yield return Fade(0f);
        transitioning = false;
    }

    private static IEnumerator CallbackRoutine(Action callback)
    {
        callback?.Invoke();
        yield break;
    }
    private IEnumerator LoadAdditiveRoutine(string sceneName, Action completed)
    {
        var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (operation != null) yield return operation;
        completed?.Invoke();
    }

    private IEnumerator ReloadAdditiveRoutine(string sceneName, Action completed)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            var unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }
        var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (load != null) yield return load;
        completed?.Invoke();
    }

    private IEnumerator UnloadAdditiveRoutine(string sceneName, Action completed)
    {
        var operation = SceneManager.UnloadSceneAsync(sceneName);
        if (operation != null) yield return operation;
        completed?.Invoke();
    }

    private IEnumerator Fade(float target)
    {
        var start = fadeGroup.alpha;
        var elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / FadeDuration));
            yield return null;
        }
        fadeGroup.alpha = target;
    }

    private static bool CanLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"关卡场景“{sceneName}”不可加载。请先加入 Build Profiles / Scenes In Build。 ");
            return false;
        }
        return true;
    }

    private void BuildFadeOverlay()
    {
        var canvasObject = new GameObject("Scene Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        fadeGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        var overlay = new GameObject("Black Fade", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvasObject.transform, false);
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = Color.black;
        overlay.GetComponent<Image>().raycastTarget = false;
    }
}
