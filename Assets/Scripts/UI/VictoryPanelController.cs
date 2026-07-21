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
        [Tooltip("Optional. Explains whether this win's score was enough to unlock the next trial, since Victory itself doesn't guarantee that.")]
        [SerializeField] private TMP_Text unlockStatusText;
        [SerializeField] private Color unlockedColor = new Color(0.4f, 1f, 0.55f);
        [SerializeField] private Color notUnlockedColor = new Color(1f, 0.75f, 0.3f);

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
            if (unlockStatusText != null)
            {
                if (runner.NextLevelUnlocked)
                {
                    unlockStatusText.text = "NEXT TRIAL UNLOCKED!";
                    unlockStatusText.color = unlockedColor;
                }
                else
                {
                    unlockStatusText.text = $"SCORE {runner.UnlockScoreThreshold}+ TO UNLOCK NEXT TRIAL (YOU GOT {runner.Score})";
                    unlockStatusText.color = notUnlockedColor;
                }
            }
        }
    }
}
