using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SurvivalGrind.Gameplay;

namespace SurvivalGrind.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private RectTransform slidingWindow;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private SettingsOverlayController settingsOverlay;
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float offscreenOffset = 900f;

        private Coroutine slideRoutine;
        private float onscreenX;
        private float offscreenX;

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(PauseGame);
            if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);

            if (slidingWindow != null)
            {
                onscreenX = slidingWindow.anchoredPosition.x;
                offscreenX = onscreenX + offscreenOffset;
            }
        }

        private void PauseGame()
        {
            LevelRunner.Instance.Pause();
            if (pausePanel != null) pausePanel.SetActive(true);
            if (slidingWindow != null)
            {
                slidingWindow.anchoredPosition = new Vector2(offscreenX, slidingWindow.anchoredPosition.y);
                Slide(onscreenX, null);
            }
            AudioManager.PlayPause();
        }

        private void ResumeGame()
        {
            LevelRunner.Instance.Resume();
            AudioManager.PlayResume();
            if (slidingWindow != null)
            {
                Slide(offscreenX, () => { if (pausePanel != null) pausePanel.SetActive(false); });
            }
            else if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        private void Slide(float targetX, System.Action onComplete)
        {
            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideRoutine(targetX, onComplete));
        }

        private IEnumerator SlideRoutine(float targetX, System.Action onComplete)
        {
            float startX = slidingWindow.anchoredPosition.x;
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Lerp(startX, targetX, Mathf.Clamp01(t / slideDuration));
                slidingWindow.anchoredPosition = new Vector2(x, slidingWindow.anchoredPosition.y);
                yield return null;
            }
            slidingWindow.anchoredPosition = new Vector2(targetX, slidingWindow.anchoredPosition.y);
            onComplete?.Invoke();
        }

        private void ToggleSettings()
        {
            if (settingsOverlay != null) settingsOverlay.Toggle();
        }

        private void GoHome()
        {
            LevelRunner.Instance.GoHome();
        }
    }
}
