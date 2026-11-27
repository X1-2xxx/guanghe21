using UnityEngine;

/// <summary>玩法与 UI 共用的道具标识，不依赖预制体名称或 Hierarchy 路径。</summary>
public enum GameItemId
{
    [InspectorName("硬纸板（剧情解锁）")] Cardboard = 0,
    [InspectorName("跳跃键")] JumpKey = 1,
    [InspectorName("驽骍难得（跑鞋）")] Rocinante = 2
}
