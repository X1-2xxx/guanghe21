using UnityEngine;

namespace LevelKit
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    [AddComponentMenu("Level Kit/Breakable Block")]
    public sealed class BreakableBlock : MonoBehaviour, IBreakable
    {
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.2f;
        [SerializeField, InspectorName("击碎后闪烁再消失")] private bool blinkBeforeDisappear;
        [SerializeField, Min(0.03f), InspectorName("闪烁间隔秒数")] private float blinkInterval = .08f;

        private SpriteRenderer visual;
        private BoxCollider2D hitbox;
        private Color restingColor;
        private bool breaking;
        private float timeLeft;
        private SpriteRenderer[] blinkVisuals;
        private bool[] restingVisibility;
        private bool hitboxWasEnabled;

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
            hitboxWasEnabled = hitbox.enabled;
            if (blinkBeforeDisappear)
            {
                blinkVisuals = GetComponentsInChildren<SpriteRenderer>(true);
                restingVisibility = new bool[blinkVisuals.Length];
                for (int i = 0; i < blinkVisuals.Length; i++) restingVisibility[i] = blinkVisuals[i].enabled;
                // Accept the impact once and let the charge pass while the artwork finishes blinking.
                hitbox.enabled = false;
            }
            else visual.color = new Color(1f, 1f, 1f, restingColor.a);
            GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.WallBroken, attacker, this);
            return true;
        }

        private void Update()
        {
            // Story text may pause physics while the accepted hit is still being presented.
            float delta = blinkBeforeDisappear ?
                (GameStateManager.Current == GameState.Playing ? Time.unscaledDeltaTime : 0f) : Time.deltaTime;
            AdvanceFlash(delta);
        }

        private void AdvanceFlash(float deltaTime)
        {
            if (!breaking) return;
            float previous = timeLeft;
            timeLeft -= deltaTime;
            if (blinkBeforeDisappear)
            {
                bool visible = Mathf.FloorToInt((flashSeconds - timeLeft) / Mathf.Max(.03f, blinkInterval)) % 2 == 0;
                SetBlinkVisibility(visible);
            }
            else if (previous > flashSeconds * 0.5f && timeLeft <= flashSeconds * 0.5f)
                visual.color = restingColor;
            if (timeLeft > 0f) return;

            hitbox.enabled = false;
            if (blinkBeforeDisappear) SetBlinkVisibility(false);
            else visual.enabled = false;
            breaking = false;
            Destroy(gameObject);
        }

        private void SetBlinkVisibility(bool visible)
        {
            if (blinkVisuals == null) return;
            for (int i = 0; i < blinkVisuals.Length; i++)
                if (blinkVisuals[i]) blinkVisuals[i].enabled = restingVisibility[i] && visible;
        }

        private void OnDisable()
        {
            if (!breaking) return;
            if (visual) visual.color = restingColor;
            if (blinkBeforeDisappear)
            {
                SetBlinkVisibility(true);
                if (hitbox) hitbox.enabled = hitboxWasEnabled;
            }
            breaking = false;
        }
    }
}
