using UnityEngine;

namespace ZombieRunner
{
    /// <summary>
    /// Persistent audio: music (cross-faded), ambience, chase layer, pooled 2D and 3D sound effects.
    /// Volumes follow GameSettings (music, SFX, master sound toggle). Clips come from the registry when
    /// a real recording was imported, otherwise from ProceduralAudio.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager inst;
        AudioSource musicA, musicB, ambience, chase, ui;
        AudioSource[] sfx;
        AudioSource[] spatial;
        int sfxIndex, spatialIndex;
        bool musicOnA = true;
        float musicFade = 1f;
        float chaseTarget;
        string currentMusic, currentAmb;
        int footstepVariant;

        static AudioManager I
        {
            get
            {
                if (inst == null)
                {
                    var go = new GameObject("ZR_AudioManager");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<AudioManager>();
                    inst.Init();
                }
                return inst;
            }
        }

        AudioSource Src(string n, bool loop, bool spatialSrc = false)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = spatialSrc ? 1f : 0f;
            if (spatialSrc) { s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3f; s.maxDistance = 45f; s.dopplerLevel = 0f; }
            return s;
        }

        void Init()
        {
            musicA = Src("MusicA", true); musicB = Src("MusicB", true);
            ambience = Src("Ambience", true);
            chase = Src("Chase", true);
            ui = Src("UI", false); ui.ignoreListenerPause = true;
            sfx = new AudioSource[10];
            for (int i = 0; i < sfx.Length; i++) sfx[i] = Src("Sfx" + i, false);
            spatial = new AudioSource[8];
            for (int i = 0; i < spatial.Length; i++) spatial[i] = Src("Spatial" + i, false, true);
            GameSettings.OnChanged += ApplyVolumes;
            ApplyVolumes();
        }

        void OnDestroy() { GameSettings.OnChanged -= ApplyVolumes; }

        static float Master => GameSettings.SoundOn ? 1f : 0f;

        void ApplyVolumes()
        {
            if (this == null) return;
            AudioListener.volume = Master;
        }

        public static AudioClip Clip(string key)
        {
            var reg = ZRAssetRegistry.Instance;
            var c = reg != null ? reg.Audio(key) : null;
            return c != null ? c : ProceduralAudio.Get(key);
        }

        // ------------------------------------------------------------------ public API
        public static void PlaySfx(string key, float volume = 1f, float pitch = 1f)
        {
            var m = I; var clip = Clip(key);
            if (clip == null) return;
            var s = m.sfx[m.sfxIndex = (m.sfxIndex + 1) % m.sfx.Length];
            s.pitch = pitch;
            s.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public static void UIClick()
        {
            var m = I; var clip = Clip(AudioKeys.UIClick);
            if (clip != null) m.ui.PlayOneShot(clip, 0.6f * GameSettings.SfxVolume);
        }

        public static void Play3D(string key, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var m = I; var clip = Clip(key);
            if (clip == null) return;
            var s = m.spatial[m.spatialIndex = (m.spatialIndex + 1) % m.spatial.Length];
            s.transform.position = pos;
            s.pitch = pitch;
            s.clip = clip;
            s.volume = volume * GameSettings.SfxVolume;
            s.Play();
        }

        public static void Footstep(RoadStyle road)
        {
            var m = I;
            m.footstepVariant = (m.footstepVariant + 1 + Random.Range(0, 2)) % 4;
            PlaySfx(AudioKeys.Footstep(road, m.footstepVariant), 0.45f, Random.Range(0.9f, 1.1f));
        }

        public static string MusicFor(ThemeId t)
        {
            switch (t)
            {
                case ThemeId.Forest: return AudioKeys.MusicForest;
                case ThemeId.Desert: return AudioKeys.MusicDesert;
                case ThemeId.DarkCity: return AudioKeys.MusicDarkCity;
                default: return AudioKeys.MusicAbandoned;
            }
        }

        public static string AmbienceKey(AmbienceStyle a)
        {
            switch (a)
            {
                case AmbienceStyle.Forest: return AudioKeys.AmbForest;
                case AmbienceStyle.DesertWind: return AudioKeys.AmbDesert;
                case AmbienceStyle.CityRain: return AudioKeys.AmbCity;
                default: return AudioKeys.AmbRuins;
            }
        }

        public static void PlayMusic(string key)
        {
            var m = I;
            if (m.currentMusic == key && (m.musicA.isPlaying || m.musicB.isPlaying)) return;
            m.currentMusic = key;
            var clip = Clip(key);
            if (clip == null) return;
            m.musicOnA = !m.musicOnA;
            var next = m.musicOnA ? m.musicA : m.musicB;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            m.musicFade = 0f;
        }

        public static void StopMusic()
        {
            var m = I;
            m.currentMusic = null;
            m.musicFade = -1f; // fade both out
        }

        public static void PlayAmbience(AmbienceStyle style)
        {
            var m = I;
            string key = AmbienceKey(style);
            if (m.currentAmb == key && m.ambience.isPlaying) return;
            m.currentAmb = key;
            m.ambience.clip = Clip(key);
            m.ambience.Play();
        }

        public static void StopAmbience()
        {
            var m = I;
            m.currentAmb = null;
            m.ambience.Stop();
        }

        /// <summary>0..1 how loud the horde behind the player is. 0 stops the chase layer.</summary>
        public static void SetChaseIntensity(float x)
        {
            var m = I;
            m.chaseTarget = x;
            if (x > 0f && !m.chase.isPlaying)
            {
                m.chase.clip = Clip(AudioKeys.Chase);
                m.chase.volume = 0f;
                m.chase.Play();
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float mv = GameSettings.MusicVolume * 0.55f;
            var cur = musicOnA ? musicA : musicB;
            var old = musicOnA ? musicB : musicA;
            if (musicFade >= 0f)
            {
                musicFade = Mathf.Min(1f, musicFade + dt / 1.5f);
                cur.volume = mv * musicFade;
                old.volume = mv * (1f - musicFade);
                if (musicFade >= 1f && old.isPlaying) old.Stop();
            }
            else
            {
                cur.volume = Mathf.MoveTowards(cur.volume, 0f, dt * 0.6f);
                old.volume = Mathf.MoveTowards(old.volume, 0f, dt * 0.6f);
            }
            ambience.volume = GameSettings.SfxVolume * 0.5f;
            chase.volume = Mathf.MoveTowards(chase.volume, chaseTarget * GameSettings.SfxVolume * 0.6f, dt * 0.8f);
            if (chaseTarget <= 0f && chase.volume <= 0.001f && chase.isPlaying) chase.Stop();
        }
    }
}
