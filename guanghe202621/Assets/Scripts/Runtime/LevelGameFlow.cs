using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LevelKit
{
    /// <summary>Connects the menu framework to a hand-built gameplay scene without replacing its player or level objects.</summary>
    [DefaultExecutionOrder(-300), DisallowMultipleComponent]
    [RequireComponent(typeof(GameFlowTemplate))]
    [AddComponentMenu("Level Kit/Level Game Flow")]
    public sealed class LevelGameFlow : MonoBehaviour
    {
        [SerializeField] private GameFlowTemplate flow;
        [Tooltip("返回菜单后重新加载本关卡；再次开始时使用干净的关卡状态。")]
        [SerializeField] private bool resetOnReturnToMenu = true;

        private static readonly Dictionary<Scene, LevelGameFlow> scenes = new Dictionary<Scene, LevelGameFlow>();
        private static readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        private static EventSystem pointerEventSystem;
        private static PointerEventData pointer;
        private static string pendingScene;
        private static bool pendingStart;

        private bool hasStarted;
        private bool reloading;
        private bool startAfterLoad;
        private int blockThroughFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            scenes.Clear(); uiHits.Clear();
            pointerEventSystem = null; pointer = null;
            pendingScene = null; pendingStart = false;
        }

        private void Awake()
        {
            if (!flow) flow = GetComponent<GameFlowTemplate>();
            if (pendingScene == gameObject.scene.path)
            {
                startAfterLoad = pendingStart;
                pendingScene = null;
                pendingStart = false;
            }
        }

        private void OnEnable()
        {
            scenes[gameObject.scene] = this;
            GameEventCenter.StateChanged += OnStateChanged;
            GameEventCenter.LevelReset += OnLevelReset;
        }

        private void Start()
        {
            if (startAfterLoad) flow.StartGame();
            else ApplyTimeScale(GameStateManager.Current);
        }

        private void OnStateChanged(GameState state)
        {
            blockThroughFrame = Time.frameCount;
            ApplyTimeScale(state);
            if (state == GameState.Playing) hasStarted = true;
            else if (state == GameState.MainMenu && hasStarted && resetOnReturnToMenu)
                BeginReload(false);
        }

        private void ApplyTimeScale(GameState state) =>
            Time.timeScale = !reloading && state == GameState.Playing &&
                (!flow || !flow.HasOpenGameplayModal) ? 1f : 0f;

        private void LateUpdate() => ApplyTimeScale(GameStateManager.Current);

        private void OnLevelReset() => BeginReload(true);

        private void BeginReload(bool enterGame)
        {
            if (reloading) return;
            string path = gameObject.scene.path;
            if (string.IsNullOrEmpty(path) || !Application.CanStreamedLevelBeLoaded(path))
            {
                Debug.LogError("请先把当前关卡添加到 Build Profiles 的场景列表，再使用重置或返回菜单。", this);
                return;
            }
            reloading = true;
            pendingScene = path;
            pendingStart = enterGame;
            Time.timeScale = 0f;
            StartCoroutine(ReloadScene(path));
        }

        private IEnumerator ReloadScene(string path)
        {
            // Let the button callback finish before replacing its scene.
            yield return null;
            SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }

        public static bool BlocksGameplayInput(Component source)
        {
            var owner = Find(source);
            return PlayerOpeningSequence.BlocksGameplayInput(source) || owner && (owner.reloading || GameStateManager.Current != GameState.Playing ||
                Time.timeScale <= 0f || Time.frameCount <= owner.blockThroughFrame ||
                (owner.flow && (owner.flow.HasOpenGameplayModal || owner.flow.LastUiInteractionFrame == Time.frameCount)));
        }

        public static bool PointerOverUi(Component source)
        {
            if (!Find(source) || !EventSystem.current || Mouse.current == null) return false;
            if (pointerEventSystem != EventSystem.current || pointer == null)
            {
                pointerEventSystem = EventSystem.current;
                pointer = new PointerEventData(pointerEventSystem);
            }
            pointer.Reset();
            pointer.position = Mouse.current.position.ReadValue();
            uiHits.Clear();
            pointerEventSystem.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is GraphicRaycaster && hit.gameObject && hit.gameObject.activeInHierarchy)
                    return true;
            return false;
        }

        private static LevelGameFlow Find(Component source)
        {
            if (!source) return null;
            scenes.TryGetValue(source.gameObject.scene, out var owner);
            return owner && owner.isActiveAndEnabled ? owner : null;
        }

        private void OnDisable()
        {
            GameEventCenter.StateChanged -= OnStateChanged;
            GameEventCenter.LevelReset -= OnLevelReset;
            if (scenes.TryGetValue(gameObject.scene, out var owner) && owner == this)
                scenes.Remove(gameObject.scene);
            if (scenes.Count == 0) Time.timeScale = 1f;
        }
    }
}
