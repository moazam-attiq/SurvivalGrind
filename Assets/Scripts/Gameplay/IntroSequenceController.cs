using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    public class IntroSequenceController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup titleCardGroup;
        [SerializeField] private CanvasGroup fadeOverlayGroup;
        [SerializeField] private GameObject hud;
        [SerializeField] private GameObject abilityBar;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private InstructionsModalController instructionsModal;

        [Header("Countdown")]
        [SerializeField] private TMP_Text countdownText;

        [SerializeField] private float titleHoldDuration = 3f;
        [SerializeField] private float titleFadeOutDuration = 0.5f;
        [SerializeField] private float overlayFadeOutDuration = 0.6f;

        public UnityEvent onGameplayStart;

        private void Awake()
        {
            if (titleCardGroup != null) titleCardGroup.alpha = 1f;
            if (fadeOverlayGroup != null)
            {
                fadeOverlayGroup.alpha = 1f;
                fadeOverlayGroup.blocksRaycasts = true;
            }
            if (hud != null) hud.SetActive(false);
            if (abilityBar != null) abilityBar.SetActive(false);
        }

        private void Start()
        {
            if (instructionsModal == null)
            {
                instructionsModal = FindFirstObjectByType<InstructionsModalController>(FindObjectsInactive.Include);
            }
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            yield return RunCountdown(titleHoldDuration);

            yield return Fade(titleCardGroup, 1f, 0f, titleFadeOutDuration);

            yield return Fade(fadeOverlayGroup, 1f, 0f, overlayFadeOutDuration);
            if (fadeOverlayGroup != null) fadeOverlayGroup.blocksRaycasts = false;

            if (hud != null) hud.SetActive(true);
            if (abilityBar != null) abilityBar.SetActive(true);
            if (musicSource != null) musicSource.Play();

            if (instructionsModal != null) instructionsModal.ShowForGameplayStart();
            onGameplayStart?.Invoke();

            gameObject.SetActive(false);
        }

        private IEnumerator RunCountdown(float duration)
        {
            int secondsLeft = Mathf.CeilToInt(duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (countdownText != null) countdownText.text = secondsLeft.ToString();
                float step = Mathf.Min(1f, duration - elapsed);
                yield return new WaitForSeconds(step);
                elapsed += step;
                secondsLeft--;
            }

            if (countdownText != null) countdownText.text = string.Empty;
        }

        private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
