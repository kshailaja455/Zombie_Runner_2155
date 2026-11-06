using UnityEngine;

namespace ZombieRunner
{
    /// <summary>
    /// Three-lane runner movement: lane switching, jump (with fast-fall), slide, collisions, stumbles and death.
    /// Collision uses an overlap box swept over the frame's movement, so it is frame-rate independent and
    /// needs no Rigidbody or physics layers.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float laneChangeSpeed = 15f;
        public float jumpHeight = 1.6f;
        public float gravity = 38f;
        public float slideDuration = 0.8f;

        [Header("Runtime")]
        public int lane;
        public CharacterVisual visual;

        public bool Alive { get; private set; } = true;
        public bool Running { get; private set; }
        public bool Grounded => grounded;
        public bool Sliding => slideTimer > 0f;
        public float Speed { get; private set; }

        GameManager gm;
        float vy, y;
        bool grounded = true;
        float slideTimer;
        bool slideQueued;
        int prevLane;
        bool changingLane;
        float stumbleTimer, invulnTimer;
        float jumpBuffer, slideBuffer;
        float footstepTimer;
        readonly Collider[] hits = new Collider[16];
        ParticleSystem slideDust;

        public void Init(GameManager manager, int characterIndex)
        {
            gm = manager;
            visual = CharacterVisual.CreateSurvivor(transform, characterIndex);
            visual.SetPose(Pose.Idle);
            slideDust = FX.Burst(transform, new Color(0.5f, 0.45f, 0.38f, 0.6f), false, 2f, 0.4f, 0.6f);
        }

        public void StartRunning()
        {
            Running = true;
            visual.SetPose(Pose.Run);
        }

        public void Command(InputCommand c)
        {
            if (!Alive || !Running) return;
            switch (c)
            {
                case InputCommand.Left: ChangeLane(-1); break;
                case InputCommand.Right: ChangeLane(1); break;
                case InputCommand.Jump: jumpBuffer = 0.15f; break;
                case InputCommand.Slide: slideBuffer = 0.15f; break;
            }
        }

        void ChangeLane(int dir)
        {
            int target = Mathf.Clamp(lane + dir, -1, 1);
            if (target == lane) { gm.Camera.Shake(0.08f, 0.15f); return; }
            prevLane = lane;
            lane = target;
            changingLane = true;
            AudioManager.PlaySfx(AudioKeys.Whoosh, 0.35f, Random.Range(1.1f, 1.3f));
        }

        public void Shift(float dz) => transform.position += new Vector3(0, 0, dz);

        void Update()
        {
            if (!Alive || !Running) return;
            float dt = Time.deltaTime;
            float startZ = transform.position.z;

            // Speed: difficulty curve, speed boost, stumble slowdown
            float speed = gm.Difficulty.Speed;
            if (gm.PowerUps.IsActive(PowerUpType.SpeedBoost)) speed *= 1.45f;
            if (stumbleTimer > 0f) { stumbleTimer -= dt; speed *= 0.65f; }
            Speed = speed;
            if (invulnTimer > 0f) invulnTimer -= dt;

            // Buffered actions
            if (jumpBuffer > 0f) { jumpBuffer -= dt; if (grounded) { DoJump(); jumpBuffer = 0f; } }
            if (slideBuffer > 0f) { slideBuffer -= dt; DoSlide(); slideBuffer = 0f; }

            // Vertical
            if (!grounded)
            {
                vy -= gravity * dt;
                y += vy * dt;
                if (y <= 0f)
                {
                    y = 0f; vy = 0f; grounded = true;
                    AudioManager.PlaySfx(AudioKeys.Land, 0.5f);
                    if (slideQueued) { slideQueued = false; StartSlide(); }
                    else if (!Sliding) visual.SetPose(Pose.Run);
                }
            }
            if (slideTimer > 0f)
            {
                slideTimer -= dt;
                if (slideTimer <= 0f && grounded) visual.SetPose(Pose.Run);
            }

            // Lateral
            float tx = ZRUtil.LaneX(lane);
            var pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, tx, laneChangeSpeed * dt);
            if (Mathf.Abs(pos.x - tx) < 0.01f) changingLane = false;
            visual.lean = Mathf.Clamp((tx - pos.x) / ZRUtil.LaneWidth, -1f, 1f);
            pos.y = y;
            pos.z += speed * dt;
            transform.position = pos;
            if (visual.CurrentPose == Pose.Run) visual.SetPose(Pose.Run, Mathf.Lerp(0.9f, 1.35f, gm.Difficulty.Level01));

            // Footsteps
            if (grounded && !Sliding)
            {
                footstepTimer -= dt * speed / 11f;
                if (footstepTimer <= 0f) { footstepTimer = 0.33f; AudioManager.Footstep(gm.Theme.road); }
            }

            CheckCollisions(startZ, pos.z);
        }

        void DoJump()
        {
            if (Sliding) { slideTimer = 0f; }
            grounded = false;
            vy = Mathf.Sqrt(2f * gravity * jumpHeight);
            visual.SetPose(Pose.Jump);
            AudioManager.PlaySfx(AudioKeys.Jump, 0.8f, Random.Range(0.95f, 1.05f));
        }

        void DoSlide()
        {
            if (!grounded)
            {
                vy = -gravity * 0.9f; // fast-fall, then slide on landing
                slideQueued = true;
                return;
            }
            StartSlide();
        }

        void StartSlide()
        {
            slideTimer = slideDuration;
            visual.SetPose(Pose.Slide);
            AudioManager.PlaySfx(AudioKeys.Slide, 0.8f);
            if (slideDust) { slideDust.transform.position = transform.position; slideDust.Emit(18); }
        }

        void CheckCollisions(float z0, float z1)
        {
            float height = Sliding ? 0.75f : 1.7f;
            float dz = Mathf.Max(0f, z1 - z0);
            var center = new Vector3(transform.position.x, y + height * 0.5f + 0.05f, (z0 + z1) * 0.5f);
            var half = new Vector3(0.28f, height * 0.5f, 0.25f + dz * 0.5f);
            Physics.SyncTransforms(); // pooled obstacles and zombies move by transform; make sure the query sees where they are now
            int n = Physics.OverlapBoxNonAlloc(center, half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var o = hits[i].GetComponentInParent<Obstacle>();
                if (o != null && !o.hit && !o.smashed) HandleHit(o);
                if (!Alive) return;
            }
        }

        void HandleHit(Obstacle o)
        {
            if (gm.PowerUps.Invulnerable)
            {
                o.Smash(transform.position);
                gm.OnSmashed(o);
                return;
            }
            o.hit = true;
            bool sideHit = changingLane && Mathf.Abs(transform.position.x - o.transform.position.x) > 0.75f;
            if (sideHit)
            {
                // bounced off the side: go back to the lane we came from
                lane = prevLane;
                changingLane = true;
                AudioManager.PlaySfx(AudioKeys.Bump, 0.9f);
                gm.Camera.Shake(0.25f, 0.25f);
                Stumble();
                return;
            }
            if (o.type == ObstacleType.Block || o.type == ObstacleType.Zombie)
            {
                if (gm.PowerUps.ConsumeHealth())
                {
                    o.Smash(transform.position);
                    AudioManager.PlaySfx(AudioKeys.Smash, 1f);
                    gm.Camera.Shake(0.5f, 0.35f);
                    Stumble(true);
                    return;
                }
                AudioManager.PlaySfx(AudioKeys.Crash, 1f);
                gm.Camera.Shake(0.8f, 0.5f);
                gm.PlayerDied(o.type == ObstacleType.Zombie ? "Grabbed by " + o.displayName : "Crashed into " + o.displayName);
                return;
            }
            // Tripped on something low/high/a pit
            AudioManager.PlaySfx(AudioKeys.Bump, 0.9f);
            gm.Camera.Shake(0.3f, 0.3f);
            Stumble();
        }

        void Stumble(bool forgiven = false)
        {
            if (invulnTimer > 0f) return;
            invulnTimer = 1.0f;
            stumbleTimer = 0.7f;
            slideTimer = 0f;
            visual.SetPose(Pose.Stumble);
            AudioManager.PlaySfx(AudioKeys.Grunt, 0.8f, Random.Range(0.9f, 1.1f));
            if (!forgiven && gm.Chase.OnStumble())
            {
                gm.PlayerDied("The horde caught you");
            }
        }

        public void Die()
        {
            if (!Alive) return;
            Alive = false;
            Running = false;
            y = 0f; vy = 0f;
            var p = transform.position; p.y = 0f; transform.position = p;
            visual.SetPose(Pose.Dead);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            float height = Sliding ? 0.75f : 1.7f;
            Gizmos.DrawWireCube(transform.position + Vector3.up * (height * 0.5f + 0.05f), new Vector3(0.56f, height, 0.5f));
        }
    }
}
