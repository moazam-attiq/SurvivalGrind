using System.Collections;
using UnityEngine;
using SurvivalGrind.UI;

namespace SurvivalGrind.Gameplay
{
    /// An aerial strike hazard (Barrage kind). A ground reticle telegraphs the strike zone, then a
    /// fast vertical beam of energy crashes down through it for a brief lethal window. Blocked ONLY
    /// by the Shield Dome. Triggers once when the Commander enters the zone, so it reads as a
    /// hand-placed set-piece rather than a looping trap.
    [RequireComponent(typeof(BoxCollider2D))]
    public class AerialStrikeZone : MonoBehaviour
    {
        private enum State { Armed, Telegraph, Strike, Spent }

        [Header("Timing")]
        [SerializeField] private float triggerDistance = 7f;
        [SerializeField] private float telegraphDuration = 1.0f;
        [SerializeField] private float strikeDuration = 0.35f;

        [Header("Damage")]
        [Tooltip("Max crowd members this hazard can kill per hit. Actual count is however many were really standing in the strike zone.")]
        [SerializeField] private int killBatchSize = 10;
        [Tooltip("Width (world units) of the real kill zone around the Commander at the moment of impact -- kept narrow (roughly one crowd row) since the strike zone's own trigger box is sized for detection range, not blast radius.")]
        [SerializeField] private float impactWidth = 1f;
        [SerializeField] private int shieldBlockBonus = 30;
        [Tooltip("Telegraph duration is multiplied down toward this fraction as the survival timer approaches its target, forcing quicker Shield reactions later in the run.")]
        [SerializeField] private float minWarningFactor = 0.5f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer reticleRenderer;
        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private ParticleSystem impactVfx;

        [Header("Audio")]
        [SerializeField] private AudioClip warningSfx;
        [SerializeField] private AudioClip strikeSfx;
        [SerializeField] private AudioClip blockSfx;

        private State state = State.Armed;
        private float timer;
        private BoxCollider2D hitCollider;
        private Transform commander;
        private bool hitRegistered;
        private bool frozenBlockRegistered;

        private void Awake()
        {
            hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
            if (reticleRenderer != null) reticleRenderer.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = false;
            HazardIndicator.Spawn(transform, HazardKind.Barrage, 1.8f);
        }

        private void Update()
        {
            if (state != State.Armed) return;

            if (commander == null)
            {
                var l = GameObject.FindGameObjectWithTag("Leader");
                if (l != null) commander = l.transform;
                else return;
            }

            var runner = LevelRunner.Instance;
            if (runner == null || runner.State != LevelState.Playing) return;

            if (commander.position.x >= transform.position.x - triggerDistance)
            {
                state = State.Telegraph;
                StartCoroutine(RunSequence());
            }
        }

        private IEnumerator RunSequence()
        {
            if (reticleRenderer != null) reticleRenderer.enabled = true;
            AudioManager.PlaySfx(warningSfx);
            LevelRunner.Instance?.NotifyHazardWarning(HazardKind.Barrage);

            float ramp = LevelRunner.Instance != null ? LevelRunner.Instance.DifficultyRamp01 : 0f;
            float effectiveTelegraph = Mathf.Lerp(telegraphDuration, telegraphDuration * minWarningFactor, ramp);

            float elapsed = 0f;
            while (elapsed < effectiveTelegraph)
            {
                elapsed += Time.deltaTime;
                if (reticleRenderer != null)
                {
                    // Pulse rate ramps up toward the strike, so the reticle visibly "ticks faster"
                    // as impact approaches instead of blinking at a flat rate the whole time.
                    float t = elapsed / effectiveTelegraph;
                    float pulseSpeed = Mathf.Lerp(10f, 24f, t);
                    float pulse = (Mathf.Sin(elapsed * pulseSpeed) + 1f) * 0.5f;
                    var c = reticleRenderer.color;
                    c.a = Mathf.Lerp(0.4f, 0.95f, pulse);
                    reticleRenderer.color = c;
                }
                yield return null;
            }

            state = State.Strike;
            hitRegistered = false;
            frozenBlockRegistered = false;
            if (reticleRenderer != null) reticleRenderer.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = true;
            if (impactVfx != null) impactVfx.Play();
            hitCollider.enabled = true;
            AudioManager.PlaySfx(strikeSfx);

            int totalKilled = 0;
            float strikeElapsed = 0f;
            while (strikeElapsed < strikeDuration)
            {
                strikeElapsed += Time.deltaTime;

                // The strike zone sits at a fixed spot; the crowd trails behind and scrolls into it
                // over this window, so keep sampling a narrow slice rather than judging by one instant.
                if (hitRegistered && !frozenBlockRegistered && totalKilled < killBatchSize)
                {
                    var formation = CrowdFormationController.Instance;
                    if (formation != null) totalKilled += formation.KillNear(hitCollider.bounds.center, impactWidth, killBatchSize - totalKilled);
                }
                yield return null;
            }

            state = State.Spent;
            hitCollider.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = false;
            if (impactVfx != null) impactVfx.Stop();

            if (hitRegistered && !frozenBlockRegistered)
            {
                LevelRunner.Instance?.RegisterHazardHit(totalKilled);
            }
            else if (!hitRegistered && !frozenBlockRegistered)
            {
                LevelRunner.Instance?.RegisterSafePass();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (state != State.Strike || !other.CompareTag("Leader")) return;
            var runner = LevelRunner.Instance;
            if (runner == null) return;

            if (runner.IsHazardNeutralized(HazardKind.Barrage))
            {
                if (frozenBlockRegistered) return;
                frozenBlockRegistered = true;
                runner.AddScore(shieldBlockBonus);
                runner.RegisterFreezeBlock("SHIELDED THE STRIKE");
                AudioManager.PlaySfx(blockSfx);
                return;
            }

            hitRegistered = true;
        }
    }
}
