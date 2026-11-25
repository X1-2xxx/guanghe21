using System;
using LevelKit;
using UnityEngine;

namespace GamePresentation
{
    /// <summary>每关只配置场景引用；镜头目标用稳定的标识关联，不依赖物体名称。</summary>
    public sealed class PresentationSceneBinding : MonoBehaviour
    {
        [Serializable] public sealed class Target
        {
            [InspectorName("目标标识")] public string key;
            [InspectorName("场景物体")] public Transform value;
            [InspectorName("仅水平移动")] public bool horizontalOnly;
            [Min(0f), InspectorName("演出正交尺寸"), Tooltip("0 沿用当前尺寸。数值越小，画面越近。")]
            public float orthographicSize;
        }
        [InspectorName("启用 V4 主线（关闭后只保留教学和小台词）")] public bool enableMainline = true;
        [InspectorName("包含树林歪树教学")] public bool includeForest = true;
        [InspectorName("本关自动播放的章节（留空只用触发器）")] public string initialEpisode = "village.opening";
        [InspectorName("演出管理器预制体")] public PresentationRuntime runtimePrefab;
        [InspectorName("玩家")] public PlayerActionController player;
        [InspectorName("相机")] public Camera levelCamera;
        [InspectorName("入口关卡镜头")] public CameraTarget entranceCamera;
        [InspectorName("村口石头")] public Rigidbody2D villageStone;
        [InspectorName("村口坑范围（世界坐标）")] public Rect villagePit = new Rect(-16.05f, -5.1f, 2.1f, 2.3f);
        [InspectorName("村口地面高度")] public float villageGroundY = -3f;
        [InspectorName("从左到右的火堆")] public Campfire[] checkpoints;
        [InspectorName("镜头目标")] public Target[] targets;
        [Header("旧旅馆任务兼容：只在启用 V4 主线时生效")]
        [InspectorName("旧收集任务组件（保留原代码）")] public Behaviour legacyInnQuest;
        [InspectorName("旧任务道具（V4 模式隐藏）")] public GameObject[] legacyQuestItems;
        [InspectorName("马头跑鞋拾取物")] public GameObject shoes;
        [InspectorName("V4 跑鞋摆放点")] public Transform shoesPoint;

        private void Start()
        {
            var runtime = PresentationRuntime.Instance;
            if (!runtime && runtimePrefab) runtime = Instantiate(runtimePrefab);
            if (runtime) runtime.Attach(this);
            else Debug.LogError("请给剧情场景绑定器拖入 PresentationRuntime 预制体。", this);
        }
        public Transform FindTarget(string key) => FindCameraTarget(key)?.value;

        public Target FindCameraTarget(string key)
        {
            if (targets != null) foreach (var target in targets)
                if (target != null && target.key == key) return target;
            return null;
        }
        private void OnDestroy()
        {
            if (PresentationRuntime.Instance) PresentationRuntime.Instance.Detach(this);
        }
    }
}
