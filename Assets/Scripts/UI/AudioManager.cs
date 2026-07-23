using UnityEngine;
using UnityEngine.SceneManagement;

namespace SurvivalGrind.UI
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;

        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip pauseClip;
        [SerializeField] private AudioClip resumeClip;

        [Tooltip("World-space range within which a positioned hazard sound (PlaySfxAt) is audible. Beyond this it's silently skipped instead of stacking with every other off-screen hazard.")]
        [SerializeField] private float audibleRange = 10f;

        private AudioSource sfxSource;
        private Transform listener;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("AudioManager (Auto)");
                    go.AddComponent<AudioManager>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            sfxSource = GetComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;

            if (Application.isPlaying)
            {
                EnsureListenerExists();
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureListenerExists();

        // Belt-and-suspenders: if a level's camera is ever missing its AudioListener (or it gets
        // lost some other way), every AudioSource.Play call silently fails and spams "no audio
        // listener" every frame. AudioManager persists across every scene and re-checks on each
        // scene load, so it's the one place that can guarantee a listener exists regardless of
        // what any individual level's camera setup does.
        private void EnsureListenerExists()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }
        }

        public static void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || !Application.isPlaying) return;
            var inst = Instance;
            if (inst == null) return;
            if (inst.sfxSource == null) inst.sfxSource = inst.GetComponent<AudioSource>();
            inst.sfxSource.PlayOneShot(clip, volumeScale);
        }

        /// Like PlaySfx, but for a hazard sound with a world position: fades out and skips
        /// entirely once the source is further than audibleRange from the Commander, so a
        /// screen full of off-screen bombs/bees doesn't turn into a wall of overlapping noise.
        public static void PlaySfxAt(AudioClip clip, Vector3 worldPosition, float volumeScale = 1f)
        {
            if (clip == null || !Application.isPlaying) return;
            var inst = Instance;
            if (inst == null) return;

            if (inst.listener == null)
            {
                var leader = GameObject.FindGameObjectWithTag("Leader");
                if (leader != null) inst.listener = leader.transform;
            }
            if (inst.listener == null)
            {
                PlaySfx(clip, volumeScale);
                return;
            }

            float distance = Vector3.Distance(inst.listener.position, worldPosition);
            if (distance >= inst.audibleRange) return;

            float falloff = 1f - distance / inst.audibleRange;
            PlaySfx(clip, volumeScale * falloff);
        }

        public static void PlayUiClick()
        {
            if (Instance != null) PlaySfx(Instance.uiClickClip);
        }

        public static void PlayPause()
        {
            if (Instance != null) PlaySfx(Instance.pauseClip);
        }

        public static void PlayResume()
        {
            if (Instance != null) PlaySfx(Instance.resumeClip);
        }
    }
}
