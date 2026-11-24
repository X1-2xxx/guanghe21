using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Inn Quest Item")]
    public sealed class InnQuestItem : MonoBehaviour
    {
        [SerializeField] private InnQuest quest;
        [SerializeField, Range(1, 3)] private int itemNumber = 1;
        private bool collected;

        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void TryCollect(Collider2D other)
        {
            if (collected || !isActiveAndEnabled || !quest) return;
            var body = other.attachedRigidbody;
            if (!body || !body.TryGetComponent<PlayerActionController>(out var player) || !player.isActiveAndEnabled ||
                !quest.TryCollect(player, itemNumber)) return;
            collected = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
