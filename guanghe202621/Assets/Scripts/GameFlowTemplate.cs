using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>接入美术可编辑的 UI 预置体，负责菜单、暂停、结果与对话的显示和按钮事件。</summary>
public sealed class GameFlowTemplate : MonoBehaviour
{
    [Header("只接入本场景 Canvas 下的 UI 预置体实例")]
    [Tooltip("可拖入 Hierarchy 中的主菜单。留空时根据 GameUIPanel 自动接入；运行时显示实际使用的实例。")]
    [SerializeField, InspectorName("场景中的主菜单")] private GameObject mainMenuPrefab;
    [Tooltip("可拖入 Hierarchy 中的暂停面板。替换实例后会重新识别面板类型。")]
    [SerializeField, InspectorName("场景中的暂停面板")] private GameObject pausePanelPrefab;
    [Tooltip("可拖入 Hierarchy 中的 HudPanel，开始游戏后显示的就是这份场景实例。")]
    [SerializeField, InspectorName("场景中的游戏界面")] private GameObject hudPanelPrefab;
    [Header("关卡加载（可选）")]
    [Tooltip("留空时在当前场景开始游戏；填写后加载指定关卡。指定场景需要加入构建场景列表。")]
    [SerializeField, InspectorName("游戏关卡场景名")] private string gameplaySceneName = "";

    private GameObject mainMenu;
    private GameObject pausePanel;
    private GameObject hudPanel;
    private GameObject mainConfirmationPanel;
    private GameObject pauseConfirmationPanel;
    private GameObject hudConfirmationPanel;
    private GameObject settingsPanel;
    private GameObject pauseSettingsPanel;
    private GameObject helpPanel;
    private GameObject pauseHelpPanel;
    private GameObject versionPanel;
    private GameObject dialoguePanel;
    private GameObject resultPanel;
    private Text dialogueTitle;
    private Text dialogueSpeaker;
    private Text dialogueContent;
    private Text resultTitle;
    private AudioManager audioManager;
    private SceneTransitionManager transitions;
    private Camera menuCamera;
    private AudioListener menuListener;
    private Scene menuScene;
    private Action confirmAction;
    private bool levelLoaded;
    // Unity 重载脚本时保存 UI 状态，但运行时事件必须重新绑定。
    [SerializeField, HideInInspector] private GameState uiState = GameState.MainMenu;
    [NonSerialized] private bool callbacksBound;
    [NonSerialized] private List<Action> releaseBindings = new List<Action>();

    private void Awake() => InitializeUi(false);

    private void OnEnable()
    {
        // Awake 不会在运行中编译后再次调用，OnEnable 会，因此在这里恢复事件。
        if (!callbacksBound) InitializeUi(true);
    }

    private bool InitializeUi(bool restoreState)
    {
        if (callbacksBound) return true;
        releaseBindings ??= new List<Action>();
        menuScene = gameObject.scene;
        menuCamera = GetComponent<Camera>();
        menuListener = GetComponent<AudioListener>();
        audioManager = GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
        audioManager.Initialize();
        if (transitions == null) transitions = SceneTransitionManager.Create();
        EnsureEventSystem();

        mainMenu = ResolveUiPanel(mainMenuPrefab, GameUIPanel.PanelKind.MainMenu, "MainMenu", "MainMenuPanel");
        pausePanel = ResolveUiPanel(pausePanelPrefab, GameUIPanel.PanelKind.PausePanel, "PausePanel", "PausePanel");
        hudPanel = ResolveUiPanel(hudPanelPrefab, GameUIPanel.PanelKind.HudPanel, "HudPanel", "GameplayHUD");
        if (mainMenu == null || pausePanel == null || hudPanel == null)
        {
            // 配置暂时缺失时保留组件启用状态，修正引用后仍可重新接入。
            return false;
        }

        // Inspector 在运行时直接显示当前使用的场景对象，方便确认美术改的是哪一份。
        mainMenuPrefab = mainMenu;
        pausePanelPrefab = pausePanel;
        hudPanelPrefab = hudPanel;
        FixCollapsedUiLayout(mainMenu);
        FixCollapsedUiLayout(pausePanel);
        FixCollapsedUiLayout(hudPanel);
        mainConfirmationPanel = FindObject(mainMenu, "ConfirmationPanel");
        pauseConfirmationPanel = FindObject(pausePanel, "ConfirmationPanel");
        hudConfirmationPanel = FindObject(hudPanel, "ConfirmationPanel");
        settingsPanel = FindObject(mainMenu, "SettingsPanel");
        pauseSettingsPanel = FindObject(pausePanel, "SettingsPanel");
        helpPanel = FindObject(mainMenu, "HelpPanel");
        pauseHelpPanel = FindObject(pausePanel, "HelpPanel");
        versionPanel = FindObject(mainMenu, "VersionPanel");
        dialoguePanel = FindObject(hudPanel, "DialoguePanel");
        resultPanel = FindObject(hudPanel, "ResultPanel");
        dialogueTitle = FindComponent<Text>(dialoguePanel, "Title");
        dialogueSpeaker = FindComponent<Text>(dialoguePanel, "SpeakerText");
        dialogueContent = FindComponent<Text>(dialoguePanel, "DialogueText");
        DialogueFontUtility.ApplyFallback(dialogueTitle);
        DialogueFontUtility.ApplyFallback(dialogueSpeaker);
        DialogueFontUtility.ApplyFallback(dialogueContent);
        resultTitle = FindComponent<Text>(resultPanel, "ResultTitle");

        BindButtons();
        if (FindComponent<Button>(mainMenu, "StartButton") == null)
            Debug.LogError("主菜单缺少开始按钮的功能引用。请在 GameUIPanel 中把“开始游戏按钮”关联到实际 Button。", mainMenu);
        BindSettings();
        GameEventCenter.DialogueRequested -= OnDialogueRequested;
        GameEventCenter.DialogueRequested += OnDialogueRequested;
        callbacksBound = true;
        if (restoreState) RestoreVisibleState();
        else ShowMainMenu();
        return true;
    }

    private void OnDisable() => ReleaseBindings();

    private void OnDestroy()
    {
        ReleaseBindings();
        if (GameStateManager.Current == GameState.Paused) Time.timeScale = 1f;
    }

    private void ReleaseBindings()
    {
        GameEventCenter.DialogueRequested -= OnDialogueRequested;
        if (releaseBindings != null)
        {
            foreach (Action release in releaseBindings) release();
            releaseBindings.Clear();
        }
        callbacksBound = false;
    }

    private void RestoreVisibleState()
    {
        mainMenu.SetActive(uiState == GameState.MainMenu);
        hudPanel.SetActive(uiState != GameState.MainMenu);
        pausePanel.SetActive(uiState == GameState.Paused);
        GameStateManager.SetState(uiState);
        Time.timeScale = uiState == GameState.Paused ? 0f : 1f;
        // 确认操作的委托无法跨脚本重载保存，取消旧确认框，避免出现空确认按钮。
        if (confirmAction == null) CloseConfirmation();
        Canvas.ForceUpdateCanvases();
    }

    private void ChangeState(GameState next)
    {
        uiState = next;
        GameStateManager.SetState(next);
    }

    private void Update()
    {
        if (!PausePressed()) return;
        if (IsActive(dialoguePanel)) dialoguePanel.SetActive(false);
        else if (IsAnyConfirmationOpen()) CloseConfirmation();
        else if (IsActive(settingsPanel) || IsActive(pauseSettingsPanel)) { ShowPanel(settingsPanel, false); ShowPanel(pauseSettingsPanel, false); }
        else if (IsActive(helpPanel) || IsActive(pauseHelpPanel)) { ShowPanel(helpPanel, false); ShowPanel(pauseHelpPanel, false); }
        else if (IsActive(versionPanel)) versionPanel.SetActive(false);
        else if (GameStateManager.Current == GameState.Playing) PauseGame();
        else if (GameStateManager.Current == GameState.Paused) ResumeGame();
    }

    private GameObject ResolveUiPanel(GameObject panel, GameUIPanel.PanelKind kind,
        string instanceName, string legacyName)
    {
        // 只从当前场景 Canvas 中选择，包括未激活的游戏和暂停面板。
        var views = FindObjectsByType<GameUIPanel>(FindObjectsInactive.Include);
        GameObject scenePanel = null;
        foreach (var view in views)
        {
            if (view.Kind != kind || !IsCanvasInstance(view.gameObject)) continue;
            // 同类型重复拖入时，优先使用后放到 Canvas 下的实例，避免旧引用压过新面板。
            if (scenePanel == null || view.transform.GetSiblingIndex() > scenePanel.transform.GetSiblingIndex())
                scenePanel = view.gameObject;
        }
        if (scenePanel == null && IsCanvasInstance(panel))
            scenePanel = panel;

        // 兼容未挂 GameUIPanel 的旧场景。新预置体改名后由类型识别。
        if (scenePanel == null)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name != instanceName && item.name != legacyName) continue;
                    if (!IsCanvasInstance(item.gameObject)) continue;
                    scenePanel = item.gameObject;
                    break;
                }
                if (scenePanel != null) break;
            }
        }

        if (scenePanel != null)
        {
            foreach (var view in views)
            {
                if (view.Kind != kind || !IsCanvasInstance(view.gameObject) || view.gameObject == scenePanel) continue;
                view.gameObject.SetActive(false);
                Debug.LogWarning($"场景中有重复的{instanceName}，已使用“{scenePanel.name}”并关闭另一份。请在编辑器里保留一份面板。", this);
            }
            return scenePanel;
        }

        Debug.LogError($"Canvas 下缺少{instanceName}实例。请把你编辑的预置体拖入 Canvas，保留根节点的 GameUIPanel 组件。", this);
        return null;
    }

    private bool IsCanvasInstance(GameObject panel)
    {
        return panel != null && panel.scene == gameObject.scene && panel.transform.parent != null &&
            panel.transform.parent.GetComponentInParent<Canvas>(true) != null;
    }
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }
    private void BindButtons()
    {
        Bind(mainMenu, "StartButton", StartGame);
        Bind(mainMenu, "SettingsButton", () => ShowPanel(settingsPanel));
        Bind(mainMenu, "HelpButton", () => ShowPanel(helpPanel));
        Bind(mainMenu, "VersionButton", () => ShowPanel(versionPanel));
        Bind(mainMenu, "ExitButton", () => Confirm(mainConfirmationPanel, ConfirmationDialog.Kind.ExitGame, ExitGame));
        Bind(pausePanel, "ResumeButton", ResumeGame);
        Bind(pausePanel, "PauseSettingsButton", () => ShowPanel(pauseSettingsPanel));
        Bind(pausePanel, "PauseHelpButton", () => ShowPanel(pauseHelpPanel));
        Bind(pausePanel, "ResetButton", () => Confirm(pauseConfirmationPanel, ConfirmationDialog.Kind.ResetLevel, ResetLevel));
        Bind(pausePanel, "ReturnToMenuButton", () => Confirm(pauseConfirmationPanel, ConfirmationDialog.Kind.ReturnToMenu, ReturnToMainMenu));

        Bind(hudPanel, "PauseButton", PauseGame);

        Bind(mainConfirmationPanel, "ConfirmButton", ConfirmAction);
        Bind(mainConfirmationPanel, "CancelButton", CloseConfirmation);
        Bind(pauseConfirmationPanel, "ConfirmButton", ConfirmAction);
        Bind(pauseConfirmationPanel, "CancelButton", CloseConfirmation);
        Bind(hudConfirmationPanel, "ConfirmButton", ConfirmAction);
        Bind(hudConfirmationPanel, "CancelButton", CloseConfirmation);
        Bind(settingsPanel, "CloseButton", () => ShowPanel(settingsPanel, false));
        Bind(pauseSettingsPanel, "CloseButton", () => ShowPanel(pauseSettingsPanel, false));
        Bind(helpPanel, "CloseButton", () => ShowPanel(helpPanel, false));
        Bind(pauseHelpPanel, "CloseButton", () => ShowPanel(pauseHelpPanel, false));
        Bind(versionPanel, "CloseButton", () => ShowPanel(versionPanel, false));
        Bind(dialoguePanel, "DialogueContinueButton", () => ShowPanel(dialoguePanel, false));
        Bind(resultPanel, "RetryButton", () => { ShowPanel(resultPanel, false); ResetLevel(); });
        Bind(resultPanel, "ResultMenuButton", () => Confirm(hudConfirmationPanel, ConfirmationDialog.Kind.ReturnToMenu, ReturnToMainMenu));
    }

    private void BindSettings()
    {
        if (audioManager == null) return;
        BindVolumeSliders(settingsPanel);
        BindVolumeSliders(pauseSettingsPanel);
    }

    private void BindVolumeSliders(GameObject panel)
    {
        var music = FindComponent<Slider>(panel, "MusicVolumeSlider");
        var sfx = FindComponent<Slider>(panel, "SfxVolumeSlider");
        if (music != null)
        {
            music.SetValueWithoutNotify(audioManager.MusicVolume);
            music.onValueChanged.AddListener(audioManager.SetMusicVolume);
            releaseBindings.Add(() => { if (music != null) music.onValueChanged.RemoveListener(audioManager.SetMusicVolume); });
        }
        if (sfx != null)
        {
            sfx.SetValueWithoutNotify(audioManager.SfxVolume);
            sfx.onValueChanged.AddListener(audioManager.SetSfxVolume);
            releaseBindings.Add(() => { if (sfx != null) sfx.onValueChanged.RemoveListener(audioManager.SetSfxVolume); });
        }
    }

    public void StartGame()
    {
        if (!callbacksBound && !InitializeUi(true)) return;
        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && gameplaySceneName != menuScene.name)
        {
            // 场景名误填为项目名等情况，仍然进入当前 Canvas 的游戏界面。
            if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                Debug.LogWarning($"关卡“{gameplaySceneName}”不存在或未加入构建列表，已进入当前场景的游戏界面。制作 UI 时请把“游戏关卡场景名”留空。", this);
                EnterGame();
                return;
            }
            transitions.LoadLevelAdditive(gameplaySceneName, () =>
            {
                levelLoaded = true;
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(gameplaySceneName));
                SetMenuCamera(false);
                EnterGame();
            });
        }
        else EnterGame();
    }

    private void EnterGame()
    {
        HideOptionalPanels();
        FixCollapsedUiLayout(hudPanel);
        hudPanel.SetActive(true);
        mainMenu.SetActive(false);
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        Canvas.ForceUpdateCanvases();
        ChangeState(GameState.Playing);
        audioManager.PlayGameplayMusic();
    }

    private static void FixCollapsedUiLayout(GameObject panel)
    {
        if (panel.transform is not RectTransform rect || rect.parent is not RectTransform) return;

        if (rect.localScale.x == 0f || rect.localScale.y == 0f)
            rect.localScale = Vector3.one;

        // 修复旧场景中被压成零宽高的根面板，正常的美术布局保持原值。
        bool widthCollapsed = rect.anchorMin.x == rect.anchorMax.x && rect.sizeDelta.x <= 0f;
        bool heightCollapsed = rect.anchorMin.y == rect.anchorMax.y && rect.sizeDelta.y <= 0f;
        if (!widthCollapsed && !heightCollapsed) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void PauseGame()
    {
        if (GameStateManager.Current != GameState.Playing) return;
        ChangeState(GameState.Paused);
        pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (GameStateManager.Current != GameState.Paused) return;
        ChangeState(GameState.Playing);
        pausePanel.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        Action finish = () =>
        {
            levelLoaded = false;
            if (menuScene.IsValid() && menuScene.isLoaded) SceneManager.SetActiveScene(menuScene);
            SetMenuCamera(true);
            ShowMainMenu();
        };
        if (levelLoaded) transitions.UnloadLevelAdditive(gameplaySceneName, finish);
        else finish();
    }

    private void ShowMainMenu()
    {
        ChangeState(GameState.MainMenu);
        Time.timeScale = 1f;
        mainMenu.SetActive(true);
        hudPanel.SetActive(false);
        pausePanel.SetActive(false);
        HideOptionalPanels();
        audioManager.PlayMenuMusic();
    }

    public void ResetLevel()
    {
        Action finish = () =>
        {
            GameEventCenter.PublishLevelReset();
            ChangeState(GameState.Playing);
            pausePanel.SetActive(false);
            ShowPanel(resultPanel, false);
        };
        if (levelLoaded) transitions.ReloadLevelAdditive(gameplaySceneName, finish);
        else finish();
    }

    public void ShowVictory() => ShowResult(true);
    public void ShowDefeat() => ShowResult(false);
    public void ShowDialogue(string speaker, string line) => GameEventCenter.RequestDialogue(speaker, line);
    public void ShowNarration(string line) => ShowDialogue("旁白", line);

    private void ShowResult(bool victory)
    {
        ChangeState(victory ? GameState.Victory : GameState.Defeat);
        if (resultTitle != null) resultTitle.text = victory ? "关卡完成" : "挑战失败";
        ShowPanel(resultPanel);
    }

    private void OnDialogueRequested(string speaker, string line)
    {
        if (dialogueTitle != null)
            dialogueTitle.text = speaker == "旁白" ? "旁白提示" : "对话";

        if (dialogueSpeaker != null)
        {
            dialogueSpeaker.text = speaker ?? string.Empty;
            dialogueSpeaker.gameObject.SetActive(!string.IsNullOrWhiteSpace(speaker));
        }

        if (dialogueContent != null) dialogueContent.text = line ?? string.Empty;
        ShowPanel(dialoguePanel);
        if (dialoguePanel != null) dialoguePanel.transform.SetAsLastSibling();
    }

    private void Confirm(GameObject panel, ConfirmationDialog.Kind kind, Action action)
    {
        if (panel == null) { Debug.LogError("确认弹窗缺失：请在对应 prefab 内添加 ConfirmationPanel。"); return; }
        if (!ConfirmationDialog.TryShow(panel, kind))
        {
            Debug.LogError("当前运行中的确认弹窗还是旧实例，缺少对应的文案物体。若刚在运行中更新过预制体，请停止运行后重新开始，让场景使用更新后的预制体；无需手动添加 OnClick。", panel);
            return;
        }
        confirmAction = action;
        ShowPanel(panel);
        panel.transform.SetAsLastSibling();
    }

    private void ConfirmAction() { var action = confirmAction; CloseConfirmation(); action?.Invoke(); }
    private bool IsAnyConfirmationOpen() => IsActive(mainConfirmationPanel) || IsActive(pauseConfirmationPanel) || IsActive(hudConfirmationPanel);

    private void CloseConfirmation()
    {
        ShowPanel(mainConfirmationPanel, false);
        ShowPanel(pauseConfirmationPanel, false);
        ShowPanel(hudConfirmationPanel, false);
        confirmAction = null;
    }
    private void HideOptionalPanels()
    {
        CloseConfirmation(); ShowPanel(settingsPanel, false); ShowPanel(pauseSettingsPanel, false); ShowPanel(helpPanel, false); ShowPanel(pauseHelpPanel, false);
        ShowPanel(versionPanel, false); ShowPanel(dialoguePanel, false); ShowPanel(resultPanel, false);
    }
    private static void ShowPanel(GameObject panel, bool visible = true) { if (panel != null) panel.SetActive(visible); }
    private static bool IsActive(GameObject panel) => panel != null && panel.activeSelf;
    private void SetMenuCamera(bool value) { if (menuCamera != null) menuCamera.enabled = value; if (menuListener != null) menuListener.enabled = value; }

    private void Bind(GameObject root, string buttonName, UnityEngine.Events.UnityAction action)
    {
        var button = FindComponent<Button>(root, buttonName);
        if (button == null) return;
        UnityEngine.Events.UnityAction listener = () => { audioManager.PlayUiClick(); action?.Invoke(); };
        button.onClick.AddListener(listener);
        // 只移除本脚本添加的事件，美术在 Inspector 中配置的其他事件保留。
        releaseBindings.Add(() => { if (button != null) button.onClick.RemoveListener(listener); });
    }

    private static GameObject FindObject(GameObject root, string objectName)
    {
        if (root == null) return null;
        var view = root.GetComponentInParent<GameUIPanel>(true);
        if (view != null)
        {
            GameObject linked = view.FindElement(objectName, root.transform);
            if (linked != null) return linked;
        }
        foreach (var item in root.GetComponentsInChildren<Transform>(true)) if (item.name == objectName) return item.gameObject;
        return null;
    }
    private static T FindComponent<T>(GameObject root, string objectName) where T : Component
    {
        var item = FindObject(root, objectName);
        return item != null ? item.GetComponent<T>() : null;
    }
    private bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
