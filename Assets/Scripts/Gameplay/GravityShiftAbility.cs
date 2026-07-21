using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalGrind.Gameplay
{
    /// Gravity Shift (G). A timed immunity PULSE that clears ONLY gravity hazards - gaps, moving
    /// platforms, broken roads and moving barriers. It does NOT move the Commander vertically:
    /// up/down lane movement is owned by CommanderController's 3-lane control, so this ability and
    /// free lane movement coexist. Freeze and Shield do nothing against gravity hazards, and Gravity
    /// Shift does nothing against lasers/bombs/bees - strict independent logic.
    public class GravityShiftAbility : MonoBehaviour, IPointerDownHandler
    {
        [Header("Timing")]
        [Tooltip("Seconds the gravity-immunity window stays open after pressing G.")]
        [SerializeField] private float shiftDuration = 1.1f;
        [Tooltip("Cooldown at the start of the run.")]
        [SerializeField] private float cooldown = 3f;
        [Tooltip("Cooldown once the survival target is reached -- abilities get more spammable the longer you survive.")]
        [SerializeField] private float minCooldown = 1.5f;

        [Header("Feedback")]
        [SerializeField] private AudioSource flipAudio;
        [SerializeField] private GameObject gravityShiftVfx;
        [SerializeField] private GameObject gravityShiftActiveLabel;
        [SerializeField] private CrowdFormationController crowdFormation;

        [Header("Commander Glow")]
        [Tooltip("The Commander's glow child (hidden by default). Shown while the shift window is open.")]
        [SerializeField] private GameObject commanderGlow;
        [SerializeField] private float glowScale = 2.4f;
        [SerializeField] private Color glowColor = new Color(1f, 0.6f, 0.2f, 0.85f);

        [Header("UI (optional)")]
        [SerializeField] private Button shiftButton;

        [Header("Denied Feedback")]
        [Tooltip("Red flash only -- the button stays fixed in place so a rapid mash never nudges it into a neighbor.")]
        [SerializeField] private float deniedShakeDuration = 0.25f;
        [SerializeField] private Color deniedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private bool isActive;
        private float activeTimer;
        private float cooldownTimer;
        private float currentCooldownDuration;
        private Vector3 glowBaseScale = Vector3.one;
        private Color glowBaseColor = Color.white;
        private Image shiftButtonImage;
        private Color shiftButtonNormalColor;
        private Coroutine deniedRoutine;

        /// 0 = ready, 1 = active or fully cooling down. Drives the HUD ability fill. While active this
        /// counts UP as activeTimer burns down, so the fill drains to show time remaining instead of
        /// sitting frozen at empty for the whole active window.
        public float CooldownFraction => isActive
            ? Mathf.Clamp01(1f - activeTimer / Mathf.Max(0.0001f, shiftDuration))
            : Mathf.Clamp01(cooldownTimer / Mathf.Max(0.0001f, currentCooldownDuration));
        public bool IsReady => !isActive && cooldownTimer <= 0f;
        /// Seconds left before this ability can be used again (0 while already ready), so the HUD
        /// can show an actual countdown instead of just a fill bar.
        public float SecondsUntilReady => isActive ? activeTimer : Mathf.Max(0f, cooldownTimer);

        private void Awake()
        {
            if (shiftButton != null)
            {
                shiftButton.onClick.AddListener(TryFlip);
                shiftButtonImage = shiftButton.targetGraphic as Image;
                if (shiftButtonImage != null) shiftButtonNormalColor = shiftButtonImage.color;
            }
            if (commanderGlow != null)
            {
                glowBaseScale = commanderGlow.transform.localScale;
                var sr = commanderGlow.GetComponent<SpriteRenderer>();
                if (sr != null) glowBaseColor = sr.color;
            }
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (isActive)
            {
                activeTimer -= Time.deltaTime;
                if (activeTimer <= 0f) EndShift(runner);
            }
            else if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (shiftButton != null) shiftButton.interactable = !runner.SurvivalComplete;

            if (runner.State != LevelState.Playing || runner.SurvivalComplete) return;

            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                if (IsReady) BeginShift(runner);
                else ShowDenied();
            }
        }

        /// Instantly clears the cooldown so the ability is ready again. Used by powerup pickups.
        public void RefillCooldown() => cooldownTimer = 0f;

        public void OnPointerDown(PointerEventData eventData) => TryFlip();

        public void TryFlip()
        {
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing || runner.SurvivalComplete) return;
            if (IsReady) BeginShift(runner);
            else ShowDenied();
        }

        private void BeginShift(LevelRunner runner)
        {
            isActive = true;
            activeTimer = shiftDuration;
            runner.SetGravityActive(true);

            if (flipAudio != null) flipAudio.Play();
            if (gravityShiftVfx != null)
            {
                if (crowdFormation != null) gravityShiftVfx.transform.position = crowdFormation.FormationCenter;
                gravityShiftVfx.SetActive(true);
            }
            if (gravityShiftActiveLabel != null) gravityShiftActiveLabel.SetActive(true);
            if (commanderGlow != null)
            {
                var sr = commanderGlow.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = glowColor;
                commanderGlow.transform.localScale = glowBaseScale * glowScale;
                commanderGlow.SetActive(true);
            }
        }

        private void EndShift(LevelRunner runner)
        {
            isActive = false;
            currentCooldownDuration = Mathf.Lerp(cooldown, minCooldown, runner.DifficultyRamp01);
            cooldownTimer = currentCooldownDuration;
            runner.SetGravityActive(false);

            if (gravityShiftVfx != null) gravityShiftVfx.SetActive(false);
            if (gravityShiftActiveLabel != null) gravityShiftActiveLabel.SetActive(false);
            if (commanderGlow != null)
            {
                commanderGlow.transform.localScale = glowBaseScale;
                var sr = commanderGlow.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = glowBaseColor;
                commanderGlow.SetActive(false);
            }
        }

        private void ShowDenied()
        {
            if (shiftButton == null) return;
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
                if (shiftButtonImage != null) shiftButtonImage.color = Color.Lerp(deniedFlashColor, shiftButtonNormalColor, t);
                yield return null;
            }
            if (shiftButtonImage != null) shiftButtonImage.color = shiftButtonNormalColor;
        }
    }
}
