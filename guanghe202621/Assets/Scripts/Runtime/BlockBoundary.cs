using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LevelKit
{
    [DisallowMultipleComponent, AddComponentMenu("Level Kit/Block Boundary")]
    public sealed class BlockBoundary : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 8f;
        [SerializeField, Min(0f)] private float warningDistance = 0.8f;
        [SerializeField, Min(0.05f)] private float warningSeconds = 0.65f;
        [SerializeField, Min(0.01f)] private float outlineWidth = 0.045f;
        [SerializeField] private Color outlineColor = new Color(1f, 0.7f, 0.23f, 0.92f);
        [SerializeField] private Material outlineMaterial;

        private const int Segments = 128;
        private const float WarningHysteresis = 0.08f;
        private readonly Vector3[] vertices = new Vector3[Segments];
        private readonly List<Collider2D> colliders = new List<Collider2D>();
        private readonly PhysicsShapeGroup2D shapes = new PhysicsShapeGroup2D();
        private Vector3 origin;
        private bool returnedWhileOutside;
        private bool wasNear;
        private float warningUntil;
        private LineRenderer outline;
        private Material generatedMaterial;

        internal void SetOrigin(Vector3 value) => origin = value;

        internal bool ShouldReturn(Rigidbody2D body, Rigidbody2D held)
        {
            if (!body || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic) return false;
            if (held && body != held) warningUntil = 0f;
            bool isHeld = body == held;
            Vector2 center = origin;
            float radiusSquared = radius * radius;
            float warningRadius = Mathf.Max(0f, radius - warningDistance - (wasNear ? WarningHysteresis : 0f));
            bool isHeldNear = false;
            bool hasSolid = false;
            bool intersects = false;
            body.GetAttachedColliders(colliders);
            foreach (var collider in colliders)
            {
                if (!collider || !collider.isActiveAndEnabled || collider.isTrigger || collider.shapeCount == 0) continue;
                hasSolid = true;
                if (!intersects)
                    intersects = (collider.ClosestPoint(center) - center).sqrMagnitude <= radiusSquared;
                if (isHeld && !isHeldNear)
                    isHeldNear = ReachesWarningRadius(collider, center, warningRadius);
            }

            if (isHeldNear && !wasNear) warningUntil = Time.unscaledTime + warningSeconds;
            wasNear = isHeldNear;
            if (intersects) returnedWhileOutside = false;
            if (!hasSolid || intersects || returnedWhileOutside) return false;
            returnedWhileOutside = true;
            return true;
        }

        private bool ReachesWarningRadius(Collider2D collider, Vector2 center, float warningRadius)
        {
            collider.GetShapes(shapes);
            var matrix = shapes.localToWorldMatrix;
            for (int index = 0; index < shapes.shapeCount; index++)
            {
                var shape = shapes.GetShape(index);
                float limit = warningRadius - shape.radius;
                if (limit <= 0f) return true;
                float limitSquared = limit * limit;
                for (int vertex = 0; vertex < shape.vertexCount; vertex++)
                {
                    Vector2 point = matrix.MultiplyPoint3x4(shapes.GetShapeVertex(index, vertex));
                    if ((point - center).sqrMagnitude >= limitSquared) return true;
                }
            }
            return false;
        }

        private void LateUpdate()
        {
            float remaining = warningUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                if (outline) outline.enabled = false;
                return;
            }

            if (!outline) CreateOutline();
            for (int i = 0; i < vertices.Length; i++) vertices[i] = CirclePoint(origin, radius, i);
            outline.SetPositions(vertices);
            var color = outlineColor;
            color.a *= Mathf.Clamp01(remaining / Mathf.Min(0.18f, warningSeconds));
            outline.startColor = outline.endColor = color;
            outline.enabled = true;
        }

        private void OnDisable()
        {
            returnedWhileOutside = false;
            wasNear = false;
            warningUntil = 0f;
            if (outline) outline.enabled = false;
        }

        private void OnDestroy()
        {
            if (outline) Destroy(outline.gameObject);
            if (generatedMaterial) Destroy(generatedMaterial);
        }

        private void CreateOutline()
        {
            var visual = new GameObject("Block Boundary Outline") { layer = gameObject.layer };
            visual.transform.SetParent(transform, false);
            outline = visual.AddComponent<LineRenderer>();
            outline.loop = true;
            outline.useWorldSpace = true;
            outline.positionCount = Segments;
            outline.startWidth = outline.endWidth = outlineWidth;
            outline.sortingOrder = 45;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.sharedMaterial = outlineMaterial;
            if (!outlineMaterial)
            {
                var shader = GraphicsSettings.currentRenderPipeline
                    ? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") : null;
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (shader)
                {
                    generatedMaterial = new Material(shader);
                    outline.sharedMaterial = generatedMaterial;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            var old = Gizmos.color;
            Gizmos.color = outlineColor;
            Vector3 center = Application.isPlaying && TryGetComponent<ManipulableBlock>(out _) ? origin : transform.position;
            var previous = CirclePoint(center, radius, Segments - 1);
            for (int i = 0; i < Segments; i++)
            {
                var point = CirclePoint(center, radius, i);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.color = old;
        }

        private static Vector3 CirclePoint(Vector3 center, float radius, int index)
        {
            float angle = index * (2f * Mathf.PI / Segments);
            return center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
    }
}
