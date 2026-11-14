using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A small, self-contained menu and pause flow that can be dropped into a scene.
/// The gameplay view is an intentionally simple placeholder for the real game scene.
/// </summary>
public sealed class GameFlowTemplate : MonoBehaviour
{
    private enum ScreenState { MainMenu, Playing, Paused }

    private static readonly Color Background = new Color(0.055f, 0.075f, 0.11f);
    private static readonly Color Panel = new Color(0.09f, 0.12f, 0.17f, 0.97f);
    private static readonly Color Accent = new Color(1f, 0.56f, 0.28f);
    private static readonly Color Muted = new Color(0.66f, 0.72f, 0.79f);

    private ScreenState state;
    private Canvas canvas;
    private GameObject mainMenu;
    private GameObject gameHud;
    private GameObject pauseMenu;

    private void Awake()
    {
        BuildInterface();
        ShowMainMenu();
    }

    private void Update()
    {
        if (WasPausePressed())
        {
            if (state == ScreenState.Playing) ShowPauseMenu();
            else if (state == ScreenState.Paused) ResumeGame();
        }
    }

    private bool WasPausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void BuildInterface()
    {
        var canvasObject = new GameObject("Game Flow UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        mainMenu = CreateScreen("Main Menu", canvas.transform);
        BuildMainMenu(mainMenu.transform);

        gameHud = CreateScreen("Game HUD", canvas.transform);
        BuildGameHud(gameHud.transform);

        pauseMenu = CreateScreen("Pause Menu", canvas.transform);
        BuildPauseMenu(pauseMenu.transform);
    }

    private GameObject CreateScreen(string name, Transform parent)
    {
        var screen = new GameObject(name, typeof(RectTransform));
        screen.transform.SetParent(parent, false);
        Stretch(screen.GetComponent<RectTransform>());
        return screen;
    }

    private void BuildMainMenu(Transform root)
    {
        AddImage(root, "Background", Background, Vector2.zero, Vector2.one);
        AddImage(root, "Accent bar", Accent, new Vector2(0.08f, 0.18f), new Vector2(0.085f, 0.82f));

        AddText(root, "GAME TEMPLATE", 22, Accent, TextAnchor.MiddleLeft,
            new Vector2(0.13f, 0.69f), new Vector2(0.57f, 0.78f));
        AddText(root, "你的游戏名称", 76, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.13f, 0.48f), new Vector2(0.72f, 0.70f));
        AddText(root, "从这里开始，替换成你的游戏内容。", 26, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.135f, 0.40f), new Vector2(0.72f, 0.48f));

        CreateButton(root, "开始游戏", new Vector2(0.135f, 0.27f), new Vector2(0.31f, 0.36f), Accent, StartGame);
        CreateButton(root, "退出游戏", new Vector2(0.325f, 0.27f), new Vector2(0.50f, 0.36f),
            new Color(0.16f, 0.20f, 0.27f), ExitGame);
        AddText(root, "操作提示   ESC 暂停 / 继续", 20, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.135f, 0.12f), new Vector2(0.50f, 0.19f));

        var card = AddImage(root, "Flow card", Panel, new Vector2(0.67f, 0.25f), new Vector2(0.88f, 0.76f));
        AddText(root, "基础流程", 30, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.71f, 0.65f), new Vector2(0.84f, 0.71f));
        AddText(root, "01   主菜单进入游戏\n\n02   游戏中按 ESC 暂停\n\n03   继续游戏或返回主菜单\n\n04   随时退出应用", 23, Muted,
            TextAnchor.UpperLeft, new Vector2(0.71f, 0.39f), new Vector2(0.85f, 0.63f));
        card.raycastTarget = false;
    }

    private void BuildGameHud(Transform root)
    {
        AddImage(root, "Placeholder game background", new Color(0.10f, 0.16f, 0.20f), Vector2.zero, Vector2.one);
        AddImage(root, "World floor", new Color(0.13f, 0.22f, 0.24f), new Vector2(0f, 0f), new Vector2(1f, 0.22f));
        AddText(root, "游戏场景占位", 42, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.60f), new Vector2(0.75f, 0.72f));
        AddText(root, "已进入游戏。将此区域替换成你的游戏场景。", 22, Muted, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.52f), new Vector2(0.75f, 0.60f));
        CreateButton(root, "暂停  (ESC)", new Vector2(0.82f, 0.88f), new Vector2(0.96f, 0.95f),
            new Color(0.16f, 0.20f, 0.27f), ShowPauseMenu, 18);
        AddText(root, "基础流程模板  ·  游戏中", 18, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.90f), new Vector2(0.42f, 0.95f));
    }

    private void BuildPauseMenu(Transform root)
    {
        AddImage(root, "Dim overlay", new Color(0.015f, 0.025f, 0.04f, 0.76f), Vector2.zero, Vector2.one);
        AddImage(root, "Pause panel", Panel, new Vector2(0.34f, 0.19f), new Vector2(0.66f, 0.81f));
        AddText(root, "游戏暂停", 52, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.37f, 0.68f), new Vector2(0.63f, 0.77f));
        AddText(root, "按 ESC 或选择继续，回到游戏。", 20, Muted, TextAnchor.MiddleCenter,
            new Vector2(0.36f, 0.61f), new Vector2(0.64f, 0.68f));
        CreateButton(root, "继续游戏", new Vector2(0.40f, 0.49f), new Vector2(0.60f, 0.58f), Accent, ResumeGame);
        CreateButton(root, "返回主菜单", new Vector2(0.40f, 0.37f), new Vector2(0.60f, 0.46f),
            new Color(0.16f, 0.20f, 0.27f), ShowMainMenu);
        CreateButton(root, "退出游戏", new Vector2(0.40f, 0.25f), new Vector2(0.60f, 0.34f),
            new Color(0.16f, 0.20f, 0.27f), ExitGame);
    }

    private void StartGame()
    {
        Time.timeScale = 1f;
        state = ScreenState.Playing;
        mainMenu.SetActive(false);
        gameHud.SetActive(true);
        pauseMenu.SetActive(false);
    }

    private void ShowPauseMenu()
    {
        if (state != ScreenState.Playing) return;
        state = ScreenState.Paused;
        Time.timeScale = 0f;
        pauseMenu.SetActive(true);
    }

    private void ResumeGame()
    {
        if (state != ScreenState.Paused) return;
        state = ScreenState.Playing;
        Time.timeScale = 1f;
        pauseMenu.SetActive(false);
    }

    private void ShowMainMenu()
    {
        state = ScreenState.MainMenu;
        Time.timeScale = 1f;
        mainMenu.SetActive(true);
        gameHud.SetActive(false);
        pauseMenu.SetActive(false);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Image AddImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text AddText(Transform parent, string value, int size, Color color, TextAnchor alignment, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(value, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = obj.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void CreateButton(Transform parent, string label, Vector2 min, Vector2 max, Color color,
        UnityEngine.Events.UnityAction action, int fontSize = 24)
    {
        var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = obj.GetComponent<Image>();
        image.color = color;
        var button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = color,
            highlightedColor = Color.Lerp(color, Color.white, 0.16f),
            pressedColor = Color.Lerp(color, Color.black, 0.12f),
            selectedColor = Color.Lerp(color, Color.white, 0.12f),
            disabledColor = color,
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        button.onClick.AddListener(action);
        AddText(obj.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
