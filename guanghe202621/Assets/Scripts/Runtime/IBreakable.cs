using UnityEngine;

namespace LevelKit
{
    public interface IBreakable
    {
        // Return true after accepting the hit. The receiving object owns its animation and collider lifetime.
        bool TryBreak(PlayerActionController attacker, Vector2 hitPoint, Vector2 direction);
    }
}
