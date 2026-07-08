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

        private void Awake()
        {
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
            SetBeamAlpha(Mathf.Lerp(idleAlpha * 0.5f, idleAlpha, pulse));
            if (beamGlow != null) beamGlow.intensity = Mathf.Lerp(idleGlowIntensity * 0.6f, idleGlowIntensity, pulse);

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
            SetBeamAlpha(beamBaseColor.a);
            if (impactVfx != null) impactVfx.Play();
            if (beamGlow != null) beamGlow.intensity = firingGlowIntensity;
            if (audioSource != null && audioSource.clip != null) audioSource.Play();

            yield return new WaitForSeconds(fireDuration);

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
            StopAllCoroutines();
            state = State.Idle;
            hitRegistered = false;
            frozenBlockRegistered = false;
            hitCollider.enabled = false;
            SetBeamAlpha(idleAlpha);
            if (warningIndicator != null)
            {
                warningIndicator.enabled = false;
                var c = warningIndicator.color;
                c.a = 0.3f;
                warningIndicator.color = c;
            }
            if (impactVfx != null) impactVfx.Stop();
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
