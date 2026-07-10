using System.Collections;
using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class HazardLaser : MonoBehaviour
    {
        private enum State { Idle, Warning, Firing, Spent }

        [Header("Difficulty Timing")]
        [SerializeField] private float triggerDistance = 6f;
        [SerializeField] private float warningDuration = 1.2f;
        [SerializeField] private float fireDuration = 1f;

        [Header("Damage")]
        [SerializeField] private int killBatchSize = 10;

        [Header("Idle Visibility")]
        [SerializeField] private float idleAlpha = 0.35f;
        [SerializeField] private float idlePulseSpeed = 1.2f;
        [SerializeField] private float idleGlowIntensity = 1.5f;
        [SerializeField] private float firingGlowIntensity = 4f;

        [Header("Flicker")]
        [Tooltip("Organic light-increase/decrease flicker layered on top of the idle and firing glow, like a live power conduit.")]
        [SerializeField] private float flickerSpeed = 9f;
        [SerializeField] private float flickerAmount = 0.18f;

        [Header("Loop Reset Feel")]
        [Tooltip("How long a mid-fire beam eases back to idle when the time loop resets, instead of hard-cutting.")]
        [SerializeField] private float resetFadeDuration = 0.15f;

        [Header("Refs")]
        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private SpriteRenderer warningIndicator;
        [SerializeField] private ParticleSystem impactVfx;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private UnityEngine.Rendering.Universal.Light2D beamGlow;

        [Header("Audio")]
        [SerializeField] private AudioClip warningSirenClip;
        [SerializeField] private AudioClip freezeBlockClip;

        private BoxCollider2D hitCollider;
        private Transform commander;
        private State state = State.Idle;
        private bool hitRegistered;
        private bool frozenBlockRegistered;
        private Color beamBaseColor;
        private float flickerSeed;

        /// Perlin-noise based flicker multiplier, ~1 +/- flickerAmount. Unique phase per instance
        /// (via flickerSeed) so a row of lasers doesn't flicker in lockstep.
        private float Flicker()
        {
            float n = Mathf.PerlinNoise(flickerSeed, Time.time * flickerSpeed);
            return 1f + (n - 0.5f) * 2f * flickerAmount;
        }

        private void Awake()
        {
            flickerSeed = Random.Range(0f, 100f);
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.enabled = false;
            if (beamRenderer == null) beamRenderer = GetComponent<SpriteRenderer>();
            if (beamRenderer != null)
            {
                beamRenderer.enabled = true;
                beamBaseColor = beamRenderer.color;
                SetBeamAlpha(idleAlpha);
            }
            if (warningIndicator != null) warningIndicator.enabled = false;
            if (beamGlow != null)
            {
                beamGlow.enabled = true;
                beamGlow.intensity = idleGlowIntensity;
            }
        }

        private void OnEnable()
        {
            CommanderController.OnLoopReset += ResetState;
        }

        private void OnDisable()
        {
            CommanderController.OnLoopReset -= ResetState;
        }

        private void Update()
        {
            if (state != State.Idle) return;

            float pulse = (Mathf.Sin(Time.time * idlePulseSpeed) + 1f) * 0.5f;
            float flicker = Flicker();
            SetBeamAlpha(Mathf.Lerp(idleAlpha * 0.5f, idleAlpha, pulse) * flicker);
            if (beamGlow != null) beamGlow.intensity = Mathf.Lerp(idleGlowIntensity * 0.6f, idleGlowIntensity, pulse) * flicker;

            if (commander == null)
            {
                var leaderGo = GameObject.FindGameObjectWithTag("Leader");
                if (leaderGo != null) commander = leaderGo.transform;
                else return;
            }

            if (commander.position.x >= transform.position.x - triggerDistance)
            {
                state = State.Warning;
                StartCoroutine(RunSequence());
            }
        }

        private IEnumerator RunSequence()
        {
            if (warningIndicator != null) warningIndicator.enabled = true;
            AudioManager.PlaySfx(warningSirenClip);
            frozenBlockRegistered = false;

            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                if (warningIndicator != null)
                {
                    float pulse = (Mathf.Sin(elapsed * 12f) + 1f) * 0.5f;
                    var c = warningIndicator.color;
                    c.a = Mathf.Lerp(0.3f, 0.9f, pulse);
                    warningIndicator.color = c;
                }
                yield return null;
            }

            state = State.Firing;
            hitRegistered = false;
            if (warningIndicator != null) warningIndicator.enabled = false;
            hitCollider.enabled = true;
            if (impactVfx != null) impactVfx.Play();
            if (audioSource != null && audioSource.clip != null) audioSource.Play();

            float fireElapsed = 0f;
            while (fireElapsed < fireDuration)
            {
                fireElapsed += Time.deltaTime;
                float flicker = Flicker();
                SetBeamAlpha(beamBaseColor.a * flicker);
                if (beamGlow != null) beamGlow.intensity = firingGlowIntensity * flicker;
                yield return null;
            }

            state = State.Spent;
            hitCollider.enabled = false;
            SetBeamAlpha(idleAlpha);
            if (impactVfx != null) impactVfx.Stop();
            if (beamGlow != null) beamGlow.intensity = idleGlowIntensity;

            if (!hitRegistered && !frozenBlockRegistered)
            {
                LevelRunner.Instance?.RegisterSafePass();
            }
        }

        public void ResetState()
        {
            bool wasActive = state == State.Warning || state == State.Firing;
            StopAllCoroutines();
            state = State.Idle;
            hitRegistered = false;
            frozenBlockRegistered = false;
            hitCollider.enabled = false;
            if (warningIndicator != null)
            {
                warningIndicator.enabled = false;
                var c = warningIndicator.color;
                c.a = 0.3f;
                warningIndicator.color = c;
            }
            if (impactVfx != null) impactVfx.Stop();

            // A beam caught mid-fire eases back to idle instead of hard-cutting, so the
            // time-loop reset doesn't read as a jarring flash.
            if (wasActive && gameObject.activeInHierarchy)
            {
                StartCoroutine(FadeToIdle());
            }
            else
            {
                SetBeamAlpha(idleAlpha);
                if (beamGlow != null) beamGlow.intensity = idleGlowIntensity;
            }
        }

        private IEnumerator FadeToIdle()
        {
            float startAlpha = beamRenderer != null ? beamRenderer.color.a : idleAlpha;
            float startGlow = beamGlow != null ? beamGlow.intensity : idleGlowIntensity;
            float elapsed = 0f;
            while (elapsed < resetFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / resetFadeDuration;
                SetBeamAlpha(Mathf.Lerp(startAlpha, idleAlpha, t));
                if (beamGlow != null) beamGlow.intensity = Mathf.Lerp(startGlow, idleGlowIntensity, t);
                yield return null;
            }
            SetBeamAlpha(idleAlpha);
            if (beamGlow != null) beamGlow.intensity = idleGlowIntensity;
        }

        private void SetBeamAlpha(float alpha)
        {
            if (beamRenderer == null) return;
            var c = beamBaseColor;
            c.a = alpha;
            beamRenderer.color = c;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (state != State.Firing) return;
            if (!other.CompareTag("Leader")) return;

            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (runner.IsFrozen)
            {
                if (frozenBlockRegistered) return;
                frozenBlockRegistered = true;
                runner.RegisterFreezeBlock();
                AudioManager.PlaySfx(freezeBlockClip);
                return;
            }

            if (hitRegistered) return;
            hitRegistered = true;
            runner.RegisterHazardHit(killBatchSize);
        }
    }
}
