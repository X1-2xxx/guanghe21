using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Runtime-built, art-free menu/HUD template for Unity 6.6.</summary>
public sealed class GameFlowTemplate : MonoBehaviour
{
    [Header("Level loading")]
    [Tooltip("Optional playable scene name. Leave empty to use the built-in placeholder gameplay screen.")]
    [SerializeField] private string gameplaySceneName = "";
    [SerializeField, Min(1)] private float startingHealth = 100f;

    private static readonly Color Background = new Color(0.055f, 0.075f, 0.11f);
    private static readonly Color Panel = new Color(0.09f, 0.12f, 0.17f, 0.98f);
    private static readonly Color Accent = new Color(1f, 0.56f, 0.28f);
    private static readonly Color Muted = new Color(0.66f, 0.72f, 0.79f);
    private static readonly Color ButtonDark = new Color(0.16f, 0.20f, 0.27f);

    private Canvas canvas;
    private Font runtimeFont;
    private GameObject mainMenu;
    private GameObject gameHud;
    private GameObject gameplayPlaceholder;
    private GameObject pauseMenu;
    private GameObject settingsPanel;
    private GameObject helpPanel;
    private GameObject versionPanel;
    private GameObject confirmPanel;
    private GameObject dialoguePanel;
    private GameObject resultPanel;
    private Slider healthSlider;
    private Text healthValueText;
    private Text confirmMessage;
    private Text dialogueSpeaker;
    private Text dialogueLine;
    private Text resultTitle;
    private AudioManager audioManager;
    private SceneTransitionManager transitionManager;
    private Camera hostCamera;
    private AudioListener hostAudioListener;
    private Scene menuScene;
    private System.Action confirmAction;
    private bool levelLoaded;
    private float currentHealth;

    private void Awake()
    {
        menuScene = SceneManager.GetActiveScene();
        hostCamera = GetComponent<Camera>();
        hostAudioListener = GetComponent<AudioListener>();
        audioManager = GetComponent<AudioManager>();
        if (audioManager == null) audioManager = gameObject.AddComponent<AudioManager>();
        audioManager.Initialize();
        transitionManager = SceneTransitionManager.Create();

        BuildInterface();
        GameEventCenter.HealthChanged += OnHealthChanged;
        GameEventCenter.DialogueRequested += OnDialogueRequested;
        ShowMainMenuImmediate();
        GameStateManager.SetState(GameState.MainMenu);
    }

    private void OnDestroy()
    {
        GameEventCenter.HealthChanged -= OnHealthChanged;
        GameEventCenter.DialogueRequested -= OnDialogueRequested;
        if (GameStateManager.Current == GameState.Paused) Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!WasPausePressed()) return;
        if (dialoguePanel.activeSelf) dialoguePanel.SetActive(false);
        else if (confirmPanel.activeSelf) CloseConfirmation();
        else if (settingsPanel.activeSelf) settingsPanel.SetActive(false);
        else if (helpPanel.activeSelf) helpPanel.SetActive(false);
        else if (versionPanel.activeSelf) versionPanel.SetActive(false);
        else if (GameStateManager.Current == GameState.Playing) ShowPauseMenu();
        else if (GameStateManager.Current == GameState.Paused) ResumeGame();
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
        settingsPanel = CreateScreen("Settings Panel", canvas.transform);
        BuildSettings(settingsPanel.transform);
        helpPanel = CreateScreen("Controls Panel", canvas.transform);
        BuildHelp(helpPanel.transform);
        versionPanel = CreateScreen("Version Panel", canvas.transform);
        BuildVersion(versionPanel.transform);
        confirmPanel = CreateScreen("Confirmation Panel", canvas.transform);
        BuildConfirmation(confirmPanel.transform);
        dialoguePanel = CreateScreen("Dialogue Panel", canvas.transform);
        BuildDialogue(dialoguePanel.transform);
        resultPanel = CreateScreen("Result Panel", canvas.transform);
        BuildResult(resultPanel.transform);
    }

    private void BuildMainMenu(Transform root)
    {
        AddImage(root, "Background", Background, Vector2.zero, Vector2.one);
        AddImage(root, "Accent bar", Accent, new Vector2(0.08f, 0.18f), new Vector2(0.085f, 0.82f));
        AddText(root, "GAME TEMPLATE  ·  UNITY 6.6", 22, Accent, TextAnchor.MiddleLeft,
            new Vector2(0.13f, 0.72f), new Vector2(0.62f, 0.79f));
        AddText(root, "你的游戏名称", 76, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.13f, 0.51f), new Vector2(0.67f, 0.72f));
        AddText(root, "从这里开始，替换成你的游戏内容。", 25, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.135f, 0.43f), new Vector2(0.68f, 0.50f));

        CreateButton(root, "开始游戏", new Vector2(0.135f, 0.31f), new Vector2(0.33f, 0.39f), Accent, StartGame);
        CreateButton(root, "操作说明", new Vector2(0.135f, 0.21f), new Vector2(0.33f, 0.29f), ButtonDark, () => helpPanel.SetActive(true));
        CreateButton(root, "音量设置", new Vector2(0.35f, 0.31f), new Vector2(0.55f, 0.39f), ButtonDark, () => settingsPanel.SetActive(true));
        CreateButton(root, "版本信息", new Vector2(0.35f, 0.21f), new Vector2(0.55f, 0.29f), ButtonDark, () => versionPanel.SetActive(true));
        CreateButton(root, "退出游戏", new Vector2(0.135f, 0.095f), new Vector2(0.33f, 0.175f), ButtonDark,
            () => ShowConfirmation("确定退出游戏吗？", ExitGame));

        AddImage(root, "Flow card", Panel, new Vector2(0.67f, 0.25f), new Vector2(0.88f, 0.76f));
        AddText(root, "基础流程", 30, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.71f, 0.65f), new Vector2(0.84f, 0.71f));
        AddText(root, "01   主菜单进入游戏\n\n02   游戏中按 ESC 暂停\n\n03   继续、重置或返回主菜单\n\n04   通关与失败弹窗示例", 22, Muted,
            TextAnchor.UpperLeft, new Vector2(0.71f, 0.37f), new Vector2(0.85f, 0.63f));
    }

    private void BuildGameHud(Transform root)
    {
        gameplayPlaceholder = CreateScreen("Placeholder Gameplay", root);
        AddImage(gameplayPlaceholder.transform, "Placeholder game background", new Color(0.10f, 0.16f, 0.20f), Vector2.zero, Vector2.one);
        AddImage(gameplayPlaceholder.transform, "World floor", new Color(0.13f, 0.22f, 0.24f), new Vector2(0f, 0f), new Vector2(1f, 0.22f));
        AddText(gameplayPlaceholder.transform, "游戏场景占位", 42, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.60f), new Vector2(0.75f, 0.72f));
        AddText(gameplayPlaceholder.transform, "已进入游戏。将此区域替换成你的游戏场景。", 22, Muted, TextAnchor.MiddleCenter,
            new Vector2(0.25f, 0.52f), new Vector2(0.75f, 0.60f));

        AddImage(root, "Health panel", Panel, new Vector2(0.025f, 0.825f), new Vector2(0.355f, 0.94f));
        AddText(root, "生命值", 20, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.045f, 0.875f), new Vector2(0.14f, 0.925f));
        healthValueText = AddText(root, "100 / 100", 18, Muted, TextAnchor.MiddleRight,
            new Vector2(0.25f, 0.875f), new Vector2(0.335f, 0.925f));
        healthSlider = CreateSlider(root, "Health Slider", new Vector2(0.045f, 0.845f), new Vector2(0.335f, 0.875f),
            new Color(0.23f, 0.10f, 0.12f), new Color(0.32f, 0.88f, 0.48f));
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.wholeNumbers = false;
        healthSlider.direction = Slider.Direction.LeftToRight;
        healthSlider.interactable = false;

        AddText(root, "基础流程模板  ·  游戏中", 18, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.94f), new Vector2(0.43f, 0.99f));
        CreateButton(root, "台词提示", new Vector2(0.69f, 0.90f), new Vector2(0.80f, 0.96f), ButtonDark,
            () => GameEventCenter.RequestDialogue("旁白", "这里展示一条剧情台词或任务提示。"), 18);
        CreateButton(root, "暂停  (ESC)", new Vector2(0.82f, 0.90f), new Vector2(0.96f, 0.96f), ButtonDark, ShowPauseMenu, 18);

        CreateButton(root, "演示：通关", new Vector2(0.40f, 0.04f), new Vector2(0.53f, 0.105f), ButtonDark,
            () => ShowResult(true), 17);
        CreateButton(root, "演示：失败", new Vector2(0.55f, 0.04f), new Vector2(0.68f, 0.105f), ButtonDark,
            () => ShowResult(false), 17);
        CreateButton(root, "演示：受伤", new Vector2(0.70f, 0.04f), new Vector2(0.83f, 0.105f), ButtonDark,
            () => SetHealth(currentHealth - 10f, startingHealth), 17);
    }

    private void BuildPauseMenu(Transform root)
    {
        AddImage(root, "Dim overlay", new Color(0.015f, 0.025f, 0.04f, 0.78f), Vector2.zero, Vector2.one);
        AddImage(root, "Pause panel", Panel, new Vector2(0.34f, 0.14f), new Vector2(0.66f, 0.86f));
        AddText(root, "游戏暂停", 48, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.37f, 0.76f), new Vector2(0.63f, 0.83f));
        CreateButton(root, "继续游戏", new Vector2(0.40f, 0.65f), new Vector2(0.60f, 0.72f), Accent, ResumeGame, 21);
        CreateButton(root, "音量设置", new Vector2(0.40f, 0.55f), new Vector2(0.60f, 0.62f), ButtonDark,
            () => settingsPanel.SetActive(true), 21);
        CreateButton(root, "操作说明", new Vector2(0.40f, 0.45f), new Vector2(0.60f, 0.52f), ButtonDark,
            () => helpPanel.SetActive(true), 21);
        CreateButton(root, "重置关卡", new Vector2(0.40f, 0.35f), new Vector2(0.60f, 0.42f), ButtonDark,
            () => ShowConfirmation("重置当前关卡？当前进度将丢失。", ResetLevel), 21);
        CreateButton(root, "返回主菜单", new Vector2(0.40f, 0.25f), new Vector2(0.60f, 0.32f), ButtonDark,
            () => ShowConfirmation("返回主菜单？当前关卡进度将丢失。", ReturnToMainMenu), 21);
        CreateButton(root, "退出游戏", new Vector2(0.40f, 0.15f), new Vector2(0.60f, 0.22f), ButtonDark,
            () => ShowConfirmation("确定退出游戏吗？", ExitGame), 21);
    }

    private void BuildSettings(Transform root)
    {
        AddImage(root, "Settings dim", new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.one);
        AddImage(root, "Settings panel", Panel, new Vector2(0.31f, 0.27f), new Vector2(0.69f, 0.73f));
        AddText(root, "音量设置", 38, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.36f, 0.63f), new Vector2(0.64f, 0.70f));
        AddText(root, "音乐音量", 22, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.37f, 0.54f), new Vector2(0.49f, 0.59f));
        AddText(root, "音效音量", 22, Muted, TextAnchor.MiddleLeft,
            new Vector2(0.37f, 0.44f), new Vector2(0.49f, 0.49f));
        var music = CreateSlider(root, "Music Volume Slider", new Vector2(0.50f, 0.545f), new Vector2(0.64f, 0.585f), ButtonDark, Accent);
        music.value = audioManager.MusicVolume;
        music.onValueChanged.AddListener(audioManager.SetMusicVolume);
        var sfx = CreateSlider(root, "SFX Volume Slider", new Vector2(0.50f, 0.445f), new Vector2(0.64f, 0.485f), ButtonDark, Accent);
        sfx.value = audioManager.SfxVolume;
        sfx.onValueChanged.AddListener(audioManager.SetSfxVolume);
        CreateButton(root, "关闭", new Vector2(0.41f, 0.32f), new Vector2(0.59f, 0.40f), Accent,
            () => settingsPanel.SetActive(false));
    }

    private void BuildHelp(Transform root)
    {
        AddImage(root, "Help dim", new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.one);
        AddImage(root, "Help panel", Panel, new Vector2(0.30f, 0.27f), new Vector2(0.70f, 0.73f));
        AddText(root, "操作说明", 38, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.35f, 0.63f), new Vector2(0.65f, 0.70f));
        AddText(root, "ESC       暂停 / 继续游戏\n鼠标      点击界面按钮\n\n将此面板替换成具体游戏的操作说明。", 23, Muted,
            TextAnchor.MiddleCenter, new Vector2(0.35f, 0.40f), new Vector2(0.65f, 0.61f));
        CreateButton(root, "关闭", new Vector2(0.41f, 0.31f), new Vector2(0.59f, 0.39f), Accent,
            () => helpPanel.SetActive(false));
    }

    private void BuildVersion(Transform root)
    {
        AddImage(root, "Version dim", new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.one);
        AddImage(root, "Version panel", Panel, new Vector2(0.32f, 0.31f), new Vector2(0.68f, 0.69f));
        AddText(root, "版本信息", 38, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.37f, 0.58f), new Vector2(0.63f, 0.66f));
        AddText(root, $"游戏版本  {Application.version}\nUnity  {Application.unityVersion}", 22, Muted,
            TextAnchor.MiddleCenter, new Vector2(0.37f, 0.45f), new Vector2(0.63f, 0.56f));
        CreateButton(root, "关闭", new Vector2(0.41f, 0.34f), new Vector2(0.59f, 0.42f), Accent,
            () => versionPanel.SetActive(false));
    }

    private void BuildConfirmation(Transform root)
    {
        AddImage(root, "Confirm dim", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one);
        AddImage(root, "Confirm panel", Panel, new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.66f));
        confirmMessage = AddText(root, "确认操作？", 27, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.36f, 0.49f), new Vector2(0.64f, 0.61f));
        CreateButton(root, "确认", new Vector2(0.37f, 0.385f), new Vector2(0.49f, 0.46f), Accent,
            () => { var action = confirmAction; CloseConfirmation(); action?.Invoke(); }, 20);
        CreateButton(root, "取消", new Vector2(0.51f, 0.385f), new Vector2(0.63f, 0.46f), ButtonDark,
            CloseConfirmation, 20);
    }

    private void BuildDialogue(Transform root)
    {
        AddImage(root, "Dialogue dim", new Color(0f, 0f, 0f, 0.35f), Vector2.zero, Vector2.one);
        AddImage(root, "Dialogue box", Panel, new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.29f));
        dialogueSpeaker = AddText(root, "旁白", 24, Accent, TextAnchor.MiddleLeft,
            new Vector2(0.20f, 0.22f), new Vector2(0.78f, 0.27f));
        dialogueLine = AddText(root, "台词内容", 23, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0.20f, 0.13f), new Vector2(0.72f, 0.22f));
        CreateButton(root, "继续", new Vector2(0.75f, 0.105f), new Vector2(0.81f, 0.17f), Accent,
            () => dialoguePanel.SetActive(false), 18);
    }

    private void BuildResult(Transform root)
    {
        AddImage(root, "Result dim", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one);
        AddImage(root, "Result panel", Panel, new Vector2(0.34f, 0.31f), new Vector2(0.66f, 0.69f));
        resultTitle = AddText(root, "关卡完成", 42, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.37f, 0.57f), new Vector2(0.63f, 0.65f));
        AddText(root, "你可以重试关卡或返回主菜单。", 20, Muted, TextAnchor.MiddleCenter,
            new Vector2(0.36f, 0.49f), new Vector2(0.64f, 0.55f));
        CreateButton(root, "重试关卡", new Vector2(0.37f, 0.37f), new Vector2(0.49f, 0.45f), Accent,
            () => { resultPanel.SetActive(false); ResetLevel(); }, 18);
        CreateButton(root, "返回主菜单", new Vector2(0.51f, 0.37f), new Vector2(0.63f, 0.45f), ButtonDark,
            () => ShowConfirmation("返回主菜单？", ReturnToMainMenu), 18);
    }

    private Slider CreateSlider(Transform parent, string name, Vector2 min, Vector2 max, Color backgroundColor, Color fillColor)
    {
        var sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        var rect = sliderObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var background = AddImage(sliderObject.transform, "Background", backgroundColor, Vector2.zero, Vector2.one);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2f, 2f);
        fillAreaRect.offsetMax = new Vector2(-2f, -2f);
        var fill = AddImage(fillArea.transform, "Fill", fillColor, Vector2.zero, Vector2.one);
        var slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = background;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private void StartGame()
    {
        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && gameplaySceneName != menuScene.name)
        {
            transitionManager.LoadLevelAdditive(gameplaySceneName, () =>
            {
                levelLoaded = true;
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(gameplaySceneName));
                SetHostCameraEnabled(false);
                EnterGameplay();
            });
            return;
        }

        transitionManager.FadeAction(EnterGameplay);
    }

    private void EnterGameplay()
    {
        GameStateManager.SetState(GameState.Playing);
        mainMenu.SetActive(false);
        gameHud.SetActive(true);
        gameplayPlaceholder.SetActive(!levelLoaded);
        pauseMenu.SetActive(false);
        SetModalPanels(false);
        SetHealth(startingHealth, startingHealth);
    }

    private void ShowPauseMenu()
    {
        if (GameStateManager.Current != GameState.Playing) return;
        GameStateManager.SetState(GameState.Paused);
        pauseMenu.SetActive(true);
    }

    private void ResumeGame()
    {
        if (GameStateManager.Current != GameState.Paused) return;
        GameStateManager.SetState(GameState.Playing);
        pauseMenu.SetActive(false);
    }

    private void ReturnToMainMenu()
    {
        if (levelLoaded)
        {
            transitionManager.UnloadLevelAdditive(gameplaySceneName, () =>
            {
                levelLoaded = false;
                if (menuScene.IsValid() && menuScene.isLoaded) SceneManager.SetActiveScene(menuScene);
                SetHostCameraEnabled(true);
                ShowMainMenuImmediate();
            });
            return;
        }
        transitionManager.FadeAction(ShowMainMenuImmediate);
    }

    private void ShowMainMenuImmediate()
    {
        GameStateManager.SetState(GameState.MainMenu);
        Time.timeScale = 1f;
        mainMenu.SetActive(true);
        gameHud.SetActive(false);
        pauseMenu.SetActive(false);
        SetModalPanels(false);
    }

    private void ResetLevel()
    {
        if (levelLoaded)
        {
            transitionManager.ReloadLevelAdditive(gameplaySceneName, () =>
            {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(gameplaySceneName));
                SetHealth(startingHealth, startingHealth);
                GameEventCenter.PublishLevelReset();
                GameStateManager.SetState(GameState.Playing);
                pauseMenu.SetActive(false);
                resultPanel.SetActive(false);
            });
            return;
        }
        SetHealth(startingHealth, startingHealth);
        GameEventCenter.PublishLevelReset();
        GameStateManager.SetState(GameState.Playing);
        pauseMenu.SetActive(false);
        resultPanel.SetActive(false);
    }

    public void SetHealth(float current, float maximum)
    {
        maximum = Mathf.Max(1f, maximum);
        current = Mathf.Clamp(current, 0f, maximum);
        GameEventCenter.PublishHealth(current, maximum);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        currentHealth = current;
        if (healthSlider == null) return;
        healthSlider.value = maximum > 0f ? current / maximum : 0f;
        healthValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
    }

    public void ShowDialogue(string speaker, string line) => GameEventCenter.RequestDialogue(speaker, line);

    private void OnDialogueRequested(string speaker, string line)
    {
        dialogueSpeaker.text = speaker;
        dialogueLine.text = line;
        dialoguePanel.SetActive(true);
    }

    public void ShowVictory() => ShowResult(true);
    public void ShowDefeat() => ShowResult(false);

    private void ShowResult(bool victory)
    {
        GameStateManager.SetState(victory ? GameState.Victory : GameState.Defeat);
        resultTitle.text = victory ? "关卡完成" : "挑战失败";
        gameHud.SetActive(true);
        gameplayPlaceholder.SetActive(!levelLoaded);
        pauseMenu.SetActive(false);
        resultPanel.SetActive(true);
    }

    private void ShowConfirmation(string message, System.Action action)
    {
        confirmMessage.text = message;
        confirmAction = action;
        confirmPanel.SetActive(true);
        confirmPanel.transform.SetAsLastSibling();
    }

    private void CloseConfirmation()
    {
        confirmPanel.SetActive(false);
        confirmAction = null;
    }

    private void SetModalPanels(bool visible)
    {
        settingsPanel.SetActive(visible);
        helpPanel.SetActive(visible);
        versionPanel.SetActive(visible);
        confirmPanel.SetActive(visible);
        dialoguePanel.SetActive(visible);
        resultPanel.SetActive(visible);
    }

    private void SetHostCameraEnabled(bool enabled)
    {
        if (hostCamera != null) hostCamera.enabled = enabled;
        if (hostAudioListener != null) hostAudioListener.enabled = enabled;
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private GameObject CreateScreen(string name, Transform parent)
    {
        var screen = new GameObject(name, typeof(RectTransform));
        screen.transform.SetParent(parent, false);
        Stretch(screen.GetComponent<RectTransform>());
        return screen;
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
        text.font = RuntimeFont;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Font RuntimeFont
    {
        get
        {
            if (runtimeFont == null) runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return runtimeFont;
        }
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
        button.onClick.AddListener(() =>
        {
            audioManager.PlayUiClick();
            action?.Invoke();
        });
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
