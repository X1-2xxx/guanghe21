using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Controls artist-editable UI prefab instances placed in the scene and wires their gameplay behaviour.</summary>
public sealed class GameFlowTemplate : MonoBehaviour
{
    [Header("Drag the UI prefab assets here")]
    [SerializeField] private GameObject mainMenuPrefab;
    [SerializeField] private GameObject pausePanelPrefab;
    [SerializeField] private GameObject hudPanelPrefab;
    [Header("Optional additive level")]
    [SerializeField] private string gameplaySceneName = "";
    [SerializeField, Min(1)] private float startingHealth = 100f;

    private GameObject mainMenu;
    private GameObject pausePanel;
    private GameObject hudPanel;
    private GameObject confirmationPanel;
    private GameObject pauseConfirmationPanel;
    private GameObject hudConfirmationPanel;
    private GameObject settingsPanel;
    private GameObject pauseSettingsPanel;
    private GameObject helpPanel;
    private GameObject pauseHelpPanel;
    private GameObject versionPanel;
    private GameObject dialoguePanel;
    private GameObject resultPanel;
    private Slider healthSlider;
    private Text healthText;
    private Text confirmText;
    private Text dialogueSpeaker;
    private Text dialogueContent;
    private Text resultTitle;
    private AudioManager audioManager;
    private SceneTransitionManager transitions;
    private Camera menuCamera;
    private AudioListener menuListener;
    private Scene menuScene;
    private Action confirmAction;
    private float currentHealth;
    private bool levelLoaded;

    private void Awake()
    {
        menuScene = SceneManager.GetActiveScene();
        menuCamera = GetComponent<Camera>();
        menuListener = GetComponent<AudioListener>();
        audioManager = GetComponent<AudioManager>() ?? gameObject.AddComponent<AudioManager>();
        audioManager.Initialize();
        transitions = SceneTransitionManager.Create();
        EnsureEventSystem();

        mainMenu = InstantiateUiPrefab(mainMenuPrefab, "MainMenu");
        pausePanel = InstantiateUiPrefab(pausePanelPrefab, "PausePanel");
        hudPanel = InstantiateUiPrefab(hudPanelPrefab, "HudPanel");
        if (mainMenu == null || pausePanel == null || hudPanel == null)
        {
            Debug.LogError("Game UI prefab missing. Assign MainMenu, PausePanel, and HudPanel assets in the Inspector.", this);
            enabled = false;
            return;
        }

        // Optional common dialogs may live inside any of the three prefab roots.
        confirmationPanel = FindObject(mainMenu, "ConfirmationPanel");
        pauseConfirmationPanel = FindObject(pausePanel, "ConfirmationPanel");
        hudConfirmationPanel = FindObject(hudPanel, "ConfirmationPanel");
        settingsPanel = FindObject(mainMenu, "SettingsPanel");
        pauseSettingsPanel = FindObject(pausePanel, "SettingsPanel");
        helpPanel = FindObject(mainMenu, "HelpPanel");
        pauseHelpPanel = FindObject(pausePanel, "HelpPanel");
        versionPanel = FindObject(mainMenu, "VersionPanel");
        dialoguePanel = FindObject(hudPanel, "DialoguePanel");
        resultPanel = FindObject(hudPanel, "ResultPanel");
        healthSlider = FindComponent<Slider>(hudPanel, "HealthSlider");
        healthText = FindComponent<Text>(hudPanel, "HealthValueText");
        confirmText = FindComponent<Text>(confirmationPanel, "MessageText");
        dialogueSpeaker = FindComponent<Text>(dialoguePanel, "SpeakerText");
        dialogueContent = FindComponent<Text>(dialoguePanel, "DialogueText");
        resultTitle = FindComponent<Text>(resultPanel, "ResultTitle");

        BindButtons();
        BindSettings();
        GameEventCenter.HealthChanged += OnHealthChanged;
        GameEventCenter.DialogueRequested += OnDialogueRequested;
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        GameEventCenter.HealthChanged -= OnHealthChanged;
        GameEventCenter.DialogueRequested -= OnDialogueRequested;
        if (GameStateManager.Current == GameState.Paused) Time.timeScale = 1f;
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

    private GameObject InstantiateUiPrefab(GameObject prefab, string instanceName)
    {
        if (prefab == null)
        {
            Debug.LogError($"GameFlowTemplate: assign the {instanceName} prefab in the Inspector.", this);
            return null;
        }
        var instance = Instantiate(prefab);
        instance.name = instanceName;
        return instance;
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
        Bind(mainMenu, "ExitButton", () => Confirm("确定退出游戏吗？", ExitGame));

        Bind(pausePanel, "ResumeButton", ResumeGame);
        Bind(pausePanel, "PauseSettingsButton", () => ShowPanel(pauseSettingsPanel));
        Bind(pausePanel, "PauseHelpButton", () => ShowPanel(pauseHelpPanel));
        Bind(pausePanel, "ResetButton", () => Confirm("重置当前关卡？当前进度将丢失。", ResetLevel));
        Bind(pausePanel, "ReturnToMenuButton", () => Confirm("返回主菜单？当前关卡进度将丢失。", ReturnToMainMenu));
        Bind(pausePanel, "PauseExitButton", () => Confirm("确定退出游戏吗？", ExitGame));

        Bind(hudPanel, "PauseButton", PauseGame);
        Bind(hudPanel, "DialogueButton", () => GameEventCenter.RequestDialogue("旁白", "这里展示剧情台词或任务提示。"));
        Bind(hudPanel, "DemoVictoryButton", ShowVictory);
        Bind(hudPanel, "DemoDefeatButton", ShowDefeat);
        Bind(hudPanel, "DemoDamageButton", () => SetHealth(currentHealth - 10f, startingHealth));

        Bind(confirmationPanel, "ConfirmButton", ConfirmAction);
        Bind(confirmationPanel, "CancelButton", CloseConfirmation);
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
        Bind(resultPanel, "ResultMenuButton", () => Confirm("返回主菜单？当前关卡进度将丢失。", ReturnToMainMenu));
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
        if (music != null) { music.SetValueWithoutNotify(audioManager.MusicVolume); music.onValueChanged.AddListener(audioManager.SetMusicVolume); }
        if (sfx != null) { sfx.SetValueWithoutNotify(audioManager.SfxVolume); sfx.onValueChanged.AddListener(audioManager.SetSfxVolume); }
    }

    private void StartGame()
    {
        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && gameplaySceneName != menuScene.name)
        {
            transitions.LoadLevelAdditive(gameplaySceneName, () =>
            {
                levelLoaded = true;
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(gameplaySceneName));
                SetMenuCamera(false);
                EnterGame();
            });
        }
        else transitions.FadeAction(EnterGame);
    }

    private void EnterGame()
    {
        GameStateManager.SetState(GameState.Playing);
        mainMenu.SetActive(false);
        pausePanel.SetActive(false);
        hudPanel.SetActive(true);
        SetHealth(startingHealth, startingHealth);
    }

    private void PauseGame()
    {
        if (GameStateManager.Current != GameState.Playing) return;
        GameStateManager.SetState(GameState.Paused);
        pausePanel.SetActive(true);
    }

    private void ResumeGame()
    {
        if (GameStateManager.Current != GameState.Paused) return;
        GameStateManager.SetState(GameState.Playing);
        pausePanel.SetActive(false);
    }

    private void ReturnToMainMenu()
    {
        Action finish = () =>
        {
            levelLoaded = false;
            if (menuScene.IsValid() && menuScene.isLoaded) SceneManager.SetActiveScene(menuScene);
            SetMenuCamera(true);
            ShowMainMenu();
        };
        if (levelLoaded) transitions.UnloadLevelAdditive(gameplaySceneName, finish);
        else transitions.FadeAction(finish);
    }

    private void ShowMainMenu()
    {
        GameStateManager.SetState(GameState.MainMenu);
        Time.timeScale = 1f;
        mainMenu.SetActive(true);
        hudPanel.SetActive(false);
        pausePanel.SetActive(false);
        HideOptionalPanels();
    }

    private void ResetLevel()
    {
        Action finish = () =>
        {
            SetHealth(startingHealth, startingHealth);
            GameEventCenter.PublishLevelReset();
            GameStateManager.SetState(GameState.Playing);
            pausePanel.SetActive(false);
            ShowPanel(resultPanel, false);
        };
        if (levelLoaded) transitions.ReloadLevelAdditive(gameplaySceneName, finish);
        else finish();
    }

    public void SetHealth(float current, float maximum)
    {
        maximum = Mathf.Max(1f, maximum);
        GameEventCenter.PublishHealth(Mathf.Clamp(current, 0f, maximum), maximum);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        currentHealth = current;
        if (healthSlider != null) healthSlider.value = maximum > 0f ? current / maximum : 0f;
        if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
    }

    public void ShowVictory() => ShowResult(true);
    public void ShowDefeat() => ShowResult(false);
    public void ShowDialogue(string speaker, string line) => GameEventCenter.RequestDialogue(speaker, line);

    private void ShowResult(bool victory)
    {
        GameStateManager.SetState(victory ? GameState.Victory : GameState.Defeat);
        if (resultTitle != null) resultTitle.text = victory ? "关卡完成" : "挑战失败";
        ShowPanel(resultPanel);
    }

    private void OnDialogueRequested(string speaker, string line)
    {
        if (dialogueSpeaker != null) dialogueSpeaker.text = speaker;
        if (dialogueContent != null) dialogueContent.text = line;
        ShowPanel(dialoguePanel);
    }

    private void Confirm(string message, Action action)
    {
        var panel = hudPanel.activeSelf && IsActive(resultPanel) ? hudConfirmationPanel : pausePanel.activeSelf ? pauseConfirmationPanel : confirmationPanel;
        if (panel == null) { Debug.LogError("确认弹窗缺失：请在对应 prefab 内添加 ConfirmationPanel。"); return; }
        confirmAction = action;
        var text = FindComponent<Text>(panel, "MessageText");
        if (text != null) text.text = message;
        ShowPanel(panel);
        panel.transform.SetAsLastSibling();
    }

    private void ConfirmAction() { var action = confirmAction; CloseConfirmation(); action?.Invoke(); }
    private bool IsAnyConfirmationOpen() => IsActive(confirmationPanel) || IsActive(pauseConfirmationPanel) || IsActive(hudConfirmationPanel);
    private void CloseConfirmation() { ShowPanel(confirmationPanel, false); ShowPanel(pauseConfirmationPanel, false); ShowPanel(hudConfirmationPanel, false); confirmAction = null; }
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
        button.onClick.AddListener(() => { audioManager.PlayUiClick(); action?.Invoke(); });
    }

    private static GameObject FindObject(GameObject root, string objectName)
    {
        if (root == null) return null;
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
