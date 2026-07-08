using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalGrind.UI
{
    public class SettingsOverlayController : MonoBehaviour
    {
        private const string MasterVolumeKey = "SG_MasterVolume";

        [Header("Overlay")]
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private CanvasGroup[] backgroundGroups;

        [Header("Controls")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;

        [Header("Music")]
        [SerializeField] private MusicPlayer musicPlayer;

        private Coroutine _fadeRoutine;
        private bool _isOpen;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            AudioListener.volume = masterVolume;
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(masterVolume);

            if (musicVolumeSlider != null)
            {
                float musicVolume = PlayerPrefs.GetFloat("SG_MusicVolume", 0.6f);
                musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            }

            overlayGroup.alpha = 0f;
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            _isOpen = true;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Fade(1f));
        }

        public void Close()
        {
            _isOpen = false;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Fade(0f));
        }

        private IEnumerator Fade(float target)
        {
            bool opening = target > 0.5f;
            overlayGroup.interactable = opening;
            overlayGroup.blocksRaycasts = opening;
            SetBackgroundInteractable(!opening);

            float start = overlayGroup.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / fadeDuration));
                overlayGroup.alpha = alpha;
                SetBackgroundAlpha(1f - alpha);
                yield return null;
            }
            overlayGroup.alpha = target;
            SetBackgroundAlpha(1f - target);
        }

        private void SetBackgroundAlpha(float alpha)
        {
            if (backgroundGroups == null) return;
            foreach (var g in backgroundGroups) if (g != null) g.alpha = alpha;
        }

        private void SetBackgroundInteractable(bool interactable)
        {
            if (backgroundGroups == null) return;
            foreach (var g in backgroundGroups)
            {
                if (g == null) continue;
                g.interactable = interactable;
                g.blocksRaycasts = interactable;
            }
        }

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            var player = musicPlayer != null ? musicPlayer : MusicPlayer.Instance;
            if (player != null) player.SetVolume(value);
        }

    }
}
