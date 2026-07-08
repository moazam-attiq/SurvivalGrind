using UnityEngine;

namespace SurvivalGrind.UI
{
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        private const string MusicVolumeKey = "SG_MusicVolume";

        private static MusicPlayer _instance;

        [SerializeField] private AudioClip track;
        [SerializeField] private float defaultVolume = 0.6f;

        public static MusicPlayer Instance => _instance;

        public AudioSource Source { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Source = GetComponent<AudioSource>();
            Source.clip = track;
            Source.loop = true;
            Source.playOnAwake = false;
            Source.volume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultVolume);
        }

        private void Start()
        {
            if (!Source.isPlaying) Source.Play();
        }

        public void SetVolume(float volume)
        {
            Source.volume = volume;
            PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        }
    }
}
