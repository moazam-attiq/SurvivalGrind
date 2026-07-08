using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SurvivalGrind.Gameplay;

namespace SurvivalGrind.UI
{
    public class GameOverController : MonoBehaviour
    {
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);
        }

        private void Retry()
        {
            SceneManager.LoadScene(LevelRunner.LastPlayedLevelScene);
        }

        private void GoHome()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
