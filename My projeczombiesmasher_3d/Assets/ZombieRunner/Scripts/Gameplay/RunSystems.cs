using UnityEngine;

namespace ZombieRunner
{
    /// <summary>Speed ramps up over time; Level01 drives spawn density and zombie mix.</summary>
    public class DifficultyManager
    {
        public float startSpeed = 11f;
        public float maxSpeed = 25f;
        public float rampSeconds = 110f;
        public float Elapsed { get; private set; }
        public float Speed { get; private set; }
        public float Level01 => Mathf.InverseLerp(startSpeed, maxSpeed, Speed);

        public DifficultyManager() { Speed = startSpeed; }

        public void Tick(float dt)
        {
            Elapsed += dt;
            Speed = startSpeed + (maxSpeed - startSpeed) * (1f - Mathf.Exp(-Elapsed / rampSeconds));
        }
    }

    /// <summary>Distance, coins and score. Every 500 m is a "safe zone" checkpoint that raises the multiplier.</summary>
    public class ScoreManager
    {
        public const float CheckpointDistance = 500f;
        public float Distance { get; private set; }
        public int Coins { get; private set; }
        public float Score { get; private set; }
        public int Checkpoint => Mathf.FloorToInt(Distance / CheckpointDistance);
        public float Multiplier => 1f + Checkpoint * 0.5f;
        public float CheckpointProgress => (Distance % CheckpointDistance) / CheckpointDistance;
        public event System.Action<int> OnCheckpoint;

        public void AddDistance(float d)
        {
            int before = Checkpoint;
            Distance += d;
            Score += d * Multiplier;
            if (Checkpoint > before) OnCheckpoint?.Invoke(Checkpoint);
        }

        public void AddCoins(int n)
        {
            Coins += n;
            Score += n * 10f * Multiplier;
        }

        public void AddBonus(float points) => Score += points * Multiplier;
    }

    /// <summary>Timed power-ups and the one-time Health Protection.</summary>
    public class PowerUpManager
    {
        public static readonly float[] Durations = { 10f, 5f, 8f, 12f, 0f };
        readonly float[] remaining = new float[5];
        bool health;

        public event System.Action<PowerUpType> OnActivated;
        public event System.Action<PowerUpType> OnEnded;

        public bool IsActive(PowerUpType t) => t == PowerUpType.HealthProtection ? health : remaining[(int)t] > 0f;
        public float Remaining01(PowerUpType t) => t == PowerUpType.HealthProtection ? (health ? 1f : 0f) : Mathf.Clamp01(remaining[(int)t] / Durations[(int)t]);
        public bool Invulnerable => IsActive(PowerUpType.Shield) || IsActive(PowerUpType.SpeedBoost);
        public int CoinValue => IsActive(PowerUpType.DoubleCoins) ? 2 : 1;
        public float MagnetRadius => IsActive(PowerUpType.Magnet) ? 9f : 0f;

        public void Activate(PowerUpType t)
        {
            if (t == PowerUpType.HealthProtection) health = true;
            else remaining[(int)t] = Durations[(int)t];
            OnActivated?.Invoke(t);
        }

        public bool ConsumeHealth()
        {
            if (!health) return false;
            health = false;
            OnEnded?.Invoke(PowerUpType.HealthProtection);
            return true;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                if (remaining[i] <= 0f) continue;
                remaining[i] -= dt;
                if (remaining[i] <= 0f) { remaining[i] = 0f; OnEnded?.Invoke((PowerUpType)i); }
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < 4; i++) remaining[i] = 0f;
            health = false;
        }
    }

    /// <summary>Third-person follow camera with smoothing, speed FOV, shake and a cinematic death view.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0, 3.3f, -6.4f);
        public Vector3 lookOffset = new Vector3(0, 1.3f, 5.5f);
        public float baseFov = 60f;
        public float boostFov = 74f;

        Transform target;
        Camera cam;
        float shakeAmp, shakeTime, shakeDur;
        bool deathCam;
        float deathT;
        float fov;
        public bool Boosting;

        public Camera Cam => cam;

        public void Init(Transform followTarget)
        {
            target = followTarget;
            cam = GetComponent<Camera>();
            fov = baseFov;
            if (target != null) transform.position = target.position + offset;
        }

        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmp * (shakeTime / Mathf.Max(0.01f, shakeDur))) return;
            shakeAmp = amplitude; shakeTime = duration; shakeDur = duration;
        }

        public void DeathView() { deathCam = true; deathT = 0f; }
        public void Shift(float dz) => transform.position += new Vector3(0, 0, dz);

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            Vector3 desired, look;
            if (deathCam)
            {
                deathT += dt;
                float a = Mathf.Clamp01(deathT / 2f);
                var orbit = Quaternion.Euler(0, Mathf.Lerp(0, 140f, Mathf.SmoothStep(0, 1, a)), 0) * new Vector3(0, 2.4f, -4.5f);
                desired = target.position + orbit;
                look = target.position + Vector3.up * 0.8f;
                transform.position = ZRUtil.Damp(transform.position, desired, 3f, dt);
            }
            else
            {
                desired = new Vector3(target.position.x * 0.85f, target.position.y * 0.5f, target.position.z) + offset;
                look = new Vector3(target.position.x * 0.6f, target.position.y * 0.4f, target.position.z) + lookOffset;
                var p = transform.position;
                p.x = ZRUtil.Damp(p.x, desired.x, 7f, dt);
                p.y = ZRUtil.Damp(p.y, desired.y, 5f, dt);
                p.z = desired.z;
                transform.position = p;
            }
            transform.rotation = Quaternion.LookRotation(look - transform.position);

            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                float k = shakeAmp * Mathf.Clamp01(shakeTime / shakeDur);
                transform.position += new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 25f, 0) - 0.5f, Mathf.PerlinNoise(0, Time.unscaledTime * 25f) - 0.5f, 0) * k;
            }
            if (cam != null)
            {
                fov = ZRUtil.Damp(fov, Boosting ? boostFov : baseFov, 4f, dt);
                cam.fieldOfView = fov;
            }
        }
    }
}
