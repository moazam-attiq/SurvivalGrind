using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalGrind.Gameplay
{
    public class ShieldDomeAbility : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float shieldDuration = 1.3f;
        [SerializeField] private float cooldown = 3.5f;

        [Header("Feedback")]
        [SerializeField] private AudioSource shieldLoopAudio;
        [SerializeField] private AudioSource shieldReleaseAudio;
        [SerializeField] private GameObject shieldDomeVfx;
        [SerializeField] private GameObject shieldActiveLabel;
        [Tooltip("Optional. When set, the dome VFX is re-centered on the crowd's formation center each activation, so the bubble covers the whole group instead of sitting wherever it was left in the scene.")]
        [SerializeField] private CrowdFormationController crowdFormation;

        [Header("UI (optional)")]
        [SerializeField] private Button shieldButton;

        [Header("Denied Feedback")]
        [Tooltip("Shake + red flash shown on the button when F is pressed but the ability isn't ready yet, " +
                 "so a mistimed press near a hazard is obvious instead of silently doing nothing.")]
        [SerializeField] private float deniedShakeDuration = 0.25f;
        [SerializeField] private float deniedShakeStrength = 8f;
        [SerializeField] private Color deniedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private bool isShielded;
        private float shieldTimer;
        private float cooldownTimer;
        private Image shieldButtonImage;
        private RectTransform shieldButtonRect;
        private Color shieldButtonNormalColor;
        private Vector2 shieldButtonBasePos;
        private Coroutine deniedRoutine;

        /// 0 = ready to use, 1 = actively shielded or fully on cooldown.
        public float CooldownFraction => isShielded ? 1f : Mathf.Clamp01(cooldownTimer / cooldown);
        public bool IsReady => cooldownTimer <= 0f && !isShielded;

        private void Awake()
        {
            if (shieldButton != null)
            {
                shieldButton.onClick.AddListener(TryActivate);
                shieldButtonImage = shieldButton.targetGraphic as Image;
                if (shieldButtonImage != null) shieldButtonNormalColor = shieldButtonImage.color;
                shieldButtonRect = shieldButton.GetComponent<RectTransform>();
                if (shieldButtonRect != null) shieldButtonBasePos = shieldButtonRect.anchoredPosition;
            }
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (isShielded)
            {
                shieldTimer -= Time.deltaTime;
                if (shieldTimer <= 0f) EndShield(runner);
            }
            else if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            // The shield mechanic is only usable until the survival goal is complete.
            if (shieldButton != null) shieldButton.interactable = !runner.SurvivalComplete;

            if (runner.State != LevelState.Playing) return;

            bool pressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            if (pressed) TryActivate();
        }

        public void TryActivate()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;
            if (runner.State != LevelState.Playing) return;
            if (runner.SurvivalComplete) return;

            if (isShielded)
            {
                // Pressing F again while still shielded drops the dome early --
                // the cooldown circle resets to empty and starts refilling from 0 right away,
                // instead of waiting out the rest of the shield duration first.
                EndShield(runner);
                return;
            }

            if (!IsReady)
            {
                ShowDenied();
                return;
            }
            BeginShield(runner);
        }

        private void ShowDenied()
        {
            if (shieldButton == null) return;
            if (deniedRoutine != null) StopCoroutine(deniedRoutine);
            deniedRoutine = StartCoroutine(DeniedFeedback());
        }

        private IEnumerator DeniedFeedback()
        {
            float elapsed = 0f;
            while (elapsed < deniedShakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / deniedShakeDuration;
                if (shieldButtonImage != null) shieldButtonImage.color = Color.Lerp(deniedFlashColor, shieldButtonNormalColor, t);
                if (shieldButtonRect != null)
                {
                    float shake = Mathf.Sin(t * Mathf.PI * 8f) * deniedShakeStrength * (1f - t);
                    shieldButtonRect.anchoredPosition = shieldButtonBasePos + new Vector2(shake, 0f);
                }
                yield return null;
            }
            if (shieldButtonImage != null) shieldButtonImage.color = shieldButtonNormalColor;
            if (shieldButtonRect != null) shieldButtonRect.anchoredPosition = shieldButtonBasePos;
        }

        private void BeginShield(LevelRunner runner)
        {
            isShielded = true;
            shieldTimer = shieldDuration;
            runner.SetFrozen(true);

            if (shieldLoopAudio != null) shieldLoopAudio.Play();
            if (shieldDomeVfx != null)
            {
                if (crowdFormation != null) shieldDomeVfx.transform.position = crowdFormation.FormationCenter;
                shieldDomeVfx.SetActive(true);
            }
            if (shieldActiveLabel != null) shieldActiveLabel.SetActive(true);
        }

        private void EndShield(LevelRunner runner)
        {
            isShielded = false;
            cooldownTimer = cooldown;
            runner.SetFrozen(false);

            if (shieldLoopAudio != null) shieldLoopAudio.Stop();
            if (shieldReleaseAudio != null) shieldReleaseAudio.Play();
            if (shieldDomeVfx != null) shieldDomeVfx.SetActive(false);
            if (shieldActiveLabel != null) shieldActiveLabel.SetActive(false);
        }
    }
}
