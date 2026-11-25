using UnityEngine;

namespace GamePresentation
{
    /// <summary>镜头演出返回原来的玩家视野；不需要关闭整个玩家控制器。</summary>
    public sealed class PresentationCameraAnchor : MonoBehaviour, ICameraFollowPositionProvider
    {
        private Transform player;
        private bool captured;
        private Vector3 cameraStart, playerStart;
        public void Capture(Camera camera, Transform actor)
        { player = actor; cameraStart = camera.transform.position; playerStart = actor.position; captured = true; }
        public bool TryGetFollowPosition(out Vector3 position)
        {
            if (!captured || !player) { position = default; return false; }
            if (player && player.TryGetComponent<LevelKit.PlayerActionController>(out var controller) && controller.TryGetPresentationCameraPosition(out position)) return true;
            position = cameraStart + (player ? player.position - playerStart : Vector3.zero); position.z = cameraStart.z; return true;
        }
    }
}
