using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    public enum ZombieKind { Walker, Runner, Brute, Emerger, Pursuer }

    /// <summary>
    /// A zombie on the track. Walkers shamble toward the player, Runners sprint at them, Brutes are big
    /// and slow, Emergers rise from the ground or step out of buildings/trees into a lane.
    /// The zombie carries an Obstacle (type Zombie) so player collision is handled the same way as props.
    /// </summary>
    public class ZombieAgent : MonoBehaviour, IPoolable
    {
        public ZombieKind kind;
        public CharacterVisual visual;
        public Obstacle obstacle;
        public float moveSpeed;
        public float minZ = float.MinValue;   // never advance past this (keeps the track solvable)
        public int lane;

        Transform player;
        bool emerging, fromSide, risen;
        float sideStartX, targetX, emergeT, growlTimer;
        bool active;

        public void Init(Transform playerT) { player = playerT; }

        public void Activate(ZombieKind k, int laneIndex, float speed, float stopZ, bool emergeFromSide)
        {
            kind = k; lane = laneIndex; moveSpeed = speed; minZ = stopZ;
            targetX = ZRUtil.LaneX(laneIndex);
            active = true;
            emerging = k == ZombieKind.Emerger;
            fromSide = emergeFromSide;
            risen = !emerging;
            emergeT = 0f;
            growlTimer = Random.Range(0.5f, 3f);
            transform.rotation = Quaternion.Euler(0, 180, 0); // face the player (-Z)
            if (emerging)
            {
                if (fromSide)
                {
                    sideStartX = Mathf.Sign(targetX == 0 ? (Random.value - 0.5f) : targetX) * Random.Range(6.5f, 8f);
                    transform.position = new Vector3(sideStartX, 0, transform.position.z);
                    transform.rotation = Quaternion.Euler(0, sideStartX > 0 ? -90 : 90, 0);
                    visual.SetPose(Pose.ZombieWalk, 1.2f);
                }
                else
                {
                    transform.position = new Vector3(targetX, 0, transform.position.z);
                    visual.gameObject.SetActive(false); // waits underground until the player is near
                }
                if (obstacle.box) obstacle.box.enabled = false;
            }
            else
            {
                transform.position = new Vector3(targetX, 0, transform.position.z);
                visual.SetPose(k == ZombieKind.Runner ? Pose.ZombieRun : Pose.ZombieWalk, k == ZombieKind.Brute ? 0.7f : Random.Range(0.85f, 1.15f));
            }
        }

        public void OnSpawned() { visual.gameObject.SetActive(true); } // Obstacle.OnSpawned is called by the pool too
        public void OnDespawned() { active = false; }

        void Update()
        {
            if (!active || player == null || obstacle.smashed) return;
            float dt = Time.deltaTime;
            float dist = transform.position.z - player.position.z;

            if (emerging)
            {
                if (dist > 42f) return;
                emergeT += dt;
                if (fromSide)
                {
                    float t = Mathf.Clamp01(emergeT / 1.3f);
                    var p = transform.position; p.x = Mathf.Lerp(sideStartX, targetX, Mathf.SmoothStep(0, 1, t)); transform.position = p;
                    if (t > 0.6f && obstacle.box) obstacle.box.enabled = true;
                    if (t >= 1f) { emerging = false; transform.rotation = Quaternion.Euler(0, 180, 0); visual.SetPose(Pose.ZombieWalk); }
                }
                else
                {
                    if (!visual.gameObject.activeSelf)
                    {
                        visual.gameObject.SetActive(true);
                        visual.SetPose(Pose.Rise);
                        AudioManager.Play3D(AudioKeys.ZombieGrowl, transform.position, 1f, Random.Range(0.85f, 1.1f));
                        FX_Dirt();
                    }
                    if (emergeT > 0.45f && obstacle.box) obstacle.box.enabled = true;
                    if (emergeT > 1.4f) { emerging = false; visual.SetPose(Pose.ZombieWalk); }
                }
                return;
            }

            // Move toward the player along the lane.
            if (dist > 1.5f && transform.position.z > minZ)
            {
                var p = transform.position;
                p.z = Mathf.Max(minZ, p.z - moveSpeed * dt);
                transform.position = p;
            }
            else if (dist < 2.5f && dist > -1f && visual.CurrentPose != Pose.Attack && kind != ZombieKind.Pursuer)
            {
                visual.SetPose(Pose.Attack);
            }

            growlTimer -= dt;
            if (growlTimer <= 0f && dist < 40f && dist > -5f)
            {
                growlTimer = Random.Range(2.5f, 5f);
                AudioManager.Play3D(kind == ZombieKind.Brute ? AudioKeys.BruteRoar : AudioKeys.ZombieGrowl, transform.position + Vector3.up * 1.6f, 0.9f, kind == ZombieKind.Brute ? 0.7f : Random.Range(0.85f, 1.2f));
            }
        }

        void FX_Dirt()
        {
            if (dirt == null)
            {
                dirt = FX.Burst(transform, new Color(0.3f, 0.25f, 0.2f, 0.9f), false, 4f, 0.35f, 1.2f);
            }
            dirt.transform.position = transform.position + Vector3.up * 0.1f;
            dirt.Emit(30);
        }
        ParticleSystem dirt;

        /// <summary>Builds a pooled zombie instance for a theme.</summary>
        public static GameObject Build(ThemeProfile theme, ZombieKind kind, int seed)
        {
            var root = new GameObject("Zombie_" + kind);
            var style = CharacterCatalog.Zombie(theme, kind, seed);
            var vis = CharacterVisual.CreateZombie(root.transform, style, seed);
            var o = Obstacle.Setup(root, ObstacleType.Zombie, kind == ZombieKind.Brute ? "a brute zombie" : "a zombie", vis.transform);
            if (kind == ZombieKind.Brute) { o.box.size = new Vector3(1.4f, 2.3f, 0.9f); o.box.center = new Vector3(0, 1.15f, 0); }
            var z = root.AddComponent<ZombieAgent>();
            z.kind = kind; z.visual = vis; z.obstacle = o;
            return root;
        }
    }

    /// <summary>
    /// The horde behind the player. It drops back once the run starts, closes in when the player stumbles,
    /// and catches them on a second stumble while it is still close. Drives the chase audio and danger overlay.
    /// </summary>
    public class ChaseSystem : MonoBehaviour
    {
        public float farDistance = 13f;
        public float closeDistance = 2.6f;
        public float closeDuration = 6f;

        readonly List<CharacterVisual> pack = new List<CharacterVisual>();
        readonly List<Vector3> offsets = new List<Vector3>();
        Transform player;
        PlayerController pc;
        float distance = 3.2f, targetDistance = 3.2f, closeTimer;
        bool attacking;

        /// <summary>0 = safe, 1 = right behind you.</summary>
        public float Danger { get; private set; }
        public bool IsClose => closeTimer > 0f;

        public void Build(ThemeProfile theme, PlayerController player, int count)
        {
            pc = player;
            this.player = player.transform;
            for (int i = 0; i < count; i++)
            {
                var kind = i == 0 && theme.id != ThemeId.Forest ? ZombieKind.Brute : ZombieKind.Runner;
                var holder = new GameObject("Pursuer_" + i).transform;
                holder.SetParent(transform, false);
                var v = CharacterVisual.CreateZombie(holder, CharacterCatalog.Zombie(theme, kind, 900 + i * 7), 900 + i * 7);
                if (kind == ZombieKind.Brute) v.transform.localScale = Vector3.one * 1.1f;
                v.SetPose(Pose.ZombieRun, Random.Range(0.95f, 1.1f));
                pack.Add(v);
                offsets.Add(new Vector3((i % 3 - 1) * 1.3f + Random.Range(-0.3f, 0.3f), 0, -(i / 3) * 1.6f - Random.Range(0f, 0.8f)));
            }
            ShiftAll(0);
        }

        public void StartRun() { targetDistance = farDistance; }

        /// <summary>Called on a stumble. Returns true if the horde catches the player.</summary>
        public bool OnStumble()
        {
            if (IsClose) return true;
            closeTimer = closeDuration;
            targetDistance = closeDistance;
            AudioManager.PlaySfx(AudioKeys.ZombieAttack, 0.9f);
            return false;
        }

        public void Attack()
        {
            attacking = true;
            targetDistance = 0.9f;
            foreach (var v in pack) v.SetPose(Pose.Attack);
        }

        public void ShiftAll(float dz)
        {
            transform.position += new Vector3(0, 0, dz);
        }

        void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            if (closeTimer > 0f && !attacking)
            {
                closeTimer -= dt;
                if (closeTimer <= 0f) targetDistance = farDistance;
            }
            distance = ZRUtil.Damp(distance, targetDistance, attacking ? 4f : 1.2f, dt);
            Danger = Mathf.Clamp01(1f - (distance - closeDistance) / (farDistance - closeDistance));
            AudioManager.SetChaseIntensity(pc != null && pc.Alive ? Mathf.Max(0.25f, Danger) : 0.4f);

            for (int i = 0; i < pack.Count; i++)
            {
                var t = pack[i].transform.parent;
                var target = new Vector3(player.position.x * 0.8f + offsets[i].x, 0, player.position.z - distance + offsets[i].z);
                if (attacking) target = new Vector3(player.position.x + offsets[i].x * 0.6f, 0, player.position.z - 0.6f + offsets[i].z * 0.4f + (i == 0 ? 0.3f : 0f));
                var p = t.position;
                p.x = ZRUtil.Damp(p.x, target.x, 6f, dt);
                p.z = target.z;
                t.position = p;
                t.rotation = Quaternion.identity;
                // hide pursuers that are far behind the camera
                bool visible = distance < 9f || attacking;
                if (pack[i].gameObject.activeSelf != visible) pack[i].gameObject.SetActive(visible);
            }
        }
    }
}
