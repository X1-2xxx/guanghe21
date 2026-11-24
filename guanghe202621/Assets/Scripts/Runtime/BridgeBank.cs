using UnityEngine;

namespace LevelKit
{
    // Identifies a fixed bank as a valid world anchor for manually taped blocks.
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Bridge Bank")]
    public sealed class BridgeBank : MonoBehaviour { }
}
