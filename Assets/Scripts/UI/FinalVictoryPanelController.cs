using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SurvivalGrind.Gameplay;

namespace SurvivalGrind.UI
{
    /// <summary>
    /// The Final Victory / congratulations screen shown after the last level (Level 4) is completed.
    /// Mirrors <see cref="VictoryPanelController"/> but adds a congratulatory headline and Home / Play Again
    /// buttons, and reports the actual total run time rather than the level's target time.
    /// </summary>
    public class FinalVictoryPanelController : MonoBehaviour
    {
        [Header("Message")]
        [SerializeField] private TMP_Text headlineText;
        [SerializeField] private TMP_Text messageText;

        [Header("Stats")]
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text survivorsStatText;
        [SerializeField] private TMP_Text timeStatText;

        [Header("Buttons")]
        [SerializeField] private Button homeButton;
        [SerializeField] private Button playAgainButton;

        [Header("Copy")]
        [SerializeField] private string headline = "CONGRATULATIONS!";
        [TextArea]
        [SerializeField] private string message =
            "YOU COMPLETED SURVIVAL GRIND.\nYou conquered every trial and mastered all abilities!";

        private void Awake()
        {
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);
            if (playAgainButton != null) playAgainButton.onClick.AddListener(PlayAgain);
        }

        private void OnEnable()
        {
            if (headlineText != null) headlineText.text = headline;
            if (messageText != null) messageText.text = message;

            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (finalScoreText != null) finalScoreText.text = $"SCORE: {runner.Score}";
            if (survivorsStatText != null)
                survivorsStatText.text = $"SURVIVORS: {runner.AliveCount}/{runner.StartingCrowdCount}";
            if (timeStatText != null)
            {
                // Total time actually taken to finish the run.
                float total = Mathf.Max(0f, runner.SurvivalTime);
                int minutes = Mathf.FloorToInt(total / 60f);
                int seconds = Mathf.FloorToInt(total % 60f);
                timeStatText.text = $"TIME: {minutes:00}:{seconds:00}";
            }
        }

        private void GoHome()
        {
            AudioManager.PlayUiClick();
            var runner = LevelRunner.Instance;
            if (runner != null) runner.GoHome();
            else
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }

        private void PlayAgain()
        {
            AudioManager.PlayUiClick();
            var runner = LevelRunner.Instance;
            if (runner != null) runner.RestartLevel();
            else
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }
    }
}
