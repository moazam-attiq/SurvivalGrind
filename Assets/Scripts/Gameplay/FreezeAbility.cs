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
        [Tooltip("Cooldown at the start of the run.")]
        [SerializeField] private float cooldown = 3.5f;
        [Tooltip("Cooldown once the survival target is reached -- abilities get more spammable the longer you survive.")]
        [SerializeField] private float minCooldown = 1.75f;

        [Header("Feedback")]
        [SerializeField] private AudioSource freezeLoopAudio;
        [SerializeField] private AudioSource freezeReleaseAudio;
        [SerializeField] private GameObject shieldDomeVfx;
        [SerializeField] private GameObject freezeActiveLabel;

        [Header("UI (optional)")]
        [SerializeField] private Button freezeButton;

        [Header("Denied Feedback")]
        [Tooltip("Red flash shown on the button when F is pressed but the ability isn't ready yet, " +
                 "so a mistimed press near a hazard is obvious instead of silently doing nothing. " +
                 "Color only -- the button stays fixed in place so a rapid mash never nudges it into a neighbor.")]
        [SerializeField] private float deniedShakeDuration = 0.25f;
        [SerializeField] private Color deniedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private bool isFrozenByAbility;
        private float freezeTimer;
        private float cooldownTimer;
        private float currentCooldownDuration;
        private Image freezeButtonImage;
        private Color freezeButtonNormalColor;
        private Coroutine deniedRoutine;

        /// 0 = ready to use, 1 = actively frozen or fully on cooldown. While frozen this counts UP as
        /// freezeTimer burns down (so the HUD fill drains to show time remaining, hitting 1 exactly
        /// when the freeze ends), then counts back down over the cooldown as usual.
        public float CooldownFraction => isFrozenByAbility
            ? Mathf.Clamp01(1f - freezeTimer / freezeDuration)
            : Mathf.Clamp01(cooldownTimer / Mathf.Max(0.0001f, currentCooldownDuration));
        public bool IsReady => cooldownTimer <= 0f && !isFrozenByAbility;
        /// Seconds left before this ability can be used again (0 while already ready), so the HUD
        /// can show an actual countdown instead of just a fill bar.
        public float SecondsUntilReady => isFrozenByAbility ? freezeTimer : Mathf.Max(0f, cooldownTimer);

        private void Awake()
        {
            if (freezeButton != null)
            {
                freezeButton.onClick.AddListener(TryActivate);
                freezeButtonImage = freezeButton.targetGraphic as Image;
                if (freezeButtonImage != null) freezeButtonNormalColor = freezeButtonImage.color;
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

        /// Instantly clears the cooldown so the ability is ready again. Used by powerup pickups.
        public void RefillCooldown() => cooldownTimer = 0f;

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
                yield return null;
            }
            if (freezeButtonImage != null) freezeButtonImage.color = freezeButtonNormalColor;
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
            currentCooldownDuration = Mathf.Lerp(cooldown, minCooldown, runner.DifficultyRamp01);
            cooldownTimer = currentCooldownDuration;
            runner.SetFreezeActive(false);

            if (freezeLoopAudio != null) freezeLoopAudio.Stop();
            if (freezeReleaseAudio != null) freezeReleaseAudio.Play();
            if (shieldDomeVfx != null) shieldDomeVfx.SetActive(false);
            if (freezeActiveLabel != null) freezeActiveLabel.SetActive(false);
        }
    }
}
