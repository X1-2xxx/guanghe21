using System.Collections.Generic;
using UnityEngine;

namespace LevelKit
{
    internal sealed class PlayerTraversal
    {
        private const float Skin = 0.015f;
        private const float GroundProbe = 0.075f;
        private const float JumpWindup = 0.14f;
        private const float ImpactPause = 0.15f;
        private const float HeightTolerance = 0.001f;
        private const float SurfaceNormalTolerance = 0.00005f;
        private readonly PlayerActionController player;
        private readonly Rigidbody2D body;
        private readonly CapsuleCollider2D capsule;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private readonly ContactFilter2D solids;
        private readonly float walkableNormal;

        private MotionPlan plan;
        private IBreakable lastTarget;
        private bool hitAccepted;
        private float impactRemaining;
        private bool risingFromLaunch;
        private bool bedBounceInProgress;
        private bool launchedJumpInProgress;
        public int ConsecutiveBedBounces { get; private set; }
        public void ResetBedBounceChain() => ConsecutiveBedBounces = 0;

        public bool IsJumping => (plan != null && plan.IsJump) || launchedJumpInProgress;

        private sealed class MotionPlan
        {
            public Vector2 Start, Landing;
            public float ClearY, RiseTime, CrossTime, SettleTime, Windup, Arc;
            public float Elapsed;
            public bool IsJump;
            public float Duration => Windup + RiseTime + CrossTime + SettleTime;

            public Vector2 PositionAt(float time)
            {
                float elapsed = time - Windup;
                if (elapsed <= 0f) return Start;
                if (elapsed < RiseTime)
                    return new Vector2(Start.x, Mathf.SmoothStep(Start.y, ClearY, elapsed / RiseTime));
                elapsed -= RiseTime;
                if (elapsed < CrossTime)
                {
                    float progress = elapsed / CrossTime;
                    return new Vector2(Mathf.SmoothStep(Start.x, Landing.x, progress),
                        ClearY + Mathf.Sin(progress * Mathf.PI) * Arc);
                }
                return new Vector2(Landing.x, Mathf.SmoothStep(ClearY, Landing.y, (elapsed - CrossTime) / SettleTime));
            }
        }

        public PlayerTraversal(PlayerActionController player, Rigidbody2D body, CapsuleCollider2D capsule, float maxSlopeAngle)
        {
            this.player = player;
            this.body = body;
            this.capsule = capsule;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer));
            solids = filter;
            walkableNormal = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
        }

        public void Tick(int direction, float speed, float stepHeight, float jumpHeight, float deltaTime)
        {
            if (AdvancePlan(deltaTime)) return;
            if (impactRemaining > 0f)
            {
                impactRemaining -= deltaTime;
                body.linearVelocityX = 0f;
                return;
            }

            var forward = new Vector2(direction, 0f);
            var origin = body.position;
            float stride = speed * deltaTime;
            RaycastHit2D support = default;
            bool grounded = !risingFromLaunch && FindSupport(origin, GroundProbe, out support);
            bool facingObstacle = FindBlockingHit(origin, forward, stride + Skin, out var obstacle);

            if (facingObstacle && TryImpact(obstacle, forward))
            {
                body.linearVelocityX = 0f;
                return;
            }

            if (!facingObstacle || (grounded && IsWalkable(obstacle.normal)))
            {
                ClearImpact();
                DriveOnSurface(direction * speed, grounded, facingObstacle ? obstacle.normal : support.normal);
                return;
            }

            if (grounded)
            {
                float groundY = origin.y - support.distance;
                float advance = obstacle.distance + capsule.bounds.extents.x + 0.12f;
                if (TryPlan(origin, groundY, forward, advance, stepHeight, false, speed) ||
                    TryPlan(origin, groundY, forward, advance, jumpHeight, true, speed))
                {
                    body.linearVelocityX = 0f;
                    AdvancePlan(deltaTime);
                    return;
                }
            }

            if (grounded || (!risingFromLaunch && FindFootContact(origin)))
            {
                player.HitWall(direction, ObstacleReason(obstacle));
                return;
            }

            body.linearVelocityX = direction * Mathf.Min(speed, Mathf.Max(0f, obstacle.distance - Skin) / deltaTime);
        }

        public void Cancel()
        {
            if (plan != null) body.linearVelocityY = 0f;
            plan = null;
            risingFromLaunch = false;
            bedBounceInProgress = false;
            launchedJumpInProgress = false;
            ClearImpact();
        }

        public void BounceFromBed(Vector2 velocity)
        {
            Cancel();
            if (ConsecutiveBedBounces < int.MaxValue) ConsecutiveBedBounces++;
            bedBounceInProgress = true;
            risingFromLaunch = velocity.y > 0f;
            launchedJumpInProgress = velocity.y > 0f;
            body.linearVelocity = velocity;
        }

        public bool UpdateBedBounce()
        {
            if (!bedBounceInProgress) return false;
            if (risingFromLaunch && body.linearVelocityY <= 0f) risingFromLaunch = false;
            if (risingFromLaunch || !FindSupport(body.position, GroundProbe, out var support)) return true;
            var supportVelocity = support.rigidbody ? support.rigidbody.GetPointVelocity(support.point) : Vector2.zero;
            if (Mathf.Abs(Vector2.Dot(body.linearVelocity - supportVelocity, support.normal)) > 0.1f) return true;
            bedBounceInProgress = false;
            return false;
        }

        public void UpdateAirborneState()
        {
            if (risingFromLaunch && body.linearVelocityY <= 0f) risingFromLaunch = false;
            if (ConsecutiveBedBounces > 0 && !risingFromLaunch && FindSupport(body.position, GroundProbe, out var ground))
            {
                var bounce = ground.collider.GetComponentInParent<HotelBounceBlock>();
                Vector2 supportVelocity = ground.rigidbody ? ground.rigidbody.GetPointVelocity(ground.point) : Vector2.zero;
                if ((!bounce || bounce.Kind != HotelBounceBlock.BlockKind.Bed) &&
                    Mathf.Abs(Vector2.Dot(body.linearVelocity - supportVelocity, ground.normal)) <= .3f)
                    ResetBedBounceChain();
            }
            if (launchedJumpInProgress && !risingFromLaunch && FindSupport(body.position, GroundProbe, out _))
                launchedJumpInProgress = false;
        }

        public float LimitRetreat(int direction, float distance)
        {
            if (distance <= 0f) return 0f;
            if (FindBlockingHit(body.position, new Vector2(direction, 0f), distance + Skin, out var hit))
                return Mathf.Min(distance, Mathf.Max(0f, hit.distance - Skin));
            return distance;
        }

        public void LaunchUpwards(float speed)
        {
            Cancel();
            risingFromLaunch = true;
            launchedJumpInProgress = true;
            float gain = Mathf.Max(0f, speed - body.linearVelocityY);
            body.AddForce(Vector2.up * (body.mass * gain), ForceMode2D.Impulse);
        }

        private void DriveOnSurface(float horizontal, bool grounded, Vector2 normal)
        {
            if (grounded && Mathf.Abs(normal.x) > 0.02f && IsWalkable(normal))
            {
                body.linearVelocity = new Vector2(horizontal, -horizontal * normal.x / normal.y);
                return;
            }
            body.linearVelocityX = horizontal;
        }

        private bool TryPlan(Vector2 start, float groundY, Vector2 forward, float advance,
            float riseLimit, bool jump, float speed)
        {
            if (riseLimit <= 0f) return false;

            float lift = riseLimit + (jump ? 0.15f : 0.06f);
            if (FindBlockingHit(start, Vector2.up, lift + Skin, out var ceiling))
                lift = Mathf.Min(lift, ceiling.distance - Skin);
            if (lift <= Skin) return false;

            Vector2 raised = start + Vector2.up * lift;
            Vector2 across = raised + forward * advance;
            if (FindBlockingHit(raised, forward, advance + Skin, out _) ||
                !FindSupport(across, lift + GroundProbe, out var top)) return false;

            float landingGroundY = across.y - top.distance;
            float rise = landingGroundY - groundY;
            if (rise <= 0.025f || (jump ? rise >= riseLimit - HeightTolerance : rise > riseLimit + HeightTolerance))
                return false;

            Vector2 landing = new Vector2(across.x, landingGroundY + Skin);
            float clearY = Mathf.Min(raised.y, landing.y + (jump ? 0.08f : 0.028f));
            if (clearY < landing.y || FindBlockingHit(new Vector2(start.x, clearY), forward, advance, out _))
                return false;

            plan = new MotionPlan
            {
                Start = start,
                IsJump = jump,
                Landing = landing,
                ClearY = clearY,
                Windup = jump ? JumpWindup : 0f,
                RiseTime = jump ? 0.14f : 0.10f + rise * 0.10f,
                CrossTime = Mathf.Max(jump ? 0.16f : 0.09f, advance / speed),
                SettleTime = jump ? 0.12f : 0.055f,
                Arc = jump ? Mathf.Min(0.05f, Mathf.Max(0f, raised.y - clearY)) : 0f
            };
            if (jump) GamePresentation.PresentationEvents.Report(GamePresentation.PlayEvent.Jumped, player);
            return true;
        }

        private bool AdvancePlan(float deltaTime)
        {
            if (plan == null) return false;
            if (plan.Elapsed >= plan.Duration)
            {
                Cancel();
                return false;
            }

            plan.Elapsed = Mathf.Min(plan.Duration, plan.Elapsed + deltaTime);
            if (plan.Elapsed <= plan.Windup)
            {
                body.linearVelocityX = 0f;
                return true;
            }

            Vector2 destination = plan.PositionAt(plan.Elapsed);
            Vector2 delta = destination - body.position;
            float distance = delta.magnitude;
            if (distance > HeightTolerance &&
                FindBlockingHit(body.position, delta / distance, distance, out var blocker) &&
                blocker.distance < distance - HeightTolerance)
            {
                bool wall = !IsWalkable(blocker.normal);
                Cancel();
                body.linearVelocityX = 0f;
                if (wall)
                    player.HitWall(player.WalkDirection, ObstacleReason(blocker));
                return true;
            }

            body.MovePosition(destination);
            return true;
        }

        private bool TryImpact(RaycastHit2D obstacle, Vector2 direction)
        {
            var target = player.BreakEnabled ? obstacle.collider.GetComponentInParent<IBreakable>() : null;
            if (target == null)
            {
                ClearImpact();
                return false;
            }
            if (ReferenceEquals(target, lastTarget)) return hitAccepted;

            lastTarget = target;
            hitAccepted = target.TryBreak(player, obstacle.point, direction);
            if (hitAccepted) impactRemaining = ImpactPause;
            return hitAccepted;
        }

        private void ClearImpact()
        {
            lastTarget = null;
            hitAccepted = false;
            impactRemaining = 0f;
        }

        private bool FindSupport(Vector2 position, float distance, out RaycastHit2D support)
        {
            capsule.Cast(position, body.rotation, Vector2.down, solids, hits, distance);
            foreach (var hit in hits)
            {
                if (!IsWalkable(hit.normal)) continue;
                support = hit;
                return true;
            }
            support = default;
            return false;
        }

        private bool FindBlockingHit(Vector2 position, Vector2 direction, float distance, out RaycastHit2D blocking)
        {
            capsule.Cast(position, body.rotation, direction, solids, hits, distance);
            foreach (var hit in hits)
            {
                if (Vector2.Dot(hit.normal, direction) >= -0.08f) continue;
                blocking = hit;
                return true;
            }
            blocking = default;
            return false;
        }

        private bool FindFootContact(Vector2 position)
        {
            capsule.Cast(position, body.rotation, Vector2.down, solids, hits, GroundProbe);
            foreach (var hit in hits)
                if (hit.normal.y > 0.05f) return true;
            return false;
        }

        private PlayerActionController.WallStopReason ObstacleReason(RaycastHit2D hit)
        {
            if (hit.normal.y < -0.05f) return PlayerActionController.WallStopReason.ClearanceBlocked;
            if (hit.normal.y > 0.05f && !IsWalkable(hit.normal))
                return PlayerActionController.WallStopReason.SlopeTooSteep;
            return PlayerActionController.WallStopReason.ObstacleTooHigh;
        }

        private bool IsWalkable(Vector2 normal) => normal.y > walkableNormal + SurfaceNormalTolerance;
    }
}
