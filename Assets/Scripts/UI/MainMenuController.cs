using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SurvivalGrind.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panel A - Main Menu")]
        [SerializeField] private CanvasGroup[] mainMenuGroups;
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panel B - Level Select")]
        [SerializeField] private CanvasGroup levelSelectGroup;
        [SerializeField] private Button backButton;

        [Header("Settings Overlay")]
        [SerializeField] private SettingsOverlayController settingsOverlay;

        [Header("Transition")]
        [SerializeField] private float fadeDuration = 0.25f;

        private Coroutine _transitionRoutine;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(ShowLevelSelect);
            if (backButton != null) backButton.onClick.AddListener(ShowMainMenu);
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            SetGroupState(levelSelectGroup, visible: false, instant: true);
            foreach (var group in mainMenuGroups) SetGroupState(group, visible: true, instant: true);
        }

        public void ToggleSettings()
        {
            if (settingsOverlay != null) settingsOverlay.Toggle();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowLevelSelect()
        {
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(Transition(mainMenuGroups, levelSelectGroup));
        }

        public void ShowMainMenu()
        {
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(Transition(new[] { levelSelectGroup }, null, mainMenuGroups));
        }

        private IEnumerator Transition(CanvasGroup[] hideGroups, CanvasGroup showGroup, CanvasGroup[] showGroups = null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float outAlpha = 1f - Mathf.Clamp01(t / fadeDuration);
                foreach (var g in hideGroups) if (g != null) g.alpha = outAlpha;
                yield return null;
            }
            foreach (var g in hideGroups) if (g != null) SetGroupState(g, visible: false, instant: true);

            var targets = showGroups ?? new[] { showGroup };
            foreach (var g in targets) if (g != null) SetGroupState(g, visible: true, instant: false);

            t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float inAlpha = Mathf.Clamp01(t / fadeDuration);
                foreach (var g in targets) if (g != null) g.alpha = inAlpha;
                yield return null;
            }
            foreach (var g in targets) if (g != null) { g.alpha = 1f; g.interactable = true; g.blocksRaycasts = true; }
        }

        private static void SetGroupState(CanvasGroup group, bool visible, bool instant)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
