using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieRunner
{
    public static class AudioKeys
    {
        public const string Jump = "jump", Land = "land", Slide = "slide", Whoosh = "whoosh", Coin = "coin", PowerDown = "powerdown",
            Bump = "bump", Crash = "crash", Smash = "smash", Grunt = "grunt", ZombieGrowl = "zombie_growl", BruteRoar = "brute_roar",
            ZombieAttack = "zombie_attack", ZombieHit = "zombie_hit", GameOver = "gameover", Countdown = "countdown", Go = "go",
            Checkpoint = "checkpoint", UIClick = "ui_click", Chase = "chase_loop",
            MusicMenu = "music_menu", MusicForest = "music_forest", MusicDesert = "music_desert", MusicDarkCity = "music_darkcity", MusicAbandoned = "music_abandoned",
            AmbForest = "amb_forest", AmbDesert = "amb_desert", AmbCity = "amb_city", AmbRuins = "amb_ruins";

        public static string PowerUp(PowerUpType t) => "powerup_" + t.ToString().ToLowerInvariant();
        public static string Footstep(RoadStyle r, int variant) => "footstep_" + r.ToString().ToLowerInvariant() + "_" + variant;

        /// <summary>All keys, listed for the setup tool so imported audio can override any of them by file name.</summary>
        public static IEnumerable<string> All()
        {
            foreach (var k in new[] { Jump, Land, Slide, Whoosh, Coin, PowerDown, Bump, Crash, Smash, Grunt, ZombieGrowl, BruteRoar, ZombieAttack, ZombieHit,
                GameOver, Countdown, Go, Checkpoint, UIClick, Chase, MusicMenu, MusicForest, MusicDesert, MusicDarkCity, MusicAbandoned, AmbForest, AmbDesert, AmbCity, AmbRuins })
                yield return k;
            for (int i = 0; i < 5; i++) yield return PowerUp((PowerUpType)i);
            foreach (RoadStyle r in Enum.GetValues(typeof(RoadStyle))) for (int v = 0; v < 4; v++) yield return Footstep(r, v);
        }
    }

    /// <summary>
    /// Original sound effects, music and ambience synthesised at runtime (no licensing needed).
    /// Every clip can be replaced by a real recording: drop a file named after the key into
    /// Assets/ZombieRunner/Imported/Audio and run the Setup Wizard.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SR = 22050;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static System.Random rnd = new System.Random(1234);

        public static AudioClip Get(string key)
        {
            if (cache.TryGetValue(key, out var c) && c != null) return c;
            c = Generate(key);
            if (c != null) cache[key] = c;
            return c;
        }

        static AudioClip Make(string name, float[] data, float gain = 0.9f)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float k = gain / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
            var clip = AudioClip.Create("ZR_" + name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float N() => (float)(rnd.NextDouble() * 2.0 - 1.0);
        static int Len(float sec) => Mathf.Max(1, Mathf.RoundToInt(sec * SR));
        static float Saw(double ph) => (float)(2.0 * (ph - Math.Floor(ph + 0.5)));
        static float Sin(double ph) => (float)Math.Sin(ph * 2.0 * Math.PI);
        static float Tri(double ph) => 1f - 4f * Mathf.Abs((float)(ph - Math.Floor(ph + 0.5)));
        static float NoteHz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        // ---------- simple filters
        struct OnePole
        {
            float z;
            public float LP(float x, float cutoff) { float a = Mathf.Clamp01(cutoff * 2f * Mathf.PI / SR); z += a * (x - z); return z; }
            public float HP(float x, float cutoff) => x - LP(x, cutoff);
        }

        struct BandPass
        {
            float x1, x2, y1, y2;
            public float Process(float x, float freq, float q)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(freq, 20f, SR * 0.45f) / SR;
                float alpha = Mathf.Sin(w) / (2f * q);
                float b0 = alpha, b2 = -alpha, a0 = 1 + alpha, a1 = -2 * Mathf.Cos(w), a2 = 1 - alpha;
                float y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static AudioClip Generate(string key)
        {
            rnd = new System.Random(key.GetHashCode());
            if (key.StartsWith("footstep_"))
            {
                var parts = key.Split('_');
                var style = (RoadStyle)Enum.Parse(typeof(RoadStyle), parts[1], true);
                return Make(key, Footstep(style), 0.7f);
            }
            if (key.StartsWith("powerup_"))
            {
                var t = (PowerUpType)Enum.Parse(typeof(PowerUpType), key.Substring(8), true);
                return Make(key, PowerUpSfx(t), 0.8f);
            }
            switch (key)
            {
                case AudioKeys.Jump: return Make(key, Sweep(0.32f, 500f, 2200f, 0.6f, 0.15f));
                case AudioKeys.Whoosh: return Make(key, Sweep(0.2f, 1500f, 600f, 0.8f, 0.3f), 0.6f);
                case AudioKeys.Land: return Make(key, Thud(0.18f, 90f, 40f, 0.4f), 0.7f);
                case AudioKeys.Slide: return Make(key, SlideSfx(), 0.7f);
                case AudioKeys.Coin: return Make(key, Bell(new[] { 1568f, 2093f }, 0.07f, 0.35f), 0.6f);
                case AudioKeys.PowerDown: return Make(key, Arp(new[] { 76, 72, 67 }, 0.08f, 0.25f), 0.5f);
                case AudioKeys.Bump: return Make(key, Thud(0.3f, 110f, 45f, 0.7f));
                case AudioKeys.Crash: return Make(key, Crash(1.3f, 1f));
                case AudioKeys.Smash: return Make(key, Crash(0.7f, 0.6f));
                case AudioKeys.Grunt: return Make(key, Voice(0.28f, 150f, 110f, 700f, 1150f, 0.2f), 0.6f);
                case AudioKeys.ZombieGrowl: return Make(key, Growl(1.5f, 85f, 0.6f));
                case AudioKeys.BruteRoar: return Make(key, Growl(2.0f, 55f, 0.9f));
                case AudioKeys.ZombieAttack: return Make(key, Growl(0.9f, 170f, 1f, true));
                case AudioKeys.ZombieHit: return Make(key, Mix(Thud(0.3f, 100f, 50f, 0.6f), Growl(0.5f, 110f, 0.5f), 0.6f));
                case AudioKeys.GameOver: return Make(key, GameOverSting(), 0.8f);
                case AudioKeys.Countdown: return Make(key, Bell(new[] { 880f }, 0.0f, 0.18f), 0.5f);
                case AudioKeys.Go: return Make(key, Bell(new[] { 1318f, 1760f }, 0.0f, 0.5f), 0.6f);
                case AudioKeys.Checkpoint: return Make(key, Arp(new[] { 72, 76, 79, 84 }, 0.09f, 0.6f), 0.7f);
                case AudioKeys.UIClick: return Make(key, Click(), 0.4f);
                case AudioKeys.Chase: return Make(key, ChaseLoop(8f), 0.8f);
                case AudioKeys.MusicMenu: return Make(key, Music(MusicStyle.Menu, 32f), 0.7f);
                case AudioKeys.MusicForest: return Make(key, Music(MusicStyle.Forest, 32f), 0.7f);
                case AudioKeys.MusicDesert: return Make(key, Music(MusicStyle.Desert, 32f), 0.7f);
                case AudioKeys.MusicDarkCity: return Make(key, Music(MusicStyle.DarkCity, 32f), 0.7f);
                case AudioKeys.MusicAbandoned: return Make(key, Music(MusicStyle.Abandoned, 32f), 0.7f);
                case AudioKeys.AmbForest: return Make(key, Ambience(AmbienceStyle.Forest, 12f), 0.6f);
                case AudioKeys.AmbDesert: return Make(key, Ambience(AmbienceStyle.DesertWind, 12f), 0.6f);
                case AudioKeys.AmbCity: return Make(key, Ambience(AmbienceStyle.CityRain, 12f), 0.6f);
                case AudioKeys.AmbRuins: return Make(key, Ambience(AmbienceStyle.RuinedCity, 12f), 0.6f);
            }
            Debug.LogWarning("[ZombieRunner] Unknown audio key " + key);
            return null;
        }

        static float[] Mix(float[] a, float[] b, float bGain)
        {
            var o = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) o[i] += a[i];
            for (int i = 0; i < b.Length; i++) o[i] += b[i] * bGain;
            return o;
        }

        // ================================================================== SFX
        static float[] Footstep(RoadStyle s)
        {
            float len = s == RoadStyle.DesertRoad ? 0.22f : 0.16f;
            var d = new float[Len(len)];
            var lp = new OnePole(); var bp = new BandPass();
            float thumpF = 70f + N() * 10f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float thump = Mathf.Sin(2 * Mathf.PI * thumpF * t) * Mathf.Exp(-t * 40f);
                float n = N();
                float tex;
                switch (s)
                {
                    case RoadStyle.Mud: tex = bp.Process(n, 350f + 200f * Mathf.Sin(t * 60f), 2f) * Mathf.Exp(-t * 22f) * 1.5f; break;
                    case RoadStyle.DesertRoad: tex = lp.HP(n, 2500f) * Mathf.Exp(-t * 14f) * 0.5f + bp.Process(n, 1200f, 1f) * Mathf.Exp(-t * 30f); break;
                    case RoadStyle.WetAsphalt: tex = bp.Process(n, 2800f, 1.5f) * Mathf.Exp(-t * 50f) + lp.HP(n, 4000f) * Mathf.Exp(-t * 18f) * 0.4f; break;
                    default: tex = bp.Process(n, 2000f, 1.2f) * Mathf.Exp(-t * 55f) + lp.LP(n, 600f) * Mathf.Exp(-t * 25f) * 0.5f; break;
                }
                d[i] = thump * 0.8f + tex;
            }
            return d;
        }

        static float[] Sweep(float len, float f0, float f1, float q, float attack)
        {
            var d = new float[Len(len)];
            var bp = new BandPass();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float env = t < attack ? t / attack : Mathf.Pow(1f - (t - attack) / (1f - attack), 2f);
                d[i] = bp.Process(N(), Mathf.Lerp(f0, f1, t), q) * env;
            }
            return d;
        }

        static float[] Thud(float len, float f0, float f1, float noise)
        {
            var d = new float[Len(len)];
            var lp = new OnePole();
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                ph += Mathf.Lerp(f0, f1, Mathf.Clamp01(t / len)) / SR;
                d[i] = Sin(ph) * Mathf.Exp(-t * 14f) + lp.LP(N(), 900f) * noise * Mathf.Exp(-t * 30f);
            }
            return d;
        }

        static float[] SlideSfx()
        {
            var d = new float[Len(0.65f)];
            var lp = new OnePole();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float env = Mathf.Min(1f, t * 12f) * (1f - t);
                float scrape = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 28f);
                d[i] = lp.LP(N(), Mathf.Lerp(3200f, 500f, t)) * env * scrape;
            }
            return d;
        }

        static float[] Bell(float[] freqs, float step, float decay)
        {
            float len = step * (freqs.Length - 1) + decay + 0.05f;
            var d = new float[Len(len)];
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = Len(step * n);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)SR;
                    float env = Mathf.Exp(-t / decay * 4f) * Mathf.Min(1f, t * 400f);
                    d[i] += (Mathf.Sin(2 * Mathf.PI * freqs[n] * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * freqs[n] * 2.01f * t) + 0.12f * Mathf.Sin(2 * Mathf.PI * freqs[n] * 3.02f * t)) * env;
                }
            }
            return d;
        }

        static float[] Arp(int[] notes, float step, float tail)
        {
            var d = new float[Len(step * notes.Length + tail)];
            for (int n = 0; n < notes.Length; n++)
            {
                float f = NoteHz(notes[n]);
                int start = Len(step * n);
                double ph = 0;
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)SR;
                    ph += f / SR;
                    float env = Mathf.Exp(-t * 6f) * Mathf.Min(1f, t * 300f);
                    d[i] += (Sin(ph) * 0.7f + Tri(ph * 2) * 0.3f) * env;
                }
            }
            return d;
        }

        static float[] PowerUpSfx(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.Magnet: return Arp(new[] { 72, 76, 79, 84 }, 0.06f, 0.4f);
                case PowerUpType.SpeedBoost:
                {
                    var d = new float[Len(0.7f)];
                    double ph = 0; var bp = new BandPass();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float x = i / (float)d.Length;
                        ph += Mathf.Lerp(250f, 1800f, x * x) / SR;
                        d[i] = (Saw(ph) * 0.4f + bp.Process(N(), Mathf.Lerp(800f, 5000f, x), 1f)) * (1f - x) * Mathf.Min(1f, x * 30f);
                    }
                    return d;
                }
                case PowerUpType.Shield:
                {
                    var d = new float[Len(0.9f)];
                    float[] f = { NoteHz(72), NoteHz(79), NoteHz(84) };
                    for (int i = 0; i < d.Length; i++)
                    {
                        float tt = i / (float)SR;
                        float trem = 0.7f + 0.3f * Mathf.Sin(tt * 2 * Mathf.PI * 14f);
                        float s = 0; foreach (var hz in f) s += Mathf.Sin(2 * Mathf.PI * hz * tt);
                        d[i] = s * trem * Mathf.Exp(-tt * 3f) * Mathf.Min(1f, tt * 50f);
                    }
                    return d;
                }
                case PowerUpType.DoubleCoins: return Mix(Bell(new[] { 1568f, 2093f }, 0.07f, 0.3f), PadLeft(Bell(new[] { 1760f, 2349f }, 0.07f, 0.4f), 0.16f), 1f);
                default: return Arp(new[] { 69, 73, 76, 81 }, 0.09f, 0.6f);
            }
        }

        static float[] PadLeft(float[] a, float sec)
        {
            int off = Len(sec);
            var o = new float[a.Length + off];
            Array.Copy(a, 0, o, off, a.Length);
            return o;
        }

        static float[] Crash(float len, float size)
        {
            var d = new float[Len(len)];
            var lp = new OnePole();
            float[] partials = { 340f, 517f, 793f, 1231f, 1877f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float noise = lp.LP(N(), 2500f) * Mathf.Exp(-t * 6f / size);
                float metal = 0; for (int p = 0; p < partials.Length; p++) metal += Mathf.Sin(2 * Mathf.PI * partials[p] * (1f + N() * 0.002f) * t) * Mathf.Exp(-t * (5f + p * 2f));
                float boom = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(60f, 35f, t / len) * t) * Mathf.Exp(-t * 5f);
                d[i] = noise * 1.2f + metal * 0.18f * size + boom * size;
            }
            return d;
        }

        static float[] Voice(float len, float f0, float f1, float formant1, float formant2, float noise)
        {
            var d = new float[Len(len)];
            var b1 = new BandPass(); var b2 = new BandPass();
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float x = i / (float)d.Length;
                ph += Mathf.Lerp(f0, f1, x) / SR;
                float src = Saw(ph) + N() * noise;
                float env = Mathf.Min(1f, x * 20f) * Mathf.Pow(1f - x, 1.5f);
                d[i] = (b1.Process(src, formant1, 4f) + b2.Process(src, formant2, 5f) * 0.6f) * env;
            }
            return d;
        }

        /// <summary>Zombie growl: jittery low voice through moving formants, soft-clipped.</summary>
        static float[] Growl(float len, float baseHz, float roughness, bool snarl = false)
        {
            var d = new float[Len(len)];
            var b1 = new BandPass(); var b2 = new BandPass(); var b3 = new BandPass();
            double ph = 0;
            float pitch = baseHz, drift = 0;
            float lfoRate = 3f + (float)rnd.NextDouble() * 4f;
            for (int i = 0; i < d.Length; i++)
            {
                float x = i / (float)d.Length;
                float t = i / (float)SR;
                drift += N() * 0.6f; drift *= 0.995f;
                pitch = baseHz * (1f + drift * 0.01f + (snarl ? x * 0.6f : -x * 0.25f));
                ph += pitch / SR;
                float src = Saw(ph) * 0.8f + Saw(ph * 0.5 + 0.13) * 0.4f + N() * roughness * 0.6f;
                float am = 0.65f + 0.35f * Mathf.Sin(t * 2 * Mathf.PI * lfoRate) * (0.5f + 0.5f * Mathf.PerlinNoise(t * 9f, 0.3f));
                float f1 = Mathf.Lerp(snarl ? 900f : 650f, 380f, x);
                float f2 = Mathf.Lerp(snarl ? 1700f : 1150f, 800f, x);
                float y = b1.Process(src, f1, 3f) + b2.Process(src, f2, 4f) * 0.7f + b3.Process(src, 2600f, 6f) * 0.25f * roughness;
                float env = Mathf.Min(1f, x * 8f) * Mathf.Pow(1f - x, 0.8f);
                d[i] = (float)Math.Tanh(y * 3f) * env;
            }
            return d;
        }

        static float[] Click()
        {
            var d = new float[Len(0.05f)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = (Mathf.Sin(2 * Mathf.PI * 2200f * t) * 0.6f + N() * 0.3f) * Mathf.Exp(-t * 120f);
            }
            return d;
        }

        static float[] GameOverSting()
        {
            int[] notes = { 57, 53, 50, 45 };
            var d = new float[Len(3.2f)];
            var lp = new OnePole();
            for (int n = 0; n < notes.Length; n++)
            {
                float f = NoteHz(notes[n]);
                int start = Len(n * 0.45f);
                double ph = 0;
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)SR;
                    ph += f / SR;
                    d[i] += (Saw(ph) * 0.4f + Sin(ph) * 0.6f) * Mathf.Exp(-t * 1.6f) * Mathf.Min(1f, t * 40f);
                }
            }
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = lp.LP(d[i], 1400f) + Mathf.Sin(2 * Mathf.PI * 41f * t) * 0.5f * Mathf.Exp(-t * 0.8f) * Mathf.Min(1f, t * 4f);
            }
            Echo(d, 0.23f, 0.35f, false);
            return d;
        }

        static void Echo(float[] d, float delaySec, float feedback, bool wrap)
        {
            int delay = Len(delaySec);
            for (int i = 0; i < d.Length; i++)
            {
                int j = i - delay;
                if (j < 0) { if (!wrap) continue; j += d.Length; }
                d[i] += d[j] * feedback;
            }
        }

        // ================================================================== LOOPS
        static void Add(float[] buf, int index, float v) { buf[((index % buf.Length) + buf.Length) % buf.Length] += v; }

        static float[] ChaseLoop(float len)
        {
            var d = new float[Len(len)];
            // heavy footfalls and heartbeat
            for (float t = 0; t < len; t += 0.8f)
            {
                StampInto(d, Thud(0.25f, 70f, 45f, 0.3f), t, 0.9f);
                StampInto(d, Thud(0.2f, 65f, 45f, 0.2f), t + 0.22f, 0.6f);
            }
            // overlapping growls
            for (int k = 0; k < 9; k++)
                StampInto(d, Growl(1.1f + (float)rnd.NextDouble(), 60f + (float)rnd.NextDouble() * 70f, 0.6f + (float)rnd.NextDouble() * 0.4f), (float)rnd.NextDouble() * len, 0.5f + (float)rnd.NextDouble() * 0.4f);
            // shuffling feet
            var lp = new OnePole();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float steps = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2 * Mathf.PI * 3.1f)), 8f) + Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2 * Mathf.PI * 2.3f + 1f)), 8f);
                d[i] += lp.LP(N(), 700f) * steps * 0.5f;
            }
            return d;
        }

        static void StampInto(float[] d, float[] src, float atSec, float gain)
        {
            int off = Len(atSec);
            for (int i = 0; i < src.Length; i++) Add(d, off + i, src[i] * gain);
        }

        static float[] Ambience(AmbienceStyle s, float len)
        {
            var d = new float[Len(len)];
            var lp = new OnePole(); var lp2 = new OnePole(); var bp = new BandPass(); var hp = new OnePole();
            int L = d.Length;
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)SR;
                float loopPh = t / len * 2f * Mathf.PI; // keeps LFOs periodic over the loop
                float n = N();
                switch (s)
                {
                    case AmbienceStyle.Forest:
                    {
                        float wind = lp.LP(n, 380f) * (0.5f + 0.35f * Mathf.Sin(loopPh * 2f) + 0.15f * Mathf.Sin(loopPh * 5f));
                        float cricket = Mathf.Sin(2 * Mathf.PI * 4600f * t) * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 2 * Mathf.PI * 28f)), 6f) * (Mathf.Sin(loopPh * 3f) > 0.2f ? 0.05f : 0f);
                        d[i] = wind * 0.9f + cricket;
                        break;
                    }
                    case AmbienceStyle.DesertWind:
                    {
                        float c = 450f + 300f * Mathf.Sin(loopPh * 2f) + 150f * Mathf.Sin(loopPh * 7f);
                        float howl = bp.Process(n, c, 3f) * (0.7f + 0.3f * Mathf.Sin(loopPh * 3f));
                        float hiss = hp.HP(n, 3000f) * 0.12f * (0.6f + 0.4f * Mathf.Sin(loopPh * 5f));
                        float rumble = lp.LP(n, 120f) * 0.8f;
                        d[i] = howl * 1.4f + hiss + rumble;
                        break;
                    }
                    case AmbienceStyle.CityRain:
                    {
                        float rain = hp.HP(n, 900f) * 0.35f;
                        float drop = n > 0.995f ? N() * 0.8f : 0f;
                        float hum = (Mathf.Sin(2 * Mathf.PI * 50f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 100f * t)) * 0.05f;
                        float sirenEnv = Mathf.Max(0, Mathf.Sin(loopPh)) ;
                        float siren = Mathf.Sin(2 * Mathf.PI * (700f + 180f * Mathf.Sin(t * 2 * Mathf.PI * 0.5f)) * t) * 0.05f * sirenEnv * sirenEnv;
                        d[i] = rain + lp2.LP(drop, 3000f) + hum + lp.LP(siren, 1500f) * 3f;
                        break;
                    }
                    default:
                    {
                        float wind = lp.LP(n, 300f) * (0.6f + 0.4f * Mathf.Sin(loopPh * 2f));
                        float rumble = lp2.LP(N(), 90f) * (0.5f + 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(loopPh * 3f + 1f)), 6f)) * 2f;
                        d[i] = wind + rumble;
                        break;
                    }
                }
            }
            // occasional events
            switch (s)
            {
                case AmbienceStyle.Forest:
                    StampInto(d, Voice(0.35f, 560f, 480f, 1100f, 1900f, 0.4f), 2.3f, 0.25f); // crow
                    StampInto(d, Voice(0.3f, 540f, 470f, 1100f, 1900f, 0.4f), 2.75f, 0.2f);
                    StampInto(d, Voice(0.9f, 120f, 95f, 300f, 600f, 0.05f), 7.1f, 0.15f); // tree creak
                    StampInto(d, Growl(1.4f, 75f, 0.5f), 9.4f, 0.15f); // distant zombie
                    break;
                case AmbienceStyle.DesertWind:
                    StampInto(d, Voice(1.4f, 900f, 1150f, 1000f, 2000f, 0.02f), 4f, 0.08f); // whistle
                    StampInto(d, Growl(1.5f, 70f, 0.6f), 8.5f, 0.12f);
                    break;
                case AmbienceStyle.CityRain:
                    StampInto(d, Crash(0.7f, 0.4f), 5.2f, 0.06f); // distant clatter
                    StampInto(d, Growl(1.4f, 80f, 0.6f), 9.8f, 0.14f);
                    break;
                default:
                    StampInto(d, Crash(1.2f, 1f), 3.5f, 0.08f); // distant collapse
                    StampInto(d, Voice(0.35f, 560f, 480f, 1100f, 1900f, 0.4f), 7f, 0.15f);
                    StampInto(d, Growl(1.4f, 75f, 0.5f), 10f, 0.12f);
                    break;
            }
            return d;
        }

        // ================================================================== MUSIC
        enum MusicStyle { Menu, Forest, Desert, DarkCity, Abandoned }

        /// <summary>Small step sequencer: pad, bass, ostinato and drums over a 4-chord progression. Seamless loop.</summary>
        static float[] Music(MusicStyle style, float len)
        {
            int L = Len(len);
            var d = new float[L];
            float bpm; int root; int[] scale; int[][] chords; bool drums = true;
            switch (style)
            {
                case MusicStyle.Forest: bpm = 96; root = 50; scale = new[] { 0, 2, 3, 5, 7, 8, 10 }; chords = new[] { new[] { 0, 3, 7 }, new[] { -2, 3, 7 }, new[] { -4, 0, 3 }, new[] { -5, -2, 2 } }; break;
                case MusicStyle.Desert: bpm = 104; root = 52; scale = new[] { 0, 1, 4, 5, 7, 8, 10 }; chords = new[] { new[] { 0, 4, 7 }, new[] { 1, 5, 8 }, new[] { 0, 4, 7 }, new[] { -2, 1, 5 } }; break;
                case MusicStyle.DarkCity: bpm = 116; root = 45; scale = new[] { 0, 2, 3, 5, 7, 8, 10 }; chords = new[] { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -5, -1, 2 } }; break;
                case MusicStyle.Abandoned: bpm = 88; root = 48; scale = new[] { 0, 2, 3, 5, 7, 8, 11 }; chords = new[] { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -7, -4, 0 }, new[] { -5, -1, 2 } }; break;
                default: bpm = 64; root = 52; scale = new[] { 0, 2, 3, 5, 7, 8, 10 }; chords = new[] { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -7, -3, 0 } }; drums = false; break;
            }
            // Fit a whole number of bars into the loop.
            float barSec = 240f / bpm;
            int bars = Mathf.Max(4, Mathf.RoundToInt(len / barSec / 4f) * 4);
            barSec = len / bars;
            float beat = barSec / 4f;
            int barsPerChord = bars / 4;

            // --- Pad
            var lpPad = new OnePole();
            var padTmp = new float[L];
            for (int c = 0; c < 4; c++)
            {
                int s0 = Len(c * barsPerChord * barSec), s1 = Len((c + 1) * barsPerChord * barSec);
                foreach (var iv in chords[c])
                {
                    float f = NoteHz(root + 12 + iv);
                    double p1 = rnd.NextDouble(), p2 = rnd.NextDouble();
                    for (int i = s0; i < s1 + Len(0.6f); i++)
                    {
                        float t = (i - s0) / (float)SR;
                        float env = Mathf.Min(1f, t / 0.6f) * (i > s1 ? Mathf.Max(0f, 1f - (i - s1) / (float)Len(0.6f)) : 1f);
                        p1 += f * 1.003 / SR; p2 += f * 0.997 / SR;
                        Add(padTmp, i, (Saw(p1) + Saw(p2)) * 0.5f * env);
                    }
                }
            }
            for (int i = 0; i < L * 2; i++) // run twice so the filter state wraps cleanly
            {
                int k = i % L;
                float cutoff = style == MusicStyle.DarkCity ? 1400f + 600f * Mathf.Sin(k / (float)L * Mathf.PI * 4f) : 900f;
                float v = lpPad.LP(padTmp[k], cutoff);
                if (i >= L) d[k] += v * (style == MusicStyle.Menu ? 0.5f : 0.28f);
            }

            // --- Bass
            for (int bar = 0; bar < bars; bar++)
            {
                int c = Mathf.Min(3, bar / barsPerChord);
                float f = NoteHz(root - 12 + chords[c][0]);
                int stepsPerBar = style == MusicStyle.DarkCity ? 8 : style == MusicStyle.Menu ? 1 : 4;
                for (int st = 0; st < stepsPerBar; st++)
                {
                    if (style == MusicStyle.Forest && st % 2 == 1) continue;
                    float at = bar * barSec + st * barSec / stepsPerBar;
                    float dur = barSec / stepsPerBar * 0.9f;
                    int s0 = Len(at); double ph = 0;
                    var lp = new OnePole();
                    for (int i = 0; i < Len(dur); i++)
                    {
                        float t = i / (float)SR;
                        ph += f / SR;
                        float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * (style == MusicStyle.Menu ? 0.8f : 5f));
                        Add(d, s0 + i, lp.LP(Saw(ph) * 0.5f + Sin(ph) * 0.8f, 500f) * env * 0.35f);
                    }
                }
            }

            // --- Ostinato / arp (plucked)
            int[] pattern;
            int perBar;
            switch (style)
            {
                case MusicStyle.DarkCity: pattern = new[] { 0, 2, 4, 2, 7, 4, 2, 4, 0, 2, 4, 2, 7, 4, 9, 7 }; perBar = 16; break;
                case MusicStyle.Desert: pattern = new[] { 0, 1, 0, 2, 0, 1, 4, 2 }; perBar = 8; break;
                case MusicStyle.Abandoned: pattern = new[] { 0, 4, 7, 4, 0, 4, 7, 9 }; perBar = 8; break;
                case MusicStyle.Forest: pattern = new[] { 0, -1, 4, -1, 2, -1, 4, 7 }; perBar = 8; break;
                default: pattern = new[] { 7, -1, -1, -1, 4, -1, -1, -1, 9, -1, -1, -1, 2, -1, -1, -1 }; perBar = 16; break;
            }
            for (int bar = 0; bar < bars; bar++)
            {
                int c = Mathf.Min(3, bar / barsPerChord);
                for (int st = 0; st < perBar; st++)
                {
                    int deg = pattern[st % pattern.Length];
                    if (deg < 0) continue;
                    int semis = scale[deg % 7] + 12 * (deg / 7);
                    float f = NoteHz(root + 24 + chords[c][0] + semis - (style == MusicStyle.Abandoned ? 12 : 0));
                    int s0 = Len(bar * barSec + st * barSec / perBar);
                    double ph = 0;
                    var lp = new OnePole();
                    float decay = style == MusicStyle.Menu ? 1.5f : 7f;
                    for (int i = 0; i < Len(style == MusicStyle.Menu ? 2.5f : 0.5f); i++)
                    {
                        float t = i / (float)SR;
                        ph += f / SR;
                        float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 500f);
                        float tone = style == MusicStyle.Menu || style == MusicStyle.Abandoned ? Sin(ph) + 0.3f * Sin(ph * 2) + 0.1f * Sin(ph * 3) : Saw(ph);
                        Add(d, s0 + i, lp.LP(tone, 2200f * env + 300f) * env * (style == MusicStyle.Menu ? 0.25f : 0.18f));
                    }
                }
            }

            // --- Drums
            if (drums)
            {
                var kick = KickSample(style == MusicStyle.Forest ? 0.6f : 0.35f, style == MusicStyle.Forest ? 95f : 140f);
                var snare = SnareSample();
                var hat = HatSample();
                for (int bar = 0; bar < bars; bar++)
                {
                    float b0 = bar * barSec;
                    switch (style)
                    {
                        case MusicStyle.Forest: // taiko-style
                            StampInto(d, kick, b0, 0.9f); StampInto(d, kick, b0 + beat * 1.5f, 0.6f); StampInto(d, kick, b0 + beat * 2f, 0.8f);
                            if (bar % 2 == 1) { StampInto(d, kick, b0 + beat * 3f, 0.6f); StampInto(d, kick, b0 + beat * 3.5f, 0.7f); }
                            break;
                        case MusicStyle.Desert:
                            StampInto(d, kick, b0, 0.8f); StampInto(d, kick, b0 + beat * 2.5f, 0.6f);
                            StampInto(d, snare, b0 + beat * 1f, 0.35f); StampInto(d, snare, b0 + beat * 3f, 0.45f);
                            for (int h = 0; h < 8; h++) StampInto(d, hat, b0 + h * beat / 2f, h % 2 == 0 ? 0.25f : 0.15f);
                            break;
                        case MusicStyle.DarkCity:
                            for (int k = 0; k < 4; k++) StampInto(d, kick, b0 + k * beat, 0.8f);
                            StampInto(d, snare, b0 + beat, 0.6f); StampInto(d, snare, b0 + beat * 3f, 0.6f);
                            for (int h = 0; h < 8; h++) StampInto(d, hat, b0 + h * beat / 2f + beat / 4f, 0.2f);
                            break;
                        default: // abandoned: cinematic hits
                            StampInto(d, kick, b0, 1f); StampInto(d, kick, b0 + beat * 0.75f, 0.5f);
                            if (bar % 2 == 1) StampInto(d, snare, b0 + beat * 2f, 0.5f);
                            break;
                    }
                }
            }
            if (style == MusicStyle.Menu || style == MusicStyle.Abandoned) Echo(d, 0.375f, 0.3f, true);
            // soft clip
            for (int i = 0; i < L; i++) d[i] = (float)Math.Tanh(d[i] * 1.2f);
            return d;
        }

        static float[] KickSample(float len, float f0)
        {
            var d = new float[Len(len)];
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                ph += (45f + (f0 - 45f) * Mathf.Exp(-t * 25f)) / SR;
                d[i] = Sin(ph) * Mathf.Exp(-t * (4f / len)) + N() * 0.1f * Mathf.Exp(-t * 200f);
            }
            return d;
        }

        static float[] SnareSample()
        {
            var d = new float[Len(0.25f)];
            var bp = new BandPass();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = bp.Process(N(), 1900f, 0.8f) * Mathf.Exp(-t * 18f) + Mathf.Sin(2 * Mathf.PI * 185f * t) * Mathf.Exp(-t * 30f) * 0.5f;
            }
            return d;
        }

        static float[] HatSample()
        {
            var d = new float[Len(0.06f)];
            var hp = new OnePole();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = hp.HP(N(), 7000f) * Mathf.Exp(-t * 70f);
            }
            return d;
        }
    }
}
