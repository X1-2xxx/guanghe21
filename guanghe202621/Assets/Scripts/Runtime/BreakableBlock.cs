using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Breakable Block")]
    public sealed class BreakableBlock : MonoBehaviour, IBreakable
    {
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.2f;

        private SpriteRenderer visual;
        private BoxCollider2D hitbox;
        private Color restingColor;
        private bool breaking;
        private float timeLeft;

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            hitbox = GetComponent<BoxCollider2D>();
        }

        public bool TryBreak(PlayerActionController attacker, Vector2 hitPoint, Vector2 direction)
        {
            if (!attacker || !isActiveAndEnabled) return false;
            if (breaking) return true;

            breaking = true;
            timeLeft = flashSeconds;
            restingColor = visual.color;
            visual.color = new Color(1f, 1f, 1f, restingColor.a);
            return true;
        }

        private void Update() => AdvanceFlash(Time.deltaTime);

        private void AdvanceFlash(float deltaTime)
        {
            if (!breaking) return;
            float previous = timeLeft;
            timeLeft -= deltaTime;
            if (previous > flashSeconds * 0.5f && timeLeft <= flashSeconds * 0.5f)
                visual.color = restingColor;
            if (timeLeft > 0f) return;

            hitbox.enabled = false;
            visual.enabled = false;
            breaking = false;
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (!breaking) return;
            if (visual) visual.color = restingColor;
            breaking = false;
        }
    }
}
