using GamePresentation;
using UnityEngine;

namespace LevelKit
{
    [DefaultExecutionOrder(50), DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Level Kit/Player Speech Bubble")]
    public sealed class PlayerSpeechBubble : MonoBehaviour
    {
        public enum Placement { Right, Left, Above }
        [Header("Artwork")]
        [SerializeField] private Sprite aboveSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;
        [SerializeField] private int sortingOrder = 100;
        [Header("Size and distance (world units)")]
        [SerializeField, Min(.2f)] private float bubbleWidth = 2.6f;
        [SerializeField, Min(0f)] private float aboveGap = .12f;
        [SerializeField, Min(0f)] private float screenPaddingPixels = 24f;
        [Header("Speaker")]
        [SerializeField] private PlayerActionController owner;
        [SerializeField] private Collider2D speakerCollider;
        [SerializeField] private Camera levelCamera;
        private SpriteRenderer bubbleRenderer;
        public Placement CurrentPlacement { get; private set; } = Placement.Above;
        public Rect CurrentWorldRect { get; private set; }
        public float CurrentOcclusion => 0f;
        public void Initialize(PlayerActionController speaker, Collider2D hitbox, Camera view)
        { owner = speaker; speakerCollider = hitbox; if (view) levelCamera = view; Cache(); }
        public void Show() { Cache(); gameObject.SetActive(true); UpdatePlacement(); }
        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); }
        private void Awake() => Cache();
        private void OnEnable() => Cache();
        private void Cache()
        {
            if (!bubbleRenderer) bubbleRenderer = GetComponent<SpriteRenderer>();
            if (!owner) owner = GetComponentInParent<PlayerActionController>();
            if (!speakerCollider && owner) speakerCollider = owner.GetComponent<CapsuleCollider2D>();
            if (!levelCamera) levelCamera = Camera.main;
        }
        private void LateUpdate() => UpdatePlacement();
        private void UpdatePlacement()
        {
            if (!speakerCollider || !bubbleRenderer) return;
            var primary = aboveSprite ? aboveSprite : bubbleRenderer.sprite;
            if (!primary) return;
            float aspect = primary.bounds.size.x / Mathf.Max(.001f, primary.bounds.size.y);
            var size = new Vector2(bubbleWidth, bubbleWidth / aspect);
            var head = new Vector2(speakerCollider.bounds.center.x, speakerCollider.bounds.max.y);
            var result = new HeadBubbleScreenLayout.Result(head + Vector2.up * (aboveGap + size.y * .5f), HeadBubbleScreenLayout.Artwork.Above);
            if (levelCamera && levelCamera.orthographic)
            {
                float margin = screenPaddingPixels * levelCamera.orthographicSize * 2f / Mathf.Max(1, levelCamera.pixelHeight);
                var visible = HeadBubbleScreenLayout.VisibleArea(levelCamera, margin);
                size *= Mathf.Min(1f, visible.width / size.x, visible.height / size.y);
                result = HeadBubbleScreenLayout.Solve(head, size, visible, aboveGap);
            }
            var sprite = result.artwork == HeadBubbleScreenLayout.Artwork.Left ? leftSprite : result.artwork == HeadBubbleScreenLayout.Artwork.Right ? rightSprite : primary;
            if (!sprite) sprite = primary;
            CurrentPlacement = result.artwork == HeadBubbleScreenLayout.Artwork.Left ? Placement.Left : result.artwork == HeadBubbleScreenLayout.Artwork.Right ? Placement.Right : Placement.Above;
            CurrentWorldRect = new Rect(result.center - size * .5f, size);
            bubbleRenderer.sprite = sprite; bubbleRenderer.drawMode = SpriteDrawMode.Simple;
            bubbleRenderer.flipX = bubbleRenderer.flipY = false; bubbleRenderer.color = Color.white; bubbleRenderer.sortingOrder = sortingOrder;
            var parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(size.x / Mathf.Max(.001f, sprite.bounds.size.x) / Safe(parentScale.x),
                size.y / Mathf.Max(.001f, sprite.bounds.size.y) / Safe(parentScale.y), 1f / Safe(parentScale.z));
            float z = owner ? owner.transform.position.z : transform.position.z;
            transform.position = new Vector3(result.center.x, result.center.y, z) - transform.TransformVector(sprite.bounds.center);
        }
        private static float Safe(float value) => Mathf.Abs(value) < .0001f ? .0001f : value;
    }
}
