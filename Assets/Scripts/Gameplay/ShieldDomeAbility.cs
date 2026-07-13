using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalGrind.Gameplay
{
    public class ShieldDomeAbility : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Timing")]
        [Tooltip("Cooldown applied after the shield is released, before it can be raised again.")]
        [SerializeField] private float cooldown = 3.5f;

        [Header("Feedback")]
        [SerializeField] private AudioSource shieldLoopAudio;
        [SerializeField] private AudioSource shieldReleaseAudio;
        [SerializeField] private GameObject shieldDomeVfx;
        [SerializeField] private GameObject shieldActiveLabel;
        [Tooltip("Optional. When set, the dome VFX is re-centered on the crowd's formation center each activation, so the bubble covers the whole group instead of sitting wherever it was left in the scene.")]
        [SerializeField] private CrowdFormationController crowdFormation;

        [Header("Commander Glow -> Dome Expansion")]
        [Tooltip("The Commander's neon glow child (hidden by default by CommanderController). While S is held, " +
                 "this same object is shown and scaled up into the visible dome, then hidden instantly on release.")]
        [SerializeField] private GameObject commanderGlow;
        [SerializeField] private float glowExpandScale = 2.4f;
        [SerializeField] private float glowExpandDuration = 0.12f;
        [SerializeField] private Color glowExpandedColor = new Color(0.15f, 0.85f, 1f, 0.85f);

        private Vector3 glowBaseScale = Vector3.one;
        private Color glowBaseColor = Color.white;
        private Coroutine glowRoutine;

        [Header("UI (optional)")]
        [SerializeField] private Button shieldButton;

        [Header("Denied Feedback")]
        [Tooltip("Shake + red flash shown on the button when S is pressed but the ability isn't ready yet, " +
                 "so a mistimed press near a hazard is obvious instead of silently doing nothing.")]
        [SerializeField] private float deniedShakeDuration = 0.25f;
        [SerializeField] private float deniedShakeStrength = 8f;
        [SerializeField] private Color deniedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private bool isShielded;
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
                shieldButtonImage = shieldButton.targetGraphic as Image;
                if (shieldButtonImage != null) shieldButtonNormalColor = shieldButtonImage.color;
                shieldButtonRect = shieldButton.GetComponent<RectTransform>();
                if (shieldButtonRect != null) shieldButtonBasePos = shieldButtonRect.anchoredPosition;
            }
            if (commanderGlow != null)
            {
                glowBaseScale = commanderGlow.transform.localScale;
                var glowSr = commanderGlow.GetComponent<SpriteRenderer>();
                if (glowSr != null) glowBaseColor = glowSr.color;
            }
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (!isShielded && cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            // The shield mechanic is only usable until the survival goal is complete.
            if (shieldButton != null) shieldButton.interactable = !runner.SurvivalComplete;

            if (runner.State != LevelState.Playing || runner.SurvivalComplete)
            {
                if (isShielded) EndShield(runner);
                return;
            }

            bool held = Keyboard.current != null && Keyboard.current.sKey.isPressed;
            bool pressedThisFrame = Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame;

            if (pressedThisFrame && !isShielded)
            {
                if (IsReady) BeginShield(runner);
                else ShowDenied();
            }
            else if (isShielded && !held)
            {
                // S was released -- the dome drops the instant the key comes up.
                EndShield(runner);
            }
        }

        /// Held-button entry point for the on-screen Shield button (mirrors the S key's press/release feel).
        public void OnPointerDown(PointerEventData eventData)
        {
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing || runner.SurvivalComplete) return;

            if (IsReady) BeginShield(runner);
            else ShowDenied();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var runner = LevelRunner.Instance;
            if (runner == null || !isShielded) return;
            EndShield(runner);
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
            runner.SetFrozen(true);

            if (shieldLoopAudio != null) shieldLoopAudio.Play();
            if (shieldDomeVfx != null)
            {
                if (crowdFormation != null) shieldDomeVfx.transform.position = crowdFormation.FormationCenter;
                shieldDomeVfx.SetActive(true);
            }
            if (shieldActiveLabel != null) shieldActiveLabel.SetActive(true);

            if (commanderGlow != null)
            {
                commanderGlow.SetActive(true);
                if (glowRoutine != null) StopCoroutine(glowRoutine);
                glowRoutine = StartCoroutine(ExpandGlow());
            }
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

            if (commanderGlow != null)
            {
                // The dome must vanish the instant S is released -- no fade, no coroutine delay.
                if (glowRoutine != null) StopCoroutine(glowRoutine);
                commanderGlow.transform.localScale = glowBaseScale;
                var glowSr = commanderGlow.GetComponent<SpriteRenderer>();
                if (glowSr != null) glowSr.color = glowBaseColor;
                commanderGlow.SetActive(false);
            }
        }

        private IEnumerator ExpandGlow()
        {
            var glowSr = commanderGlow.GetComponent<SpriteRenderer>();
            Vector3 targetScale = glowBaseScale * glowExpandScale;
            float elapsed = 0f;
            while (elapsed < glowExpandDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / glowExpandDuration;
                commanderGlow.transform.localScale = Vector3.Lerp(glowBaseScale, targetScale, t);
                if (glowSr != null) glowSr.color = Color.Lerp(glowBaseColor, glowExpandedColor, t);
                yield return null;
            }
            commanderGlow.transform.localScale = targetScale;
            if (glowSr != null) glowSr.color = glowExpandedColor;
        }
    }
}
