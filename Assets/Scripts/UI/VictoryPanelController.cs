using TMPro;
using UnityEngine;
using SurvivalGrind.Gameplay;

namespace SurvivalGrind.UI
{
    public class VictoryPanelController : MonoBehaviour
    {
        [SerializeField] private TMP_Text finalScoreText;
        [SerializeField] private TMP_Text survivorsStatText;
        [SerializeField] private TMP_Text timeStatText;

        private void OnEnable()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (finalScoreText != null) finalScoreText.text = $"SCORE: {runner.Score}";
            if (survivorsStatText != null) survivorsStatText.text = $"SURVIVORS: {runner.AliveCount}/{runner.StartingCrowdCount}";
            if (timeStatText != null)
            {
                int minutes = Mathf.FloorToInt(runner.SurvivalTarget / 60f);
                int seconds = Mathf.FloorToInt(runner.SurvivalTarget % 60f);
                timeStatText.text = $"TIME: {minutes:00}:{seconds:00}";
            }
        }
    }
}
