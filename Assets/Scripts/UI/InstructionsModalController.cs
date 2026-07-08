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
        }

        private void OnOk()
        {
            if (modalPanel != null) modalPanel.SetActive(false);
            AudioManager.PlayUiClick();

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
