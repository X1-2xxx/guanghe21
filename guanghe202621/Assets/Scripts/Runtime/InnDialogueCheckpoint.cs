using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Inn Dialogue Checkpoint")]
    public sealed class InnDialogueCheckpoint : MonoBehaviour
    {
        [SerializeField] private InnQuest quest;

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!quest || !isActiveAndEnabled) return;
            var body = other.attachedRigidbody;
            if (body && body.TryGetComponent<PlayerActionController>(out var player) && player.isActiveAndEnabled)
                quest.TryTalk(player);
        }
    }
}
