using UnityEngine;

namespace GamePresentation
{
    /// <summary>Prefer the centered overhead bubble; side artwork is used only when a screen edge forces a move.</summary>
    public static class HeadBubbleScreenLayout
    {
        public enum Artwork { Above, Left, Right }
        public readonly struct Result
        {
            public readonly Vector2 center;
            public readonly Artwork artwork;
            public Result(Vector2 center, Artwork artwork) { this.center = center; this.artwork = artwork; }
        }
        public static Rect VisibleArea(Camera camera, float margin)
        {
            float height = camera.orthographicSize * 2f, width = height * camera.aspect;
            margin = Mathf.Clamp(margin, 0f, Mathf.Min(width, height) * .2f);
            return new Rect(camera.transform.position.x - width * .5f + margin,
                camera.transform.position.y - height * .5f + margin, width - margin * 2f, height - margin * 2f);
        }
        public static Result Solve(Vector2 head, Vector2 size, Rect visible, float gap)
        {
            var above = head + Vector2.up * (Mathf.Max(0f, gap) + size.y * .5f);
            if (Fits(above, size, visible)) return new Result(above, Artwork.Above);
            // Side versions put the tail near the speaker: right-moving paper has its tail on the left.
            bool moveRight = above.x - size.x * .5f < visible.xMin ? true :
                above.x + size.x * .5f > visible.xMax ? false : head.x <= visible.center.x;
            var center = above + Vector2.right * size.x * (moveRight ? .35f : -.35f);
            center.x = ClampCenter(center.x, size.x, visible.xMin, visible.xMax);
            center.y = ClampCenter(center.y, size.y, visible.yMin, visible.yMax);
            if (center.x > head.x + .001f) moveRight = true;
            else if (center.x < head.x - .001f) moveRight = false;
            return new Result(center, moveRight ? Artwork.Right : Artwork.Left);
        }
        static float ClampCenter(float value, float size, float min, float max)
            => size >= max - min ? (min + max) * .5f : Mathf.Clamp(value, min + size * .5f, max - size * .5f);
        public static bool Fits(Vector2 center, Vector2 size, Rect visible)
            => center.x - size.x * .5f >= visible.xMin - .0001f && center.x + size.x * .5f <= visible.xMax + .0001f &&
               center.y - size.y * .5f >= visible.yMin - .0001f && center.y + size.y * .5f <= visible.yMax + .0001f;
    }
}
