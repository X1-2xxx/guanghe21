using LevelKit;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>只把正式关卡已有的角色能力同步给 UI，不修改队友的角色、拾取或任务逻辑。</summary>
[DisallowMultipleComponent]
public sealed class LevelUiCompatibility : MonoBehaviour
{
    [SerializeField, HideInInspector] private PlayerActionController player;
    private float nextSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        RegisterSceneCallback();
        for (int i = 0; i < SceneManager.sceneCount; i++) Attach(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= Attach;
        SceneManager.sceneLoaded += Attach;
    }

    private static void Attach(Scene scene, LoadSceneMode mode)
    {
        if (!scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LevelGameFlow flow in root.GetComponentsInChildren<LevelGameFlow>(true))
                if (flow.GetComponent<LevelUiCompatibility>() == null) flow.gameObject.AddComponent<LevelUiCompatibility>();
    }

    private void OnEnable()
    {
        RegisterSceneCallback();
        nextSearch = 0f;
        Sync();
    }

    private void LateUpdate() => Sync();

    private void Sync()
    {
        if (player == null)
        {
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                player = root.GetComponentInChildren<PlayerActionController>(true);
                if (player != null) break;
            }
        }
        if (player == null) return;
        // 玩家能力是唯一依据；重置关卡后，新角色也会把旧 UI 解锁状态一并复原。
        if (ItemUnlockManager.IsUnlocked(GameItemId.JumpKey) != player.JumpEnabled)
            ItemUnlockManager.SetUnlocked(GameItemId.JumpKey, player.JumpEnabled);
        if (ItemUnlockManager.IsUnlocked(GameItemId.Rocinante) != player.BreakEnabled)
            ItemUnlockManager.SetUnlocked(GameItemId.Rocinante, player.BreakEnabled);
    }
}
