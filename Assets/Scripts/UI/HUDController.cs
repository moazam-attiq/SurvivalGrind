using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SurvivalGrind.Gameplay;

namespace SurvivalGrind.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text aliveText;
        [SerializeField] private TMP_Text timeText;

        [Header("Bars")]
        [SerializeField] private Image survivalTimerBar;

        [Header("Freeze Ability (optional)")]
        [SerializeField] private FreezeAbility freezeAbility;
        [SerializeField] private Image freezeCooldownFill;

        [Header("Shield Dome Ability (optional)")]
        [SerializeField] private ShieldDomeAbility shieldDomeAbility;
        [SerializeField] private Image shieldDomeCooldownFill;

        [Header("Gravity Shift Ability (optional)")]
        [SerializeField] private GravityShiftAbility gravityShiftAbility;
        [SerializeField] private Image gravityShiftCooldownFill;

        private LevelRunner runner;

        private void Start()
        {
            runner = LevelRunner.Instance;
            if (runner == null) return;

            runner.OnScoreChanged += HandleScoreChanged;
            runner.OnAliveChanged += HandleAliveChanged;
            runner.OnTimeChanged += HandleTimeChanged;

            HandleScoreChanged(runner.Score);
            HandleAliveChanged(runner.AliveCount);
            HandleTimeChanged(runner.SurvivalTime, runner.SurvivalTarget);
        }

        private void OnDestroy()
        {
            if (runner == null) return;
            runner.OnScoreChanged -= HandleScoreChanged;
            runner.OnAliveChanged -= HandleAliveChanged;
            runner.OnTimeChanged -= HandleTimeChanged;
        }

        private void Update()
        {
            if (freezeAbility != null && freezeCooldownFill != null)
            {
                freezeCooldownFill.fillAmount = 1f - freezeAbility.CooldownFraction;
            }
            if (shieldDomeAbility != null && shieldDomeCooldownFill != null)
            {
                shieldDomeCooldownFill.fillAmount = 1f - shieldDomeAbility.CooldownFraction;
            }
            if (gravityShiftAbility != null && gravityShiftCooldownFill != null)
            {
                gravityShiftCooldownFill.fillAmount = 1f - gravityShiftAbility.CooldownFraction;
            }
        }

        private void HandleScoreChanged(int score)
        {
            if (scoreText != null) scoreText.text = "SCORE: " + score;
        }

        private void HandleAliveChanged(int alive)
        {
            if (aliveText != null) aliveText.text = "ALIVE: " + alive;
        }

        private void HandleTimeChanged(float current, float target)
        {
            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(current / 60f);
                int seconds = Mathf.FloorToInt(current % 60f);
                timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
            if (survivalTimerBar != null)
            {
                survivalTimerBar.fillAmount = target > 0f ? Mathf.Clamp01(current / target) : 0f;
            }
        }
    }
}
