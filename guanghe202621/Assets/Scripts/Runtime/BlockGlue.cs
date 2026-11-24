using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LevelKit
{
    internal sealed class BlockGlue
    {
        private const float GlueDistance = 0.18f;
        private const float MaxOverlap = 0.06f;
        private const float SteadySeconds = 0.3f;
        private const float SteadySpeed = 0.55f;
        private const float SteadyAngularSpeed = 25f;
        private const float RemoveRadius = 0.42f;
        private const float TapeSize = 0.36f;

        private static readonly Color TapeColor = new Color(0.92f, 0.52f, 0.23f);
        private static readonly Color PreviewColor = new Color(0.96f, 0.72f, 0.25f, 0.8f);
        private static readonly Color ReadyColor = new Color(0.25f, 0.75f, 0.46f, 0.9f);

        private sealed class Link
        {
            public Collider2D Source, Target;
            public Rigidbody2D BodyA, BlockB;
            public bool IsPlatform;
            public HingeJoint2D Joint;
            public SpriteRenderer Mark;
            public float RestAngle;

            public bool IsValid => Joint && Joint.enabled && BodyA && BodyA.simulated
                && (IsPlatform || (BlockB && BlockB.simulated))
                && IsSolid(Source) && IsSolid(Target);
            public Vector2 Point => (BodyToWorld(BodyA, Joint.anchor) +
                (Joint.connectedBody ? BodyToWorld(Joint.connectedBody, Joint.connectedAnchor) : Joint.connectedAnchor)) * 0.5f;
        }

        private readonly Transform owner;
        private readonly PhysicsScene2D physicsScene;
        private readonly Sprite configuredSprite;
        private readonly float tapeWidth;
        private readonly List<Link> links = new List<Link>();
        private readonly HashSet<Rigidbody2D> group = new HashSet<Rigidbody2D>();
        private readonly Queue<Rigidbody2D> pending = new Queue<Rigidbody2D>();
        private readonly List<Collider2D> sources = new List<Collider2D>();
        private readonly List<Collider2D> nearby = new List<Collider2D>();
        private readonly ContactFilter2D solidFilter = new ContactFilter2D { useTriggers = false };

        private Collider2D candidateSource, candidateTarget;
        private Vector2 candidatePoint;
        private float steadyTime;
        private SpriteRenderer preview;
        private Sprite defaultSquare;

        public BlockGlue(Transform owner, PhysicsScene2D physicsScene, Sprite configuredSprite, float tapeWidth = 0.5f)
        {
            this.owner = owner;
            this.physicsScene = physicsScene;
            this.configuredSprite = configuredSprite;
            this.tapeWidth = Mathf.Max(0.05f, tapeWidth);
        }

        private bool IsReady => steadyTime >= SteadySeconds;

        public bool IsLinkedToBalancePlatform(Rigidbody2D body)
        {
            group.Clear();
            group.Add(body);
            ExpandGroup();
            foreach (var member in group)
                if (member && member.TryGetComponent<BalancePlatform>(out _)) return true;
            return false;
        }

        public void StopGroupMotion(Rigidbody2D body)
        {
            group.Clear();
            group.Add(body);
            ExpandGroup();
            foreach (var member in group)
            {
                if (!member || member.bodyType != RigidbodyType2D.Dynamic) continue;
                member.linearVelocity = Vector2.zero;
                member.angularVelocity = 0f;
            }
        }

        public void ApplyTapeResistance(float stiffness, float damping, float step)
        {
            if (stiffness <= 0f && damping <= 0f) return;
            float effectiveDamping = damping + stiffness * step;
            foreach (var link in links)
            {
                if (!link.IsValid) continue;
                var a = link.BodyA;
                var b = link.Joint.connectedBody;
                if (!a.IsAwake() && (!b || !b.IsAwake())) continue;

                float inverseA = InverseRotationalInertia(a);
                float inverseB = InverseRotationalInertia(b);
                if (inverseA + inverseB <= 0f) continue;

                float angle = Mathf.DeltaAngle(link.RestAngle, a.rotation - (b ? b.rotation : 0f)) * Mathf.Deg2Rad;
                float speed = (a.angularVelocity - (b ? b.angularVelocity : 0f)) * Mathf.Deg2Rad;
                // Use an implicit spring step so lighter linked pieces stay stable at the same stiffness.
                float torque = -(stiffness * angle + effectiveDamping * speed)
                    / (1f + step * effectiveDamping * (inverseA + inverseB));
                if (inverseA > 0f) a.AddTorque(torque);
                if (inverseB > 0f) b.AddTorque(-torque);
            }
        }

        private static float InverseRotationalInertia(Rigidbody2D body) =>
            body && body.bodyType == RigidbodyType2D.Dynamic && body.inertia > 0f &&
            (body.constraints & RigidbodyConstraints2D.FreezeRotation) == 0 ? 1f / body.inertia : 0f;

        public void UpdateCandidate(Rigidbody2D held, Vector2 cursor, float deltaTime)
        {
            group.Clear();
            group.Add(held);
            ExpandGroup();

            Collider2D bestSource = null, bestTarget = null;
            Vector2 bestPoint = default;
            float bestScore = float.PositiveInfinity;
            foreach (var body in group)
            {
                body.GetAttachedColliders(sources);
                foreach (var source in sources)
                {
                    if (!IsSolid(source)) continue;
                    var bounds = source.bounds;
                    Vector2 size = (Vector2)bounds.size + Vector2.one * (2f * GlueDistance);
                    physicsScene.OverlapBox(bounds.center, size, 0f, solidFilter, nearby);
                    foreach (var target in nearby)
                    {
                        if (!CanConnectTo(target)) continue;
                        var distance = source.Distance(target);
                        if (!distance.isValid || distance.distance > GlueDistance || distance.distance < -MaxOverlap) continue;
                        Vector2 point = (distance.pointA + distance.pointB) * 0.5f;
                        float score = Mathf.Max(0f, distance.distance) + Vector2.Distance(point, cursor) * 0.015f;
                        if (score >= bestScore) continue;
                        bestScore = score;
                        bestSource = source;
                        bestTarget = target;
                        bestPoint = point;
                    }
                }
            }

            if (!bestSource)
            {
                ClearCandidate();
                return;
            }

            if (bestSource != candidateSource || bestTarget != candidateTarget) steadyTime = 0f;
            candidateSource = bestSource;
            candidateTarget = bestTarget;
            candidatePoint = bestPoint;
            var sourceBody = bestSource.attachedRigidbody;
            var targetBody = bestTarget.attachedRigidbody;
            Vector2 targetVelocity = targetBody ? targetBody.GetPointVelocity(bestPoint) : Vector2.zero;
            float targetSpin = targetBody ? targetBody.angularVelocity : 0f;
            float relativeSpeed = (sourceBody.GetPointVelocity(bestPoint) - targetVelocity).magnitude;
            float relativeSpin = Mathf.Abs(sourceBody.angularVelocity - targetSpin);
            steadyTime = relativeSpeed <= SteadySpeed && relativeSpin <= SteadyAngularSpeed ? steadyTime + deltaTime : 0f;

            if (!preview) preview = CreateMark("Tape Preview", sourceBody, PreviewColor);
            preview.sharedMaterial = FindMaterial(sourceBody, preview);
            preview.color = IsReady ? ReadyColor : PreviewColor;
            preview.transform.position = candidatePoint;
            preview.enabled = true;
        }

        public bool ConnectCandidate(bool requireSteady)
        {
            if (!candidateSource || !candidateTarget || (requireSteady && !IsReady)) return false;

            var source = candidateSource;
            var target = candidateTarget;
            var body = source.attachedRigidbody;
            var connected = target.attachedRigidbody;
            bool fixedPlatform = !connected || connected.bodyType == RigidbodyType2D.Static;
            var joint = body.gameObject.AddComponent<HingeJoint2D>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = connected;
            joint.anchor = WorldToBody(body, candidatePoint);
            joint.connectedAnchor = connected ? WorldToBody(connected, candidatePoint) : candidatePoint;
            joint.useMotor = false;
            joint.useLimits = false;
            joint.enableCollision = fixedPlatform;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;

            var mark = CreateMark("Tape", body, configuredSprite ? Color.white : TapeColor);
            mark.transform.position = candidatePoint;
            links.Add(new Link
            {
                Source = source, Target = target, BodyA = body, BlockB = fixedPlatform ? null : connected,
                IsPlatform = fixedPlatform,
                Joint = joint, Mark = mark,
                RestAngle = Mathf.DeltaAngle(connected ? connected.rotation : 0f, body.rotation)
            });
            body.WakeUp();
            if (!fixedPlatform) connected.WakeUp();
            ClearCandidate();
            return true;
        }

        public void RemoveNearest(Vector2 cursor)
        {
            int nearest = FindNearestLink(cursor);
            if (nearest < 0) return;
            RemoveLink(nearest);
        }

        public bool HasTapeAt(Vector2 cursor) => FindNearestLink(cursor) >= 0;

        private int FindNearestLink(Vector2 cursor)
        {
            int nearest = -1;
            float best = RemoveRadius * RemoveRadius;
            for (int i = 0; i < links.Count; i++)
            {
                if (!links[i].IsValid) continue;
                float distance = (links[i].Point - cursor).sqrMagnitude;
                if (distance >= best) continue;
                nearest = i;
                best = distance;
            }
            return nearest;
        }

        public void RemoveLinks(Rigidbody2D body)
        {
            bool removed = false;
            for (int i = links.Count - 1; i >= 0; i--)
            {
                var link = links[i];
                if (link.BodyA != body && link.BlockB != body) continue;
                RemoveLink(i);
                removed = true;
            }
            if (removed) ClearCandidate();
        }

        public void UpdateMarks()
        {
            for (int i = links.Count - 1; i >= 0; i--)
            {
                var link = links[i];
                if (link.IsValid && link.Mark)
                {
                    link.Mark.transform.position = link.Point;
                    continue;
                }
                RemoveLink(i);
            }
        }

        public void ClearCandidate()
        {
            candidateSource = candidateTarget = null;
            steadyTime = 0f;
            if (preview) preview.enabled = false;
        }

        public void Dispose()
        {
            for (int i = links.Count - 1; i >= 0; i--) RemoveLink(i);
            if (preview) Destroy(preview.gameObject);
            if (defaultSquare) Destroy(defaultSquare);
        }

        private bool CanConnectTo(Collider2D target)
        {
            if (!IsSolid(target)) return false;
            var body = target.attachedRigidbody;
            if (!body || body.bodyType == RigidbodyType2D.Static)
                return target.GetComponent<Tilemap>();
            if (body.bodyType != RigidbodyType2D.Dynamic || !body.simulated || group.Contains(body)) return false;
            return (body.TryGetComponent<ManipulableBlock>(out var block) && block.isActiveAndEnabled) ||
                (body.TryGetComponent<BalancePlatform>(out var platform) && platform.isActiveAndEnabled);
        }

        private static bool IsSolid(Collider2D collider) =>
            collider && collider.isActiveAndEnabled && !collider.isTrigger;

        private static Vector2 WorldToBody(Rigidbody2D body, Vector2 point)
        {
            // Interpolation can leave Transform at the rendered pose while joints use the current physics pose.
            Vector2 local = Quaternion.Euler(0f, 0f, -body.rotation) * (Vector3)(point - body.position);
            Vector3 scale = body.transform.lossyScale;
            return new Vector2(local.x / scale.x, local.y / scale.y);
        }

        private static Vector2 BodyToWorld(Rigidbody2D body, Vector2 point)
        {
            Vector3 scale = body.transform.lossyScale;
            Vector2 scaled = new Vector2(point.x * scale.x, point.y * scale.y);
            return body.position + (Vector2)(Quaternion.Euler(0f, 0f, body.rotation) * (Vector3)scaled);
        }

        private void ExpandGroup()
        {
            foreach (var body in group) pending.Enqueue(body);
            while (pending.Count > 0)
            {
                var body = pending.Dequeue();
                foreach (var link in links)
                {
                    // A Tilemap can contain both shores, so it ends a chain instead of joining islands.
                    if (link.IsPlatform || !link.IsValid) continue;
                    var next = link.BodyA == body ? link.BlockB : link.BlockB == body ? link.BodyA : null;
                    if (next && group.Add(next)) pending.Enqueue(next);
                }
            }
        }

        private void RemoveLink(int index)
        {
            var link = links[index];
            links.RemoveAt(index);
            if (link.Joint)
            {
                link.Joint.enabled = false;
                Destroy(link.Joint);
            }
            if (link.Mark)
            {
                link.Mark.enabled = false;
                Destroy(link.Mark.gameObject);
            }
            if (link.BodyA) link.BodyA.WakeUp();
            if (link.BlockB) link.BlockB.WakeUp();
        }

        private SpriteRenderer CreateMark(string name, Rigidbody2D body, Color color)
        {
            if (!configuredSprite && !defaultSquare)
            {
                var texture = Texture2D.whiteTexture;
                defaultSquare = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width);
            }

            var mark = new GameObject(name) { layer = owner.gameObject.layer }.AddComponent<SpriteRenderer>();
            mark.transform.SetParent(owner, false);
            mark.sprite = configuredSprite ? configuredSprite : defaultSquare;
            var size = mark.sprite.bounds.size;
            float scale = (configuredSprite ? tapeWidth : TapeSize) / Mathf.Max(size.x, 0.0001f);
            mark.transform.localScale = new Vector3(scale, scale, 1f);
            mark.sharedMaterial = FindMaterial(body, mark);
            mark.color = color;
            mark.sortingOrder = 40;
            var visual = body.GetComponentInChildren<SpriteRenderer>();
            if (visual) mark.sortingLayerID = visual.sortingLayerID;
            return mark;
        }

        private static Material FindMaterial(Rigidbody2D body, SpriteRenderer mark)
        {
            var visual = body.GetComponentInChildren<SpriteRenderer>();
            return visual && visual.sharedMaterial ? visual.sharedMaterial : mark.sharedMaterial;
        }

        private static void Destroy(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
