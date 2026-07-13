using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SurvivalGrind.Gameplay
{
    /// Level 2's gravity-flip mechanic. The Commander (and the whole crowd, which follows) rides either the
    /// floor or the ceiling road. Tapping G flips gravity: the group smoothly crosses to the opposite surface,
    /// invulnerable during the crossing, so hazards sitting on a surface are avoided by flipping off it.
    /// This component OWNS the Commander's vertical position -- CommanderController must have relinquishVertical=true.
    public class GravityShiftAbility : MonoBehaviour, IPointerDownHandler
    {
        [Header("Surfaces")]
        [Tooltip("Rest Y when gravity pulls the crowd down onto the floor road.")]
        [SerializeField] private float floorY = -3.5f;
        [Tooltip("Rest Y when gravity is flipped and the crowd rides the ceiling road.")]
        [SerializeField] private float ceilingY = 3.5f;

        [Header("Flip Feel")]
        [Tooltip("Seconds to cross from one surface to the other.")]
        [SerializeField] private float flipDuration = 0.28f;
        [Tooltip("Small delay after landing before another flip is allowed.")]
        [SerializeField] private float flipCooldown = 0.22f;

        [Header("Feedback")]
        [SerializeField] private AudioSource flipAudio;
        [SerializeField] private GameObject gravityShiftVfx;
        [SerializeField] private GameObject gravityShiftActiveLabel;
        [SerializeField] private CrowdFormationController crowdFormation;

        [Header("Commander Glow")]
        [Tooltip("The Commander's glow child (hidden by default). Shown + expanded while crossing.")]
        [SerializeField] private GameObject commanderGlow;
        [SerializeField] private float glowScale = 2.6f;
        [SerializeField] private Color glowColor = new Color(0.9f, 0.2f, 1f, 0.85f);

        [Header("UI (optional)")]
        [SerializeField] private Button shiftButton;

        private Transform leader;
        private bool gravityDown = true;   // true = resting on floor, false = resting on ceiling
        private bool flipping;
        private float flipT;
        private float flipFrom, flipTo;
        private float cooldownTimer;
        private Vector3 glowBaseScale = Vector3.one;
        private Color glowBaseColor = Color.white;

        /// 0 = ready to flip, 1 = mid-flip or cooling down. Drives the HUD ability fill.
        public float CooldownFraction => flipping ? 1f : Mathf.Clamp01(cooldownTimer / Mathf.Max(0.0001f, flipCooldown));
        public bool IsReady => !flipping && cooldownTimer <= 0f;

        private void Awake()
        {
            if (shiftButton != null) shiftButton.onClick.AddListener(TryFlip);
            if (commanderGlow != null)
            {
                glowBaseScale = commanderGlow.transform.localScale;
                var sr = commanderGlow.GetComponent<SpriteRenderer>();
                if (sr != null) glowBaseColor = sr.color;
            }
        }

        private void EnsureLeader()
        {
            if (leader != null) return;
            var go = GameObject.FindGameObjectWithTag("Leader");
            if (go != null) leader = go.transform;
        }

        private void Update()
        {
            var runner = LevelRunner.Instance;
            if (runner == null) return;
            EnsureLeader();
            if (leader == null) return;

            if (!flipping && cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (shiftButton != null) shiftButton.interactable = !runner.SurvivalComplete;

            bool playing = runner.State == LevelState.Playing && !runner.SurvivalComplete;

            if (flipping)
            {
                flipT += Time.deltaTime / Mathf.Max(0.0001f, flipDuration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flipT));
                SetLeaderY(Mathf.Lerp(flipFrom, flipTo, e));
                if (commanderGlow != null)
                {
                    float pop = Mathf.Sin(Mathf.Clamp01(flipT) * Mathf.PI); // swell then settle
                    commanderGlow.transform.localScale = Vector3.Lerp(glowBaseScale, glowBaseScale * glowScale, pop);
                }
                if (flipT >= 1f) Land(runner);
                return;
            }

            // Resting on a surface.
            SetLeaderY(gravityDown ? floorY : ceilingY);

            if (playing && IsReady && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                StartFlip(runner);
            }
        }

        public void OnPointerDown(PointerEventData eventData) => TryFlip();

        public void TryFlip()
        {
            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing || runner.SurvivalComplete) return;
            if (!IsReady) return;
            StartFlip(runner);
        }

        private void StartFlip(LevelRunner runner)
        {
            gravityDown = !gravityDown;
            flipping = true;
            flipT = 0f;
            flipFrom = leader.position.y;
            flipTo = gravityDown ? floorY : ceilingY;

            // Invulnerable while crossing so hazards on the surface are cleared naturally.
            runner.SetFrozen(true);
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
                commanderGlow.SetActive(true);
            }
        }

        private void Land(LevelRunner runner)
        {
            flipping = false;
            flipT = 0f;
            cooldownTimer = flipCooldown;
            SetLeaderY(gravityDown ? floorY : ceilingY);

            runner.SetFrozen(false);
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

        private void SetLeaderY(float y)
        {
            var p = leader.position;
            p.y = y;
            leader.position = p;
        }
    }
}
