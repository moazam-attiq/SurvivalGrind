using UnityEngine;

namespace SurvivalGrind.UI
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;

        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip pauseClip;
        [SerializeField] private AudioClip resumeClip;

        private AudioSource sfxSource;

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
        }

        public static void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || !Application.isPlaying) return;
            var inst = Instance;
            if (inst == null) return;
            if (inst.sfxSource == null) inst.sfxSource = inst.GetComponent<AudioSource>();
            inst.sfxSource.PlayOneShot(clip, volumeScale);
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
