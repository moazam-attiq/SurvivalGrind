using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalGrind.Gameplay
{
    public class FreezeAbility : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float freezeDuration = 1.3f;
        [SerializeField] private float cooldown = 3.5f;

        [Header("Feedback")]
        [SerializeField] private AudioSource freezeLoopAudio;
        [SerializeField] private AudioSource freezeReleaseAudio;
        [SerializeField] private GameObject shieldDomeVfx;
        [SerializeField] private GameObject freezeActiveLabel;

        [Header("UI (optional)")]
        [SerializeField] private Button freezeButton;

        [Header("Denied Feedback")]
        [Tooltip("Shake + red flash shown on the button when F is pressed but the ability isn't ready yet, " +
                 "so a mistimed press near a hazard is obvious instead of silently doing nothing.")]
        [SerializeField] private float deniedShakeDuration = 0.25f;
        [SerializeField] private float deniedShakeStrength = 8f;
        [SerializeField] private Color deniedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private bool isFrozenByAbility;
        private float freezeTimer;
        private float cooldownTimer;
        private Image freezeButtonImage;
        private RectTransform freezeButtonRect;
        private Color freezeButtonNormalColor;
        private Vector2 freezeButtonBasePos;
        private Coroutine deniedRoutine;

        /// 0 = ready to use, 1 = actively frozen or fully on cooldown. While frozen this counts UP as
        /// freezeTimer burns down (so the HUD fill drains to show time remaining, hitting 1 exactly
        /// when the freeze ends), then counts back down over the cooldown as usual.
        public float CooldownFraction => isFrozenByAbility
            ? Mathf.Clamp01(1f - freezeTimer / freezeDuration)
            : Mathf.Clamp01(cooldownTimer / cooldown);
        public bool IsReady => cooldownTimer <= 0f && !isFrozenByAbility;

        private void Awake()
        {
            if (freezeButton != null)
            {
                freezeButton.onClick.AddListener(TryActivate);
                freezeButtonImage = freezeButton.targetGraphic as Image;
                if (freezeButtonImage != null) freezeButtonNormalColor = freezeButtonImage.color;
                freezeButtonRect = freezeButton.GetComponent<RectTransform>();
                if (freezeButtonRect != null) freezeButtonBasePos = freezeButtonRect.anchoredPosition;
            }
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (isFrozenByAbility)
            {
                freezeTimer -= Time.deltaTime;
                if (freezeTimer <= 0f) EndFreeze(runner);
            }
            else if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            // The freeze mechanic is only usable until the survival goal is complete.
            if (freezeButton != null) freezeButton.interactable = !runner.SurvivalComplete;

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

            if (isFrozenByAbility)
            {
                // Pressing F again while still frozen cancels the shield early --
                // the cooldown circle resets to empty and starts refilling from 0 right away,
                // instead of waiting out the rest of the freeze duration first.
                EndFreeze(runner);
                return;
            }

            if (!IsReady)
            {
                ShowDenied();
                return;
            }
            BeginFreeze(runner);
        }

        private void ShowDenied()
        {
            if (freezeButton == null) return;
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
                if (freezeButtonImage != null) freezeButtonImage.color = Color.Lerp(deniedFlashColor, freezeButtonNormalColor, t);
                if (freezeButtonRect != null)
                {
                    float shake = Mathf.Sin(t * Mathf.PI * 8f) * deniedShakeStrength * (1f - t);
                    freezeButtonRect.anchoredPosition = freezeButtonBasePos + new Vector2(shake, 0f);
                }
                yield return null;
            }
            if (freezeButtonImage != null) freezeButtonImage.color = freezeButtonNormalColor;
            if (freezeButtonRect != null) freezeButtonRect.anchoredPosition = freezeButtonBasePos;
        }

        private void BeginFreeze(LevelRunner runner)
        {
            isFrozenByAbility = true;
            freezeTimer = freezeDuration;
            runner.SetFreezeActive(true);

            if (freezeLoopAudio != null) freezeLoopAudio.Play();
            if (shieldDomeVfx != null) shieldDomeVfx.SetActive(true);
            if (freezeActiveLabel != null) freezeActiveLabel.SetActive(true);
        }

        private void EndFreeze(LevelRunner runner)
        {
            isFrozenByAbility = false;
            cooldownTimer = cooldown;
            runner.SetFreezeActive(false);

            if (freezeLoopAudio != null) freezeLoopAudio.Stop();
            if (freezeReleaseAudio != null) freezeReleaseAudio.Play();
            if (shieldDomeVfx != null) shieldDomeVfx.SetActive(false);
            if (freezeActiveLabel != null) freezeActiveLabel.SetActive(false);
        }
    }
}
