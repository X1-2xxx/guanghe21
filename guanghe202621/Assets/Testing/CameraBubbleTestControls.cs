using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>独立测试场景的操作面板和快捷键，使用 IMGUI，无需制作额外 UI 资源。</summary>
public sealed class CameraBubbleTestControls : MonoBehaviour
{
    [SerializeField] private CameraPerformanceManager cameraPerformance;
    [SerializeField] private BubbleDialogManager bubbles;
    [SerializeField] private BubbleDialogSequence npcDialog;
    [SerializeField] private CameraBubbleTestPlayer player;
    [SerializeField] private CameraBubbleTestFollow cameraFollow;
    [SerializeField, InspectorName("进入测试场景后自动播放镜头")]
    private bool autoPlayCameraOnStart = true;

    private float previousTimeScale = 1f;

    private void Awake()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        // Start 在所有组件的 Awake 完成后执行，此时相机单例与引用均已初始化。
        if (autoPlayCameraOnStart) PlayCameraPerformance();
    }

    private void OnDestroy() => Time.timeScale = previousTimeScale;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame) PlayIntro();
        if (keyboard.digit2Key.wasPressedThisFrame) PlayQuestDone();
        if (keyboard.digit3Key.wasPressedThisFrame) TestOverlap();
        if (keyboard.digit4Key.wasPressedThisFrame) PlayCameraPerformance();
        if (keyboard.digit5Key.wasPressedThisFrame) cameraPerformance.StopPerformance();
        if (keyboard.digit6Key.wasPressedThisFrame) ResetTest();
        if (keyboard.digit7Key.wasPressedThisFrame) ClearDialogs();
        if (keyboard.eKey.wasPressedThisFrame) npcDialog.PlayNextConversation();
        if (keyboard.pKey.wasPressedThisFrame) TogglePause();
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayIntro();
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayQuestDone();
        if (Input.GetKeyDown(KeyCode.Alpha3)) TestOverlap();
        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayCameraPerformance();
        if (Input.GetKeyDown(KeyCode.Alpha5)) cameraPerformance.StopPerformance();
        if (Input.GetKeyDown(KeyCode.Alpha6)) ResetTest();
        if (Input.GetKeyDown(KeyCode.Alpha7)) ClearDialogs();
        if (Input.GetKeyDown(KeyCode.E)) npcDialog.PlayNextConversation();
        if (Input.GetKeyDown(KeyCode.P)) TogglePause();
#endif
    }

    // 面板只存在于该独立测试场景，方便直接点击验证各项功能。
    private void OnGUI()
    {
        Font previousFont = GUI.skin.font;
        int previousLabelSize = GUI.skin.label.fontSize;
        int previousButtonSize = GUI.skin.button.fontSize;
        GUI.skin.font = DialogueFontUtility.GetFallbackFont();
        GUI.skin.label.fontSize = 16;
        GUI.skin.button.fontSize = 16;
        GUILayout.BeginArea(new Rect(12f, 12f, 330f, 520f), GUI.skin.box);
        GUILayout.Label("相机演出与底部对话测试");
        GUILayout.Label("WASD / 方向键：移动蓝色玩家");
        GUILayout.Label("进入浅黄色范围：播放一段对话");
        GUILayout.Label("本段结束后，离开再靠近：下一段");
        GUILayout.Label("E：下一段对话　P：暂停 / 继续");
        GUILayout.Space(8f);
        if (GUILayout.Button("1　初次对话", GUILayout.Height(30f))) PlayIntro();
        if (GUILayout.Button("2　后续对话", GUILayout.Height(30f))) PlayQuestDone();
        if (GUILayout.Button("3　连续三句旁白", GUILayout.Height(30f))) TestOverlap();
        if (GUILayout.Button("4　聚焦目标，再自动返回玩家", GUILayout.Height(30f)))
            PlayCameraPerformance();
        if (GUILayout.Button("5　中断演出并返回玩家", GUILayout.Height(30f)))
            cameraPerformance.StopPerformance();
        if (GUILayout.Button("6　重置测试", GUILayout.Height(30f))) ResetTest();
        if (GUILayout.Button("7　清空对话", GUILayout.Height(30f))) ClearDialogs();
        if (GUILayout.Button("P　暂停 / 继续", GUILayout.Height(30f))) TogglePause();
        GUILayout.Space(8f);
        GUILayout.Label("镜头演出：" + cameraPerformance.StatusText);
        GUILayout.Label("玩家相机跟随：" + (cameraFollow.enabled ? "已开启" : "已暂时关闭"));
        GUILayout.Label("游戏时间：" + (Time.timeScale > 0f ? "正常" : "暂停"));
        GUILayout.Label("修改台词：选中“对话角色_黄色”。");
        GUILayout.EndArea();
        GUI.skin.font = previousFont;
        GUI.skin.label.fontSize = previousLabelSize;
        GUI.skin.button.fontSize = previousButtonSize;
    }

    public void PlayIntro()
    {
        ClearDialogs();
        npcDialog.PlayConversation("intro");
    }

    public void PlayCameraPerformance()
    {
        if (cameraPerformance == null)
        {
            Debug.LogError("测试控制器没有绑定相机演出管理器。", this);
            return;
        }
        cameraPerformance.PlayConfiguredPerformance();
    }

    public void PlayQuestDone()
    {
        ClearDialogs();
        npcDialog.PlayConversation("quest_done");
    }

    public void TestOverlap()
    {
        ClearDialogs();
        bubbles.ShowDialogue("旁白", "第一句旁白显示在屏幕底部，镜头移动时对话框保持在原处。", 3f, true);
        bubbles.ShowDialogue("旁白", "第二句会等待第一句结束，不会和第一句重叠。", 3f, true);
        bubbles.ShowDialogue("旁白", "第三句说明：同一套对话框可以在后续剧情中反复使用。", 3f, false);
    }

    public void ClearDialogs()
    {
        npcDialog.StopConversation();
        bubbles.ClearAll();
    }

    public void ResetTest()
    {
        Time.timeScale = 1f;
        cameraPerformance.StopPerformance();
        ClearDialogs();
        npcDialog.ResetProgress();
        player.ResetPosition();
        cameraFollow.SnapToTarget();
    }

    private static void TogglePause() => Time.timeScale = Time.timeScale > 0f ? 0f : 1f;
}
