using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    /// The Shield Dome doubles as the run's "ultimate": besides blocking Barrage hazards while
    /// held, raising it also detonates every bomb and bee near the crowd. That extra power is why
    /// its cooldown starts noticeably longer than Freeze's or Gravity Shift's.
    public class ShieldDomeAbility : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Timing")]
        [Tooltip("Cooldown at the start of the run, applied after the shield is released.")]
        [SerializeField] private float cooldown = 8f;
        [Tooltip("Cooldown once the survival target is reached -- abilities get more spammable the longer you survive.")]
        [SerializeField] private float minCooldown = 4f;

        [Header("Ultimate Blast")]
        [Tooltip("Raising the shield also clears every bomb and bee within this radius of the crowd, instead of only blocking whatever's already touching it.")]
        [SerializeField] private float blastRadius = 10f;
        [SerializeField] private int blastBonusPerTarget = 15;
        [SerializeField] private AudioClip blastSfx;

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
        [Tooltip("Red flash shown on the button when S is pressed but the ability isn't ready yet, " +
                 "so a mistimed press near a hazard is obvious instead of silently doing nothing. " +
                 "Color only -- the button stays fixed in place so a rapid mash never nudges it into a neighbor.")]
        [SerializeField] private float deniedShakeDuration = 0.25f;
        [SerializeField] private Color deniedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private bool isShielded;
        private float cooldownTimer;
        private float currentCooldownDuration;
        private Image shieldButtonImage;
        private Color shieldButtonNormalColor;
        private Coroutine deniedRoutine;

        /// 0 = ready to use, 1 = actively shielded or fully on cooldown.
        public float CooldownFraction => isShielded ? 1f : Mathf.Clamp01(cooldownTimer / Mathf.Max(0.0001f, currentCooldownDuration));
        public bool IsReady => cooldownTimer <= 0f && !isShielded;
        /// Seconds left before this ability can be used again (0 while already ready), so the HUD
        /// can show an actual countdown instead of just a fill bar.
        public float SecondsUntilReady => Mathf.Max(0f, cooldownTimer);

        private void Awake()
        {
            if (shieldButton != null)
            {
                shieldButtonImage = shieldButton.targetGraphic as Image;
                if (shieldButtonImage != null) shieldButtonNormalColor = shieldButtonImage.color;
            }
            if (commanderGlow != null)
            {
                glowBaseScale = commanderGlow.transform.localScale;
                var glowSr = commanderGlow.GetComponent<SpriteRenderer>();
                if (glowSr != null) glowBaseColor = glowSr.color;
            }
        }

        /// Instantly clears the cooldown so the ability is ready again. Used by powerup pickups.
        public void RefillCooldown() => cooldownTimer = 0f;

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
                yield return null;
            }
            if (shieldButtonImage != null) shieldButtonImage.color = shieldButtonNormalColor;
        }

        private void BeginShield(LevelRunner runner)
        {
            isShielded = true;
            runner.SetShieldActive(true);

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

            BlastNearbyHazards(runner);
        }

        /// The "ultimate" half of the ability: detonates every bomb and bee within blastRadius of
        /// the crowd the instant the dome goes up, on top of the dome's normal passive block.
        private void BlastNearbyHazards(LevelRunner runner)
        {
            Vector3 center = crowdFormation != null ? crowdFormation.FormationCenter : transform.position;
            int cleared = 0;

            foreach (var bomb in FindObjectsByType<TargetedBomb>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(bomb.transform.position, center) <= blastRadius)
                {
                    Destroy(bomb.gameObject);
                    cleared++;
                }
            }

            foreach (var bee in FindObjectsByType<ChasingBee>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(bee.transform.position, center) <= blastRadius)
                {
                    Destroy(bee.gameObject);
                    cleared++;
                }
            }

            if (cleared > 0)
            {
                runner.RegisterBonus(cleared * blastBonusPerTarget, "ULTIMATE BLAST");
                AudioManager.PlaySfxAt(blastSfx, center);
            }
        }

        private void EndShield(LevelRunner runner)
        {
            isShielded = false;
            currentCooldownDuration = Mathf.Lerp(cooldown, minCooldown, runner.DifficultyRamp01);
            cooldownTimer = currentCooldownDuration;
            runner.SetShieldActive(false);

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
