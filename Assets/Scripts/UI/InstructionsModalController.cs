using UnityEngine;
using UnityEngine.UI;
using SurvivalGrind.Gameplay;

namespace SurvivalGrind.UI
{
    public class InstructionsModalController : MonoBehaviour
    {
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private Button okButton;
        [SerializeField] private Button reopenButton;

        [Tooltip("Freeze the scene (Time.timeScale = 0) while the start-of-level instructions panel is open, " +
                 "so hazard machines/bombs/enemies behind the panel don't run before the player dismisses it.")]
        [SerializeField] private bool pauseGameWhileOpen = true;

        private bool hasStarted;

        private void Awake()
        {
            if (okButton != null) okButton.onClick.AddListener(OnOk);
            if (reopenButton != null) reopenButton.onClick.AddListener(Reopen);
            if (modalPanel != null) modalPanel.SetActive(false);
        }

        public void ShowForGameplayStart()
        {
            if (modalPanel != null) modalPanel.SetActive(true);
            // Freeze everything behind the panel (turret sweeps, lobbed bombs, enemies) until the player hits OK.
            if (pauseGameWhileOpen) Time.timeScale = 0f;
        }

        private void OnOk()
        {
            if (modalPanel != null) modalPanel.SetActive(false);
            AudioManager.PlayUiClick();

            // Always restore normal time flow when leaving the panel. (The reopened/paused path
            // also calls Resume() below, which re-sets timeScale to 1 -- harmless to do both.)
            if (pauseGameWhileOpen) Time.timeScale = 1f;

            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (!hasStarted)
            {
                hasStarted = true;
                runner.BeginGameplay();
            }
            else if (runner.State == LevelState.Paused)
            {
                runner.Resume();
                AudioManager.PlayResume();
            }
        }

        private void Reopen()
        {
            var runner = LevelRunner.Instance;
            if (runner == null || !hasStarted) return;

            if (runner.State == LevelState.Playing) runner.Pause();
            if (modalPanel != null) modalPanel.SetActive(true);
            AudioManager.PlayPause();
        }
    }
}
