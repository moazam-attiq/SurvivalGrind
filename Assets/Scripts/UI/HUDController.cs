using System.Collections;
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
        [Tooltip("Optional. Reuses the button's own name label to show a live countdown ('1.4s') while on cooldown, reverting to its original text (e.g. 'FREEZE') the instant it's ready.")]
        [SerializeField] private TMP_Text freezeCooldownText;

        [Header("Shield Dome Ability (optional)")]
        [SerializeField] private ShieldDomeAbility shieldDomeAbility;
        [SerializeField] private Image shieldDomeCooldownFill;
        [SerializeField] private TMP_Text shieldCooldownText;

        [Header("Gravity Shift Ability (optional)")]
        [SerializeField] private GravityShiftAbility gravityShiftAbility;
        [SerializeField] private Image gravityShiftCooldownFill;
        [SerializeField] private TMP_Text gravityCooldownText;

        [Header("Notifications")]
        [Tooltip("Optional. Shows every score/hazard event (SAFE PASS, FROZE THE THREAT, CROWD LOST, ...). Auto-created above the score text if left empty.")]
        [SerializeField] private TMP_Text notificationText;
        [SerializeField] private float notificationDuration = 1.3f;
        [SerializeField] private Color notificationGainColor = new Color(0.4f, 1f, 0.55f);
        [SerializeField] private Color notificationLossColor = new Color(1f, 0.35f, 0.35f);

        [Header("Ability Prompt")]
        [Tooltip("How much each ability icon scales up when its matching hazard starts telegraphing, so 'use it now' is obvious.")]
        [SerializeField] private float promptPulseScale = 1.35f;
        [SerializeField] private float promptPulseDuration = 0.35f;

        private LevelRunner runner;
        private Coroutine notificationRoutine;
        private Coroutine freezePromptRoutine;
        private Coroutine shieldPromptRoutine;
        private Coroutine gravityPromptRoutine;
        private string freezeReadyLabel;
        private string shieldReadyLabel;
        private string gravityReadyLabel;

        private void Start()
        {
            runner = LevelRunner.Instance;
            if (runner == null) return;

            // Capture each button's own name text so the countdown can revert to it once ready,
            // instead of hard-coding "FREEZE"/"SHIELD"/"GRAVITY SHIFT" here.
            if (freezeCooldownText != null) freezeReadyLabel = freezeCooldownText.text;
            if (shieldCooldownText != null) shieldReadyLabel = shieldCooldownText.text;
            if (gravityCooldownText != null) gravityReadyLabel = gravityCooldownText.text;

            runner.OnScoreChanged += HandleScoreChanged;
            runner.OnAliveChanged += HandleAliveChanged;
            runner.OnTimeChanged += HandleTimeChanged;
            runner.OnNotification += HandleNotification;
            runner.OnHazardWarning += HandleHazardWarning;

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
            runner.OnNotification -= HandleNotification;
            runner.OnHazardWarning -= HandleHazardWarning;
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

            UpdateCooldownLabel(freezeAbility != null ? freezeAbility.SecondsUntilReady : (float?)null, freezeCooldownText, freezeReadyLabel);
            UpdateCooldownLabel(shieldDomeAbility != null ? shieldDomeAbility.SecondsUntilReady : (float?)null, shieldCooldownText, shieldReadyLabel);
            UpdateCooldownLabel(gravityShiftAbility != null ? gravityShiftAbility.SecondsUntilReady : (float?)null, gravityCooldownText, gravityReadyLabel);
        }

        private static void UpdateCooldownLabel(float? secondsRemaining, TMP_Text label, string readyText)
        {
            if (label == null || secondsRemaining == null) return;
            label.text = secondsRemaining.Value > 0.05f
                ? secondsRemaining.Value.ToString("0.0") + "s"
                : readyText;
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

        private void HandleNotification(string message)
        {
            EnsureNotificationText();
            if (notificationText == null) return;

            bool isLoss = message.Contains("-") || message.Contains("LOST") || message.Contains("DOWN") || message.Contains("WIPED");
            notificationText.color = isLoss ? notificationLossColor : notificationGainColor;
            notificationText.text = message;

            if (notificationRoutine != null) StopCoroutine(notificationRoutine);
            notificationRoutine = StartCoroutine(PlayNotification());
        }

        private IEnumerator PlayNotification()
        {
            var rt = (RectTransform)notificationText.transform;
            Vector2 basePos = rt.anchoredPosition;
            notificationText.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < notificationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / notificationDuration;
                rt.anchoredPosition = basePos + Vector2.up * (24f * t);
                var c = notificationText.color;
                c.a = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                notificationText.color = c;
                yield return null;
            }

            notificationText.gameObject.SetActive(false);
            rt.anchoredPosition = basePos;
        }

        // Auto-creates a simple floating text under this HUD if no notificationText was wired up in
        // the Inspector, so the score/hazard feedback works out of the box on every level's HUD.
        private void EnsureNotificationText()
        {
            if (notificationText != null) return;

            var go = new GameObject("NotificationText", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.8f);
            rt.anchorMax = new Vector2(0.5f, 0.8f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(700f, 90f);

            notificationText = go.AddComponent<TextMeshProUGUI>();
            notificationText.alignment = TextAlignmentOptions.Center;
            notificationText.fontSize = 44;
            notificationText.fontStyle = FontStyles.Bold;
            notificationText.raycastTarget = false;
            go.SetActive(false);
        }

        private void HandleHazardWarning(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Laser:
                    if (freezeCooldownFill != null) PulseIcon(freezeCooldownFill.transform, ref freezePromptRoutine);
                    break;
                case HazardKind.Gravity:
                    if (gravityShiftCooldownFill != null) PulseIcon(gravityShiftCooldownFill.transform, ref gravityPromptRoutine);
                    break;
                case HazardKind.Barrage:
                    if (shieldDomeCooldownFill != null) PulseIcon(shieldDomeCooldownFill.transform, ref shieldPromptRoutine);
                    break;
            }
        }

        private void PulseIcon(Transform icon, ref Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PulseIconRoutine(icon));
        }

        private IEnumerator PulseIconRoutine(Transform icon)
        {
            Vector3 baseScale = icon.localScale;
            Vector3 peakScale = baseScale * promptPulseScale;
            float elapsed = 0f;
            while (elapsed < promptPulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / promptPulseDuration;
                // Up fast, back down slower -- reads as a sharp "look here" pop rather than a smooth bounce.
                float curve = t < 0.35f ? (t / 0.35f) : 1f - (t - 0.35f) / 0.65f;
                icon.localScale = Vector3.LerpUnclamped(baseScale, peakScale, curve);
                yield return null;
            }
            icon.localScale = baseScale;
        }
    }
}
