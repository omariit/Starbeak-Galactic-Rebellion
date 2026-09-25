using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Central audio router. Loads every clip from Assets/Resources/Audio once,
    /// then plays SFX and music through a small pooled set of AudioSources.
    /// Volumes are driven by the persisted <see cref="GameSettings"/>.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string AudioResourceFolder = "Audio";
        private const int SfxSourceCount = 12;

        [Header("Music")]
        [SerializeField] private float musicFadeDuration = 1.2f;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> sfxSources = new List<AudioSource>(SfxSourceCount);
        private AudioSource musicSource;
        private int sfxCursor;

        private float masterVolume = 1f;
        private float sfxVolume = 1f;
        private float musicVolume = 0.8f;
        private bool muted;

        private float musicTargetVolume;
        private float musicFadeVelocity;

        public bool Muted => muted;

        private void Awake()
        {
            CrashLog.Info("AudioManager.Awake:enter");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CrashLog.Info("AudioManager.Awake:singleton");
            if (CrashLog.HasArgument("-starbeakNoAudio"))
            {
                CrashLog.Info("AudioManager.Awake:probe-disabled");
                return;
            }

            BuildSources();
            CrashLog.Info("AudioManager.Awake:sources");
            LoadAllClips();
            CrashLog.Info("AudioManager.Awake:clips");
            ApplySettings(SaveSystem.Instance?.Profile?.settings);
            CrashLog.Info("AudioManager.Awake:leave");
        }

        private void BuildSources()
        {
            // Music source (loops, independent pitch/volume).
            GameObject musicGo = new GameObject("MusicSource");
            musicGo.transform.SetParent(transform, false);
            musicSource = musicGo.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.priority = 0;

            // SFX pool: round-robin voices so overlapping shots never cut each other.
            for (int i = 0; i < SfxSourceCount; i++)
            {
                GameObject go = new GameObject($"SfxSource_{i:00}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 128;
                sfxSources.Add(source);
            }
        }

        private void LoadAllClips()
        {
            AudioClip[] loaded = Resources.LoadAll<AudioClip>(AudioResourceFolder);
            foreach (AudioClip clip in loaded)
            {
                if (clip == null) continue;
                clips[clip.name] = clip;
            }
            Debug.Log($"[AudioManager] Loaded {clips.Count} audio clips.");
        }

        /// <summary>Re-reads the persisted settings (called when the options menu changes)."</summary>
        public void ApplySettings(GameSettings settings)
        {
            if (settings == null) return;
            masterVolume = Mathf.Clamp01(settings.masterVolume);
            sfxVolume = Mathf.Clamp01(settings.sfxVolume);
            musicVolume = Mathf.Clamp01(settings.musicVolume);
            RefreshMusicVolume();
        }

        public void SetMuted(bool value)
        {
            muted = value;
            RefreshMusicVolume();
        }

        private void RefreshMusicVolume()
        {
            float effective = muted ? 0f : musicVolume * masterVolume;
            musicTargetVolume = effective;
            if (musicFadeDuration <= 0f)
            {
                musicSource.volume = effective;
                musicFadeVelocity = 0f;
            }
        }

        private void Update()
        {
            if (musicSource == null) return;
            // Smooth music fades between themes / mute toggles.
            if (!Mathf.Approximately(musicSource.volume, musicTargetVolume))
            {
                musicSource.volume = Mathf.SmoothDamp(
                    musicSource.volume, musicTargetVolume, ref musicFadeVelocity,
                    Mathf.Max(0.05f, musicFadeDuration));
            }
        }

        /// <summary>True if the named clip is available.</summary>
        public bool HasClip(string clipName)
        {
            return !string.IsNullOrEmpty(clipName) && clips.ContainsKey(clipName);
        }

        /// <summary>
        /// Plays a one-shot SFX at natural pitch/volume. Safe to call from pooled
        /// spawn paths; missing clips are ignored with a single warning.
        /// </summary>
        public void PlaySfx(string clipName, float volumeScale = 1f, float pitch = 1f)
        {
            if (sfxSources.Count == 0) return;
            if (muted || string.IsNullOrEmpty(clipName)) return;
            if (!clips.TryGetValue(clipName, out AudioClip clip) || clip == null)
            {
                Debug.LogWarning($"[AudioManager] Missing clip: {clipName}");
                return;
            }

            AudioSource source = sfxSources[sfxCursor];
            sfxCursor = (sfxCursor + 1) % sfxSources.Count;

            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            source.volume = Mathf.Clamp01(sfxVolume * masterVolume * volumeScale);
            source.PlayOneShot(clip);
        }

        /// <summary>Plays a SFX with up to <paramref name="jitter"/> semitone randomization.</summary>
        public void PlaySfxJittered(string clipName, float volumeScale = 1f, float jitter = 0.06f)
        {
            float pitch = Mathf.Pow(2f, Random.Range(-jitter, jitter));
            PlaySfx(clipName, volumeScale, pitch);
        }

        /// <summary>Crossfades to a looping music clip by name.</summary>
        public void PlayMusic(string clipName)
        {
            if (musicSource == null) return;
            CrashLog.Info($"AudioManager.PlayMusic:{clipName}:enter");
            if (string.IsNullOrEmpty(clipName)) return;
            if (!clips.TryGetValue(clipName, out AudioClip clip) || clip == null)
            {
                Debug.LogWarning($"[AudioManager] Missing music: {clipName}");
                return;
            }

            if (musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.clip = clip;
            musicSource.volume = 0f;
            musicTargetVolume = muted ? 0f : musicVolume * masterVolume;
            musicSource.Play();
            CrashLog.Info($"AudioManager.PlayMusic:{clipName}:leave");
        }

        public void StopMusic()
        {
            musicTargetVolume = 0f;
            if (musicFadeDuration <= 0f) musicSource.Stop();
        }

        /// <summary>Fades music out and stops it once silent.</summary>
        private System.Collections.IEnumerator StopMusicWhenSilent()
        {
            yield return new WaitUntil(() => Mathf.Approximately(musicSource.volume, 0f));
            musicSource.Stop();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
